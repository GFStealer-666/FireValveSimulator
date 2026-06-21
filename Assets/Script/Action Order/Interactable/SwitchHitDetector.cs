using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

public class SwitchHitDetector : MonoBehaviour
{
    private enum TrackedHandId
    {
        Left,
        Right
    }

    public ActionType switchActionType;
    public UnityEvent onSwitchHit;

    [Header("Collider Activation")]
    [SerializeField] private string activatorTag = "PlayerHand";
    [SerializeField] private bool requireActivationInput = false;
    [SerializeField] private bool onlyActivateWhenCurrentStep = true;

    [Header("Tracked Hand Fallback")]
    [SerializeField] private bool enableTrackedHandFallback = true;
    [SerializeField] private bool requireTrackedHandPinch = false;
    [SerializeField] private float trackedHandActivationRadius = 0.05f;
    [SerializeField, Range(0f, 1f)] private float pinchActivationThreshold = 0.65f;
    [SerializeField] private float fallbackPinchMaxDistance = 0.035f;

    private static readonly List<XRHandSubsystem> HandSubsystems = new List<XRHandSubsystem>();

    private bool isTriggered = false;
    private ActionOrderManager actionOrderManager;
    private Collider switchCollider;
    private XRHandSubsystem handSubsystem;
    private XROrigin xrOrigin;

    private void Awake()
    {
        actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
        switchCollider = GetComponent<Collider>();
        xrOrigin = FindAnyObjectByType<XROrigin>();
    }

    private void Update()
    {
        if (!enableTrackedHandFallback || isTriggered)
            return;

        TryCompleteFromTrackedHands();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Entering {tag}");
        Debug.Log($"Other collider tag: {other.tag}");
        TryCompleteFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCompleteFromCollider(other);
    }

    public void ReTrigger()
    {
        isTriggered = false;
    }

    private void TryCompleteFromCollider(Collider other)
    {
        if (isTriggered || !IsActivatorCollider(other))
            return;

        if (requireActivationInput && !IsActivationPressed())
            return;

        CompleteSwitchHit();
    }

    private void TryCompleteFromTrackedHands()
    {
        if (requireActivationInput && !IsActivationPressed())
            return;

        if (!TryFindRunningHandSubsystem())
            return;

        if (IsTrackedHandTouchingSwitch(TrackedHandId.Left) || IsTrackedHandTouchingSwitch(TrackedHandId.Right))
            CompleteSwitchHit();
    }

    private void CompleteSwitchHit()
    {
        if (isTriggered || !CanActivateForCurrentStep())
            return;

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (actionOrderManager == null)
        {
            Debug.LogWarning($"Switch {tag} cannot report action because no ActionOrderManager was found.");
            return;
        }

        Debug.Log($"Switch activated. Action: {switchActionType}");
        actionOrderManager.RegisterAction(tag, switchActionType);
        onSwitchHit?.Invoke();
        isTriggered = true;
    }

    private bool IsActivatorCollider(Collider other)
    {
        if (other == null || string.IsNullOrEmpty(activatorTag))
            return false;

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag(activatorTag))
                return true;

            current = current.parent;
        }

        return false;
    }

    private bool CanActivateForCurrentStep()
    {
        if (!onlyActivateWhenCurrentStep)
            return true;

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (actionOrderManager == null)
            return false;

        ActionStep currentStep = actionOrderManager.GetCurrentStep();
        return currentStep != null &&
               currentStep.actionType == switchActionType &&
               actionOrderManager.CurrentStepRequiresTag(tag);
    }

    private bool IsTrackedHandTouchingSwitch(TrackedHandId handId)
    {
        XRHand hand = handId == TrackedHandId.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
        if (!hand.isTracked || !IsTrackedHandActivationHeld(handId, hand))
            return false;

        if (!TryGetTrackedHandWorldPosition(handId, hand, out Vector3 handPosition))
            return false;

        return IsWorldPositionNearSwitch(handPosition);
    }

    private bool TryGetTrackedHandWorldPosition(TrackedHandId handId, XRHand hand, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        XRCommonHandGestures gestures = GetCommonHandGestures(handId);

        if (gestures != null && gestures.TryGetPinchPose(out Pose pinchPose))
        {
            worldPosition = HandLocalToWorld(pinchPose.position);
            return IsValidVector(worldPosition);
        }

        XRHandJoint joint = hand.GetJoint(XRHandJointID.IndexTip);
        if (!joint.TryGetPose(out Pose jointPose))
            return false;

        worldPosition = HandLocalToWorld(jointPose.position);
        return IsValidVector(worldPosition);
    }

    private bool IsTrackedHandActivationHeld(TrackedHandId handId, XRHand hand)
    {
        if (!requireTrackedHandPinch)
            return true;

        XRCommonHandGestures gestures = GetCommonHandGestures(handId);
        if (gestures != null &&
            gestures.TryGetPinchValue(out float pinchValue) &&
            pinchValue >= pinchActivationThreshold)
        {
            return true;
        }

        return IsThumbAndIndexPinched(hand);
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

    private bool IsWorldPositionNearSwitch(Vector3 worldPosition)
    {
        if (switchCollider == null)
            switchCollider = GetComponent<Collider>();

        if (switchCollider == null || !switchCollider.enabled)
            return false;

        float radius = Mathf.Max(0f, trackedHandActivationRadius);
        Vector3 closestPoint = switchCollider.ClosestPoint(worldPosition);
        return (closestPoint - worldPosition).sqrMagnitude <= radius * radius;
    }

    private static bool IsValidVector(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
               !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }

    private static bool IsActivationPressed()
    {
        return IsTriggerPressed();
    }

    private static bool IsTriggerPressed()
    {
        return IsTriggerPressed(XRNode.LeftHand) || IsTriggerPressed(XRNode.RightHand);
    }

    private static bool IsTriggerPressed(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        return device.isValid &&
               device.TryGetFeatureValue(CommonUsages.triggerButton, out bool isPressed) &&
               isPressed;
    }
}
