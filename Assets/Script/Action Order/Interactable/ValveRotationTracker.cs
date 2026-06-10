using Unity.VRTemplate;
using UnityEngine;

public class ValveRotationTracker : MonoBehaviour
{
    public XRKnob knob;
    public ValveStateTracker linkedValve;
    [SerializeField] private float targetRotation;
    [SerializeField] private float currentAccumulatedRotation = 0f;

    private ActionOrderManager actionOrderManager;
    private float previousKnobRotation;

    private void Start()
    {
        actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
        SyncKnobBaseline();
    }

    private void OnEnable()
    {
        SyncKnobBaseline();
    }

    private void Update()
    {
        if (!HasRequiredReferences())
            return;

        ActionStep currentStep = actionOrderManager.GetCurrentStep();
        if (!CanTrackStep(currentStep))
        {
            currentAccumulatedRotation = 0f;
            SyncKnobBaseline();
            return;
        }

        float requiredRotation = currentStep.RequiredRotationDegrees > 0f
            ? currentStep.RequiredRotationDegrees
            : targetRotation;

        if (requiredRotation <= 0f)
        {
            Debug.LogWarning($"Valve {linkedValve.tag} has no required rotation target.");
            return;
        }

        float currentKnobRotation = knob.GetCurrentRotation();
        float deltaRotation = Mathf.DeltaAngle(previousKnobRotation, currentKnobRotation);
        currentAccumulatedRotation += deltaRotation;
        previousKnobRotation = currentKnobRotation;

        if (Mathf.Abs(currentAccumulatedRotation) >= requiredRotation)
        {
            Debug.Log($"Valve {linkedValve.tag} rotated to target.");
            currentAccumulatedRotation = 0f;
            linkedValve.ToggleValve();
            enabled = false;
        }
    }

    public void Reset()
    {
        enabled = true;
        currentAccumulatedRotation = 0f;

        if (knob != null)
        {
            knob.value = 0f;
            SyncKnobBaseline();
        }
    }

    private bool CanTrackStep(ActionStep currentStep)
    {
        return currentStep != null &&
               (currentStep.actionType == ActionType.TurnOnValve || currentStep.actionType == ActionType.TurnOffValve) &&
               actionOrderManager.CurrentStepRequiresTag(linkedValve.tag);
    }

    private bool HasRequiredReferences()
    {
        if (knob == null)
        {
            Debug.LogWarning($"{nameof(ValveRotationTracker)} on {name} is missing an XRKnob reference.");
            enabled = false;
            return false;
        }

        if (linkedValve == null)
        {
            Debug.LogWarning($"{nameof(ValveRotationTracker)} on {name} is missing a linked valve.");
            enabled = false;
            return false;
        }

        if (actionOrderManager == null)
        {
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
            if (actionOrderManager == null)
                return false;
        }

        return true;
    }

    private void SyncKnobBaseline()
    {
        if (knob != null)
            previousKnobRotation = knob.GetCurrentRotation();
    }
}
