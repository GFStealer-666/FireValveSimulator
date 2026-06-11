using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TwoHandValveRotator : XRBaseInteractable
{
    private enum TrackedHandId
    {
        Left,
        Right
    }

    private enum TrackedHandPoint
    {
        Pinch,
        Grip,
        Palm,
        IndexTip
    }

    private enum TrackedHandActivation
    {
        Pinch,
        Grasp,
        None
    }

    [SerializeField] private Transform handle;
    [SerializeField] private Transform rotationCenter;
    [SerializeField] private Vector3 localRotationAxis = Vector3.forward;
    [SerializeField] private Vector3 localReferenceDirection = Vector3.up;
    [SerializeField] private float fallbackRequiredRotationDegrees = 1000f;
    [SerializeField] private float maxDegreesPerFrame = 45f;
    [SerializeField] private ValveStateTracker linkedValve;

    [Header("Tracked Hand Fallback")]
    [SerializeField] private bool enableTrackedHandFallback = true;
    [SerializeField] private TrackedHandPoint trackedHandPoint = TrackedHandPoint.Pinch;
    [SerializeField] private TrackedHandActivation trackedHandActivation = TrackedHandActivation.Pinch;
    [SerializeField] private bool requireTwoTrackedHands = false;
    [SerializeField] private float handActivationRadius = 0.25f;
    [SerializeField, Range(0f, 1f)] private float pinchActivationThreshold = 0.65f;
    [SerializeField] private float fallbackPinchMaxDistance = 0.035f;
    [SerializeField, Range(0f, 1f)] private float graspActivationThreshold = 0.65f;
    [SerializeField] private float trackedHandPositionSmoothing = 16f;

    private static readonly List<XRHandSubsystem> HandSubsystems = new List<XRHandSubsystem>();
    private readonly Dictionary<IXRSelectInteractor, float> lastInteractorAngles = new Dictionary<IXRSelectInteractor, float>();
    private readonly Dictionary<TrackedHandId, float> lastTrackedHandAngles = new Dictionary<TrackedHandId, float>();
    private readonly Dictionary<TrackedHandId, Vector3> smoothedTrackedHandPositions = new Dictionary<TrackedHandId, Vector3>();

    private ActionOrderManager actionOrderManager;
    private ActionStep completedStep;
    private ActionStep trackedStep;
    private XRHandSubsystem handSubsystem;
    private XROrigin xrOrigin;
    private float accumulatedStepDegrees;

    protected override void Awake()
    {
        base.Awake();

        if (handle == null)
            handle = transform;

        if (rotationCenter == null)
            rotationCenter = handle;

        if (linkedValve == null)
            linkedValve = GetComponent<ValveStateTracker>();

        actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
        xrOrigin = FindAnyObjectByType<XROrigin>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        selectEntered.AddListener(HandleSelectEntered);
        selectExited.AddListener(HandleSelectExited);
    }

    protected override void OnDisable()
    {
        selectEntered.RemoveListener(HandleSelectEntered);
        selectExited.RemoveListener(HandleSelectExited);
        lastInteractorAngles.Clear();
        ClearTrackedHandState();
        base.OnDisable();
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);

        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            return;

        if (isSelected)
        {
            ClearTrackedHandState();
            ProcessInteractorRotation();
            return;
        }

        if (enableTrackedHandFallback)
            ProcessTrackedHandRotation();
    }

    public void ResetProgress()
    {
        accumulatedStepDegrees = 0f;
        completedStep = null;
        trackedStep = null;
        RefreshInteractorBaselines();
        ClearTrackedHandState();
    }

    private void HandleSelectEntered(SelectEnterEventArgs args)
    {
        if (TryGetInteractorAngle(args.interactorObject, out float angle))
            lastInteractorAngles[args.interactorObject] = angle;
    }

    private void HandleSelectExited(SelectExitEventArgs args)
    {
        lastInteractorAngles.Remove(args.interactorObject);
    }

    private void ProcessInteractorRotation()
    {
        float totalDelta = 0f;
        int validInteractorCount = 0;

        for (int i = 0; i < interactorsSelecting.Count; i++)
        {
            IXRSelectInteractor interactor = interactorsSelecting[i];
            if (!TryGetInteractorAngle(interactor, out float currentAngle))
                continue;

            if (!lastInteractorAngles.TryGetValue(interactor, out float lastAngle))
            {
                lastInteractorAngles[interactor] = currentAngle;
                continue;
            }

            float delta = Mathf.DeltaAngle(lastAngle, currentAngle);
            delta = Mathf.Clamp(delta, -maxDegreesPerFrame, maxDegreesPerFrame);

            lastInteractorAngles[interactor] = currentAngle;
            totalDelta += delta;
            validInteractorCount++;
        }

        if (validInteractorCount == 0)
            return;

        float averageDelta = totalDelta / validInteractorCount;
        if (Mathf.Approximately(averageDelta, 0f))
            return;

        RotateHandle(averageDelta);
        TrackActionProgress(averageDelta);
    }

    private void ProcessTrackedHandRotation()
    {
        if (!TryFindRunningHandSubsystem())
        {
            ClearTrackedHandState();
            return;
        }

        int activeHandCount = 0;
        float totalDelta = 0f;
        int validDeltaCount = 0;

        if (TryAccumulateTrackedHandDelta(TrackedHandId.Left, ref totalDelta, ref validDeltaCount))
            activeHandCount++;
        else
            ClearTrackedHandState(TrackedHandId.Left);

        if (TryAccumulateTrackedHandDelta(TrackedHandId.Right, ref totalDelta, ref validDeltaCount))
            activeHandCount++;
        else
            ClearTrackedHandState(TrackedHandId.Right);

        if (requireTwoTrackedHands && activeHandCount < 2)
            return;

        if (validDeltaCount == 0)
            return;

        float averageDelta = totalDelta / validDeltaCount;
        if (Mathf.Approximately(averageDelta, 0f))
            return;

        RotateHandle(averageDelta);
        TrackActionProgress(averageDelta);
    }

    private bool TryAccumulateTrackedHandDelta(TrackedHandId handId, ref float totalDelta, ref int validDeltaCount)
    {
        if (!TryGetTrackedHandAngle(handId, out float currentAngle))
            return false;

        if (!lastTrackedHandAngles.TryGetValue(handId, out float lastAngle))
        {
            lastTrackedHandAngles[handId] = currentAngle;
            return true;
        }

        float delta = Mathf.DeltaAngle(lastAngle, currentAngle);
        delta = Mathf.Clamp(delta, -maxDegreesPerFrame, maxDegreesPerFrame);

        lastTrackedHandAngles[handId] = currentAngle;
        totalDelta += delta;
        validDeltaCount++;
        return true;
    }

    private void TrackActionProgress(float rotationDelta)
    {
        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (actionOrderManager == null || linkedValve == null)
            return;

        ActionStep currentStep = actionOrderManager.GetCurrentStep();
        if (!CanCompleteCurrentStep(currentStep))
        {
            trackedStep = currentStep;
            accumulatedStepDegrees = 0f;
            return;
        }

        if (trackedStep != currentStep)
        {
            trackedStep = currentStep;
            completedStep = null;
            accumulatedStepDegrees = 0f;
        }

        if (completedStep == currentStep)
            return;

        accumulatedStepDegrees += Mathf.Abs(rotationDelta);
        float requiredRotation = currentStep.RequiredRotationDegrees > 0f
            ? currentStep.RequiredRotationDegrees
            : fallbackRequiredRotationDegrees;

        if (requiredRotation <= 0f)
            return;

        if (accumulatedStepDegrees >= requiredRotation)
        {
            completedStep = currentStep;
            accumulatedStepDegrees = 0f;
            linkedValve.ToggleValve();
        }
    }

    private bool CanCompleteCurrentStep(ActionStep currentStep)
    {
        return currentStep != null &&
               (currentStep.actionType == ActionType.TurnOnValve || currentStep.actionType == ActionType.TurnOffValve) &&
               actionOrderManager.CurrentStepRequiresTag(linkedValve.tag);
    }

    private bool TryGetInteractorAngle(IXRSelectInteractor interactor, out float angle)
    {
        angle = 0f;

        if (interactor == null || rotationCenter == null)
            return false;

        Transform attachTransform = interactor.GetAttachTransform(this);
        if (attachTransform == null)
            return false;

        return TryGetAngleFromWorldPosition(attachTransform.position, out angle);
    }

    private bool TryGetTrackedHandAngle(TrackedHandId handId, out float angle)
    {
        angle = 0f;

        if (!TryGetTrackedHandWorldPosition(handId, out Vector3 handPosition))
            return false;

        return TryGetAngleFromWorldPosition(handPosition, out angle);
    }

    private bool TryGetAngleFromWorldPosition(Vector3 worldPosition, out float angle)
    {
        angle = 0f;

        if (rotationCenter == null)
            return false;

        Vector3 center = rotationCenter.position;
        Vector3 normal = transform.TransformDirection(localRotationAxis.normalized);
        Vector3 reference = Vector3.ProjectOnPlane(transform.TransformDirection(localReferenceDirection), normal);
        Vector3 radial = Vector3.ProjectOnPlane(worldPosition - center, normal);

        if (reference.sqrMagnitude < 0.0001f)
            reference = Vector3.ProjectOnPlane(transform.up, normal);

        if (reference.sqrMagnitude < 0.0001f || radial.sqrMagnitude < 0.0001f)
            return false;

        angle = Vector3.SignedAngle(reference.normalized, radial.normalized, normal);
        return true;
    }

    private bool TryGetTrackedHandWorldPosition(TrackedHandId handId, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        XRHand hand = handId == TrackedHandId.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
        if (!hand.isTracked || !IsTrackedHandActivationHeld(handId, hand))
            return false;

        if (!TryGetTrackedHandLocalPosition(handId, hand, out Vector3 localPosition))
            return false;

        worldPosition = HandLocalToWorld(localPosition);
        if (!IsTrackedHandNearValve(worldPosition))
            return false;

        if (trackedHandPositionSmoothing > 0f && smoothedTrackedHandPositions.TryGetValue(handId, out Vector3 smoothedPosition))
        {
            float t = 1f - Mathf.Exp(-trackedHandPositionSmoothing * Time.deltaTime);
            worldPosition = Vector3.Lerp(smoothedPosition, worldPosition, t);
        }

        smoothedTrackedHandPositions[handId] = worldPosition;
        return true;
    }

    private bool TryGetTrackedHandLocalPosition(TrackedHandId handId, XRHand hand, out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        XRCommonHandGestures gestures = GetCommonHandGestures(handId);

        if (trackedHandPoint == TrackedHandPoint.Pinch && gestures != null && gestures.TryGetPinchPose(out Pose pinchPose))
        {
            localPosition = pinchPose.position;
            return IsValidVector(localPosition);
        }

        if (trackedHandPoint == TrackedHandPoint.Grip && gestures != null && gestures.TryGetGripPose(out Pose gripPose))
        {
            localPosition = gripPose.position;
            return IsValidVector(localPosition);
        }

        XRHandJointID jointId = trackedHandPoint == TrackedHandPoint.IndexTip
            ? XRHandJointID.IndexTip
            : XRHandJointID.Palm;

        XRHandJoint joint = hand.GetJoint(jointId);
        if (!joint.TryGetPose(out Pose jointPose))
            return false;

        localPosition = jointPose.position;
        return IsValidVector(localPosition);
    }

    private bool IsTrackedHandActivationHeld(TrackedHandId handId, XRHand hand)
    {
        XRCommonHandGestures gestures = GetCommonHandGestures(handId);

        switch (trackedHandActivation)
        {
            case TrackedHandActivation.None:
                return true;

            case TrackedHandActivation.Grasp:
                return gestures != null &&
                       gestures.TryGetGraspValue(out float graspValue) &&
                       graspValue >= graspActivationThreshold;

            case TrackedHandActivation.Pinch:
                if (gestures != null &&
                    gestures.TryGetPinchValue(out float pinchValue) &&
                    pinchValue >= pinchActivationThreshold)
                {
                    return true;
                }

                return IsThumbAndIndexPinched(hand);

            default:
                return false;
        }
    }

    private bool IsThumbAndIndexPinched(XRHand hand)
    {
        if (fallbackPinchMaxDistance <= 0f)
            return false;

        XRHandJoint thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
        XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);

        if (!thumbTip.TryGetPose(out Pose thumbPose) || !indexTip.TryGetPose(out Pose indexPose))
            return false;

        return Vector3.Distance(thumbPose.position, indexPose.position) <= fallbackPinchMaxDistance;
    }

    private XRCommonHandGestures GetCommonHandGestures(TrackedHandId handId)
    {
        if (handSubsystem == null)
            return null;

        return handId == TrackedHandId.Left
            ? handSubsystem.leftHandCommonGestures
            : handSubsystem.rightHandCommonGestures;
    }

    private bool TryFindRunningHandSubsystem()
    {
        if (handSubsystem != null && handSubsystem.running)
            return true;

        SubsystemManager.GetSubsystems(HandSubsystems);

        for (int i = 0; i < HandSubsystems.Count; i++)
        {
            XRHandSubsystem subsystem = HandSubsystems[i];
            if (subsystem != null && subsystem.running)
            {
                handSubsystem = subsystem;
                return true;
            }
        }

        handSubsystem = null;
        return false;
    }

    private Vector3 HandLocalToWorld(Vector3 localPosition)
    {
        if (xrOrigin == null)
            xrOrigin = FindAnyObjectByType<XROrigin>();

        Transform originTransform = xrOrigin != null && xrOrigin.Origin != null
            ? xrOrigin.Origin.transform
            : null;

        return originTransform != null ? originTransform.TransformPoint(localPosition) : localPosition;
    }

    private bool IsTrackedHandNearValve(Vector3 worldPosition)
    {
        if (handActivationRadius <= 0f)
            return true;

        float radiusSquared = handActivationRadius * handActivationRadius;

        for (int i = 0; i < colliders.Count; i++)
        {
            Collider valveCollider = colliders[i];
            if (valveCollider == null || !valveCollider.enabled)
                continue;

            Vector3 closestPoint = valveCollider.ClosestPoint(worldPosition);
            if ((closestPoint - worldPosition).sqrMagnitude <= radiusSquared)
                return true;
        }

        Transform centerTransform = rotationCenter != null ? rotationCenter : handle;
        return centerTransform != null &&
               (centerTransform.position - worldPosition).sqrMagnitude <= radiusSquared;
    }

    private static bool IsValidVector(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
               !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }

    private void RotateHandle(float degrees)
    {
        if (handle != null)
            handle.Rotate(localRotationAxis.normalized, degrees, Space.Self);
    }

    private void RefreshInteractorBaselines()
    {
        lastInteractorAngles.Clear();

        for (int i = 0; i < interactorsSelecting.Count; i++)
        {
            IXRSelectInteractor interactor = interactorsSelecting[i];
            if (TryGetInteractorAngle(interactor, out float angle))
                lastInteractorAngles[interactor] = angle;
        }
    }

    private void ClearTrackedHandState()
    {
        lastTrackedHandAngles.Clear();
        smoothedTrackedHandPositions.Clear();
    }

    private void ClearTrackedHandState(TrackedHandId handId)
    {
        lastTrackedHandAngles.Remove(handId);
        smoothedTrackedHandPositions.Remove(handId);
    }
}
