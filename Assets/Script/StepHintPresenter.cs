using System;
using TMPro;
using UnityEngine;

public class StepHintPresenter : MonoBehaviour
{
    [Serializable]
    public class StepHintBinding
    {
        public ActionStep step;
        public GameObject hintPanel;
        public TMP_Text hintText;
        public GameObject learningDetailPanel;
        public TMP_Text learningDetailText;
    }

    [SerializeField] private SimulatorModeManager modeManager;
    [SerializeField] private ActionOrderManager actionOrderManager;
    [SerializeField] private StepHintTextLibrary textLibrary;
    [SerializeField] private StepHintBinding[] hints;
    private SimulatorMode currentMode = SimulatorMode.Menu;
    private ActionStep currentStep;

    private void Awake()
    {
        ResolveReferences();
        HideAllHints();
    }

    private void OnEnable()
    {
        SimulatorModeManager.OnModeChanged += HandleModeChanged;
        ActionOrderManager.OnCurrentStepChanged += HandleCurrentStepChanged;
    }

    private void OnDisable()
    {
        SimulatorModeManager.OnModeChanged -= HandleModeChanged;
        ActionOrderManager.OnCurrentStepChanged -= HandleCurrentStepChanged;
    }

    private void Start()
    {
        ResolveReferences();

        if (modeManager != null)
            currentMode = modeManager.CurrentMode;

        if (actionOrderManager != null)
            currentStep = actionOrderManager.GetCurrentStep();

        Refresh();
    }

    public void Refresh()
    {
        HideAllHints();

        if (currentStep == null || currentMode == SimulatorMode.Menu || currentMode == SimulatorMode.Exam)
            return;

        StepHintBinding binding = FindBinding(currentStep) ?? FindReusableBinding();
        if (binding == null)
            return;

        ShowBindingForCurrentMode(binding);
    }

    private void HandleModeChanged(SimulatorMode mode)
    {
        currentMode = mode;
        Refresh();
    }

    private void HandleCurrentStepChanged(ActionStep step, int stepIndex)
    {
        currentStep = step;
        Refresh();
    }

    private void ShowBindingForCurrentMode(StepHintBinding binding)
    {
        ApplyText(binding);

        if (currentMode == SimulatorMode.Learning)
        {
            SetActive(binding.hintPanel, true);
            SetActive(binding.learningDetailPanel, true);
            return;
        }

        if (currentMode == SimulatorMode.Training)
            SetActive(binding.hintPanel, true);
    }

    private void ApplyText(StepHintBinding binding)
    {
        if (binding == null || textLibrary == null)
            return;

        StepHintTextLibrary.Entry entry = textLibrary.Find(currentStep);
        if (entry == null)
            return;

        TMP_Text hintText = binding.hintText != null ? binding.hintText : GetText(binding.hintPanel);
        TMP_Text learningDetailText = binding.learningDetailText != null
            ? binding.learningDetailText
            : GetText(binding.learningDetailPanel);

        if (hintText != null)
            hintText.text = entry.trainingText;

        if (learningDetailText != null)
            learningDetailText.text = entry.learningDetailText;
    }

    private TMP_Text GetText(GameObject owner)
    {
        return owner != null ? owner.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private StepHintBinding FindBinding(ActionStep step)
    {
        if (hints == null)
            return null;

        foreach (StepHintBinding binding in hints)
        {
            if (binding != null && binding.step == step)
                return binding;
        }

        return null;
    }

    private StepHintBinding FindReusableBinding()
    {
        if (hints == null)
            return null;

        foreach (StepHintBinding binding in hints)
        {
            if (binding != null && binding.step == null && HasDisplayTarget(binding))
                return binding;
        }

        return null;
    }

    private bool HasDisplayTarget(StepHintBinding binding)
    {
        return binding.hintPanel != null ||
               binding.hintText != null ||
               binding.learningDetailPanel != null ||
               binding.learningDetailText != null;
    }

    private void HideAllHints()
    {
        if (hints == null)
            return;

        foreach (StepHintBinding binding in hints)
        {
            if (binding == null)
                continue;

            SetActive(binding.hintPanel, false);
            SetActive(binding.learningDetailPanel, false);
        }
    }

    private void SetActive(GameObject obj, bool active)
    {
        if (obj != null)
            obj.SetActive(active);
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
            modeManager = FindAnyObjectByType<SimulatorModeManager>(FindObjectsInactive.Include);

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);
    }
}
