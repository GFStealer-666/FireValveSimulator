using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class ActionOrderManager : MonoBehaviour
{
    public List<ActionStep> orderedSteps;
    private int currentStepIndex = 0;
    [SerializeField] private ActionStep currentStep;
    private bool sequenceActive = false;
    private readonly HashSet<string> completedTagsInCurrentStep = new HashSet<string>();

    public UnityEvent onAllStepsCompleted;
    public UnityEvent onStepFailed;
    public UnityEvent onStepSuccess;
    public UnityEvent OnSubStepCompleted;

    public static event Action OnAllStepsCompleted, OnStepFailed, OnStepSuccess;
    public static event Action<ActionStep, int> OnCurrentStepChanged;
    public event Action<ActionStep, int> CurrentStepChanged;

    private readonly List<ObjectHighlighter> activeHighlighters = new List<ObjectHighlighter>();
    public bool isExam = false;
    public TMP_Text stepText;
    public int CurrentStepIndex => sequenceActive ? currentStepIndex : -1;
    public int StepCount => orderedSteps != null ? orderedSteps.Count : 0;

    public void InitializeSequence()
    {
        if (!HasSteps())
        {
            sequenceActive = false;
            currentStep = null;
            UpdateCurrentStepUI();
            NotifyCurrentStepChanged();
            Debug.LogWarning("ActionOrderManager cannot initialize because no action steps are assigned.");
            return;
        }

        sequenceActive = true;
        currentStepIndex = Mathf.Clamp(currentStepIndex, 0, orderedSteps.Count - 1);
        currentStep = orderedSteps[currentStepIndex];
        UpdateCurrentStepUI();

        if (!isExam)
            HighlightCurrentStepObjects();

        NotifyCurrentStepChanged();
    }

    public void RegisterAction(string objectTag, ActionType actionType)
    {
        if (!HasSteps() || currentStepIndex >= orderedSteps.Count)
            return;

        currentStep = orderedSteps[currentStepIndex];
        if (currentStep == null)
        {
            Debug.LogWarning($"Action step {currentStepIndex} is missing.");
            return;
        }

        if (currentStep.actionType != actionType)
        {
            Debug.LogWarning($"Wrong action type for {objectTag}");
            TriggerStepFailed();
            return;
        }

        if (currentStep.objectTags == null || currentStep.objectTags.Length == 0)
        {
            Debug.Log($"Action {actionType} completed for {currentStep.stepName}.");
            CompleteStep();
            return;
        }

        if (CurrentStepRequiresTag(objectTag))
        {
            completedTagsInCurrentStep.Add(objectTag);
            Debug.Log($"Sub-action {objectTag} completed for {currentStep.stepName}");
            OnSubStepCompleted?.Invoke();

            if (completedTagsInCurrentStep.Count >= currentStep.objectTags.Length)
                CompleteStep();
        }
        else
        {
            Debug.LogWarning($"Wrong object {objectTag} for current step");
            TriggerStepFailed();
        }
    }

    public ActionStep GetCurrentStep()
    {
        if (!sequenceActive)
            return null;

        if (HasSteps() && currentStepIndex < orderedSteps.Count)
            return orderedSteps[currentStepIndex];

        return null;
    }

    public bool CurrentStepRequiresTag(string objectTag)
    {
        if (string.IsNullOrEmpty(objectTag))
            return false;

        ActionStep step = GetCurrentStep();
        return step != null &&
               step.objectTags != null &&
               Array.Exists(step.objectTags, tag => tag == objectTag);
    }

    public void ResetSequence()
    {
        Debug.Log("Resetting sequence...");
        sequenceActive = true;
        currentStepIndex = 0;
        completedTagsInCurrentStep.Clear();

        if (!HasSteps())
        {
            sequenceActive = false;
            currentStep = null;
            ClearHighlights();
            UpdateCurrentStepUI();
            NotifyCurrentStepChanged();
            Debug.LogWarning("ActionOrderManager cannot reset because no action steps are assigned.");
            return;
        }

        currentStep = orderedSteps[currentStepIndex];

        if (!isExam)
            HighlightCurrentStepObjects();

        UpdateCurrentStepUI();
        NotifyCurrentStepChanged();
    }

    public void ResetToIdle()
    {
        sequenceActive = false;
        currentStepIndex = 0;
        completedTagsInCurrentStep.Clear();
        currentStep = null;
        ClearHighlights();

        if (stepText != null)
            stepText.text = "";

        NotifyCurrentStepChanged();
    }

    public void SetExamMode(bool isExamMode)
    {
        isExam = isExamMode;

        if (isExam)
        {
            Debug.Log("Exam mode ON.");
            ClearHighlights();
        }
        else
        {
            HighlightCurrentStepObjects();
        }
    }

    public void TriggerFailure()
    {
        Debug.LogWarning("Exam failed due to timeout.");
        TriggerStepFailed();
    }

    public void UpdateCurrentStepUI()
    {
        if (stepText == null)
            return;

        int totalSteps = orderedSteps != null ? orderedSteps.Count : 0;
        if (totalSteps == 0)
        {
            stepText.text = "No steps assigned";
            return;
        }

        if (currentStepIndex < totalSteps)
        {
            int displayStep = currentStepIndex + 1;
            stepText.text = $"Step {displayStep} of {totalSteps}";
        }
        else
        {
            stepText.text = "All steps completed!";
        }
    }

    private void HighlightCurrentStepObjects()
    {
        if (isExam)
            return;

        ClearHighlights();

        ActionStep step = GetCurrentStep();
        if (step == null || step.objectTags == null)
            return;

        foreach (string tag in step.objectTags)
        {
            if (string.IsNullOrEmpty(tag))
                continue;

            GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in objs)
            {
                ObjectHighlighter highlighter = obj.GetComponentInChildren<ObjectHighlighter>();
                if (highlighter != null)
                {
                    highlighter.Highlight();
                    activeHighlighters.Add(highlighter);
                }
            }
        }
    }

    private void CompleteStep()
    {
        Debug.Log($"Step {currentStep.stepName} completed!");
        onStepSuccess?.Invoke();
        OnStepSuccess?.Invoke();

        currentStepIndex++;
        completedTagsInCurrentStep.Clear();

        if (currentStepIndex >= orderedSteps.Count)
        {
            Debug.Log("All steps completed!");
            sequenceActive = false;
            currentStep = null;
            ClearHighlights();
            onAllStepsCompleted?.Invoke();
            OnAllStepsCompleted?.Invoke();
            UpdateCurrentStepUI();
            NotifyCurrentStepChanged();
        }
        else
        {
            currentStep = orderedSteps[currentStepIndex];
            UpdateCurrentStepUI();
            HighlightCurrentStepObjects();
            NotifyCurrentStepChanged();
        }
    }

    private void ClearHighlights()
    {
        foreach (ObjectHighlighter highlighter in activeHighlighters)
        {
            if (highlighter != null)
                highlighter.RemoveHighlight();
        }

        activeHighlighters.Clear();
    }

    private void TriggerStepFailed()
    {
        onStepFailed?.Invoke();
        OnStepFailed?.Invoke();
    }

    private void NotifyCurrentStepChanged()
    {
        int stepIndex = sequenceActive ? currentStepIndex : -1;
        CurrentStepChanged?.Invoke(currentStep, stepIndex);
        OnCurrentStepChanged?.Invoke(currentStep, stepIndex);
    }

    private bool HasSteps()
    {
        return orderedSteps != null && orderedSteps.Count > 0;
    }
}
