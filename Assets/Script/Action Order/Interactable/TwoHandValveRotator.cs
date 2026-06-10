using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TwoHandValveRotator : XRBaseInteractable
{
    [SerializeField] private Transform handle;
    [SerializeField] private Transform rotationCenter;
    [SerializeField] private Vector3 localRotationAxis = Vector3.forward;
    [SerializeField] private Vector3 localReferenceDirection = Vector3.up;
    [SerializeField] private float fallbackRequiredRotationDegrees = 1000f;
    [SerializeField] private float maxDegreesPerFrame = 45f;
    [SerializeField] private ValveStateTracker linkedValve;

    private readonly Dictionary<IXRSelectInteractor, float> lastInteractorAngles = new Dictionary<IXRSelectInteractor, float>();
    private ActionOrderManager actionOrderManager;
    private ActionStep completedStep;
    private ActionStep trackedStep;
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
        base.OnDisable();
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);

        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && isSelected)
            ProcessRotation();
    }

    public void ResetProgress()
    {
        accumulatedStepDegrees = 0f;
        completedStep = null;
        trackedStep = null;
        RefreshInteractorBaselines();
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

    private void ProcessRotation()
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
        Vector3 center = rotationCenter.position;
        Vector3 normal = transform.TransformDirection(localRotationAxis.normalized);
        Vector3 reference = Vector3.ProjectOnPlane(transform.TransformDirection(localReferenceDirection), normal);
        Vector3 radial = Vector3.ProjectOnPlane(attachTransform.position - center, normal);

        if (reference.sqrMagnitude < 0.0001f)
            reference = Vector3.ProjectOnPlane(transform.up, normal);

        if (reference.sqrMagnitude < 0.0001f || radial.sqrMagnitude < 0.0001f)
            return false;

        angle = Vector3.SignedAngle(reference.normalized, radial.normalized, normal);
        return true;
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
}
