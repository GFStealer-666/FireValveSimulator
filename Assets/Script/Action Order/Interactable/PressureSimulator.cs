using System.Collections;
using TMPro;
using UnityEngine;

public class PressureSimulator : MonoBehaviour
{
    public ActionOrderManager actionOrderManager;
    public TextMeshProUGUI pressureText;
    public float pressureIncreaseRate = 10f;

    private float currentPressure = 0f;
    private Coroutine pressureCoroutine;

    public bool isActive = false;

    private void Awake()
    {
        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
    }

    private void Update()
    {
        if (!isActive || pressureCoroutine != null)
            return;

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (actionOrderManager == null)
            return;

        ActionStep currentStep = actionOrderManager.GetCurrentStep();
        if (currentStep != null && currentStep.actionType == ActionType.CheckPSI)
            pressureCoroutine = StartCoroutine(PressureBuildUp(currentStep, currentStep.PressureTarget));
    }

    public void ResetPressure()
    {
        if (pressureCoroutine != null)
        {
            StopCoroutine(pressureCoroutine);
            pressureCoroutine = null;
        }

        currentPressure = 0f;
        if (pressureText != null)
            pressureText.text = "";
    }

    private IEnumerator PressureBuildUp(ActionStep step, float targetPressure)
    {
        while (currentPressure < targetPressure)
        {
            currentPressure += pressureIncreaseRate * Time.deltaTime;

            if (pressureText != null)
                pressureText.text = $"Pressure: {Mathf.RoundToInt(currentPressure)} PSI";

            yield return null;
        }

        if (pressureText != null)
            pressureText.text = $"Pressure: {Mathf.RoundToInt(targetPressure)} PSI";

        pressureCoroutine = null;
        currentPressure = 0f;

        if (actionOrderManager != null && actionOrderManager.GetCurrentStep() == step)
            actionOrderManager.RegisterAction("", ActionType.CheckPSI);

        if (pressureText != null)
            pressureText.text = "";
    }
}
