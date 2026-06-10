using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class SimulatorModeManager : MonoBehaviour
{
    [SerializeField] private SimulatorMode currentMode = SimulatorMode.Menu;
    [SerializeField] private bool showMenuOnStart = true;

    [Header("Exam Skip")]
    [FormerlySerializedAs("allowExamStepSkip")]
    [SerializeField] private bool isExamSkippable = false;
    [Min(0f)]
    [SerializeField] private float examSkipPenaltySeconds = 20f;

    public UnityEvent onEnterMenu;
    public UnityEvent onEnterLearning;
    public UnityEvent onEnterTraining;
    public UnityEvent onEnterExam;
    public UnityEvent onModeChanged;

    public static event Action<SimulatorMode> OnModeChanged;

    public SimulatorMode CurrentMode => currentMode;
    public bool IsExamSkippable => isExamSkippable;
    public float ExamSkipPenaltySeconds => Mathf.Max(0f, examSkipPenaltySeconds);

    public bool IsStepSkipAllowedInMode(SimulatorMode mode)
    {
        return mode == SimulatorMode.Learning ||
               mode == SimulatorMode.Training ||
               (mode == SimulatorMode.Exam && isExamSkippable);
    }

    private void Start()
    {
        if (showMenuOnStart)
            ShowMenu();
        else
            PublishModeChanged();
    }

    public void ShowMenu()
    {
        StartMode(SimulatorMode.Menu);
    }

    public void StartLearningMode()
    {
        StartMode(SimulatorMode.Learning);
    }

    public void StartTrainingMode()
    {
        StartMode(SimulatorMode.Training);
    }

    public void StartExamMode()
    {
        StartMode(SimulatorMode.Exam);
    }

    public void StartMode(SimulatorMode mode)
    {
        currentMode = mode;
        PublishModeChanged();
    }

    public bool TryStartModeBySceneName(string sceneOrModeName)
    {
        if (!TryGetModeFromSceneName(sceneOrModeName, out SimulatorMode mode))
            return false;

        StartMode(mode);
        return true;
    }

    public void ResetCurrentMode()
    {
        PublishModeChanged();
    }

    private void PublishModeChanged()
    {
        OnModeChanged?.Invoke(currentMode);
        onModeChanged?.Invoke();

        if (currentMode == SimulatorMode.Menu)
            onEnterMenu?.Invoke();
        else if (currentMode == SimulatorMode.Learning)
            onEnterLearning?.Invoke();
        else if (currentMode == SimulatorMode.Training)
            onEnterTraining?.Invoke();
        else if (currentMode == SimulatorMode.Exam)
            onEnterExam?.Invoke();
    }

    private bool TryGetModeFromSceneName(string sceneOrModeName, out SimulatorMode mode)
    {
        mode = SimulatorMode.Menu;

        if (string.IsNullOrWhiteSpace(sceneOrModeName))
            return false;

        string normalized = sceneOrModeName.Trim().ToLowerInvariant().Replace(" ", "");
        switch (normalized)
        {
            case "mainmenu":
            case "menu":
                mode = SimulatorMode.Menu;
                return true;

            case "learningscene":
            case "learning":
                mode = SimulatorMode.Learning;
                return true;

            case "trainningscene":
            case "trainingscene":
            case "trainning":
            case "training":
                mode = SimulatorMode.Training;
                return true;

            case "examscene":
            case "exam":
                mode = SimulatorMode.Exam;
                return true;

            default:
                return false;
        }
    }
}
