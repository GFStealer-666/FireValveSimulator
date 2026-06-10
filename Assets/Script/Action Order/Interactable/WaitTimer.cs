using System.Collections;
using TMPro;
using UnityEngine;

public class WaitTimer : MonoBehaviour
{
    public ActionOrderManager actionOrderManager;
    public TMP_Text timerText;

    [SerializeField] private float waitDuration = 0f;
    private float remainingTime = 0f;
    private Coroutine waitCoroutine;

    public bool isActive = false;

    private void Awake()
    {
        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
    }

    private void Update()
    {
        if (!isActive || waitCoroutine != null)
            return;

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (actionOrderManager == null)
            return;

        ActionStep currentStep = actionOrderManager.GetCurrentStep();
        if (currentStep != null && currentStep.actionType == ActionType.WaitLimitedTime)
        {
            waitDuration = currentStep.WaitDuration;
            waitCoroutine = StartCoroutine(WaitAndCompleteStep(currentStep, waitDuration));
        }
    }

    public void ResetTimer()
    {
        if (waitCoroutine != null)
        {
            StopCoroutine(waitCoroutine);
            waitCoroutine = null;
        }

        remainingTime = 0f;
        if (timerText != null)
            timerText.text = "";
    }

    private IEnumerator WaitAndCompleteStep(ActionStep step, float duration)
    {
        Debug.Log($"Waiting {duration} seconds...");
        remainingTime = duration;

        while (remainingTime > 0f)
        {
            if (timerText != null)
                timerText.text = $"Waiting: {Mathf.CeilToInt(remainingTime)}s";

            yield return null;
            remainingTime -= Time.deltaTime;
        }

        if (timerText != null)
            timerText.text = "";

        waitCoroutine = null;

        if (actionOrderManager != null && actionOrderManager.GetCurrentStep() == step)
            actionOrderManager.RegisterAction("", ActionType.WaitLimitedTime);
    }
}
