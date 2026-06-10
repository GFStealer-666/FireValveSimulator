using UnityEngine;
using UnityEngine.UI;

public class SimulatorMenuPanelController : MonoBehaviour
{
    [SerializeField] private SimulatorModeManager modeManager;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject learningModePanel;
    [SerializeField] private GameObject trainingModePanel;
    [SerializeField] private GameObject examModePanel;
    [SerializeField] private GameObject completePanel;
    [SerializeField] private GameObject examFailedPanel;
    [SerializeField] private bool showMainMenuOnStart = true;
    [SerializeField] private bool wireButtonListenersOnEnable = true;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button learningModeButton;
    [SerializeField] private Button trainingModeButton;
    [SerializeField] private Button examModeButton;

    [Header("Learning Panel Buttons")]
    [SerializeField] private Button learningBackButton;
    [SerializeField] private Button learningStartButton;

    [Header("Training Panel Buttons")]
    [SerializeField] private Button trainingBackButton;
    [SerializeField] private Button trainingStartButton;

    [Header("Exam Panel Buttons")]
    [SerializeField] private Button examBackButton;
    [SerializeField] private Button examStartButton;

    private void Awake()
    {
        if (modeManager == null)
            modeManager = FindAnyObjectByType<SimulatorModeManager>(FindObjectsInactive.Include);

        ResolveButtonReferences();
    }

    private void OnEnable()
    {
        SimulatorModeManager.OnModeChanged += HandleModeChanged;

        if (wireButtonListenersOnEnable)
            WireButtonListeners();
    }

    private void OnDisable()
    {
        SimulatorModeManager.OnModeChanged -= HandleModeChanged;

        if (wireButtonListenersOnEnable)
            UnwireButtonListeners();
    }

    private void Start()
    {
        if (showMainMenuOnStart)
            ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        SetPanels(mainMenu: true);
    }

    [ContextMenu("Test/Show Learning Panel")]
    public void ShowLearningPanel()
    {
        SetPanels(learning: true);
    }

    [ContextMenu("Test/Show Training Panel")]
    public void ShowTrainingPanel()
    {
        SetPanels(training: true);
    }

    [ContextMenu("Test/Show Exam Panel")]
    public void ShowExamPanel()
    {
        SetPanels(exam: true);
    }

    [ContextMenu("Test/Start Learning Mode")]
    public void StartLearningMode()
    {
        if (modeManager != null)
            modeManager.StartLearningMode();
        else
            Debug.LogWarning("Cannot start Learning mode because SimulatorModeManager is not assigned.");
    }

    [ContextMenu("Test/Start Training Mode")]
    public void StartTrainingMode()
    {
        if (modeManager != null)
            modeManager.StartTrainingMode();
        else
            Debug.LogWarning("Cannot start Training mode because SimulatorModeManager is not assigned.");
    }

    [ContextMenu("Test/Start Exam Mode")]
    public void StartExamMode()
    {
        if (modeManager != null)
            modeManager.StartExamMode();
        else
            Debug.LogWarning("Cannot start Exam mode because SimulatorModeManager is not assigned.");
    }

    [ContextMenu("Test/Show Main Menu")]
    public void TestShowMainMenu()
    {
        if (modeManager != null)
            modeManager.ShowMenu();
        else
            ShowMainMenu();
    }

    [ContextMenu("Setup/Resolve Button References")]
    public void ResolveButtonReferences()
    {
        if (learningModeButton == null)
            learningModeButton = FindButtonInPanel(mainMenuPanel, "LearningButton", "Learning Mode");

        if (trainingModeButton == null)
            trainingModeButton = FindButtonInPanel(mainMenuPanel, "TrainingButton", "TraniningButton", "Training Mode");

        if (examModeButton == null)
            examModeButton = FindButtonInPanel(mainMenuPanel, "ExamButton", "Exam Mode");

        if (learningBackButton == null)
            learningBackButton = FindButtonInPanel(learningModePanel, "BackButton", "Back");

        if (learningStartButton == null)
            learningStartButton = FindButtonInPanel(learningModePanel, "StartButton", "Start");

        if (trainingBackButton == null)
            trainingBackButton = FindButtonInPanel(trainingModePanel, "BackButton", "Back");

        if (trainingStartButton == null)
            trainingStartButton = FindButtonInPanel(trainingModePanel, "StartButton", "Start");

        if (examBackButton == null)
            examBackButton = FindButtonInPanel(examModePanel, "BackButton", "Back");

        if (examStartButton == null)
            examStartButton = FindButtonInPanel(examModePanel, "StartButton", "Start");
    }

    [ContextMenu("Setup/Wire Button Listeners")]
    public void WireButtonListeners()
    {
        ResolveButtonReferences();

        WireButton(learningModeButton, ShowLearningPanel);
        WireButton(trainingModeButton, ShowTrainingPanel);
        WireButton(examModeButton, ShowExamPanel);

        WireButton(learningBackButton, ShowMainMenu);
        WireButton(learningStartButton, StartLearningMode);

        WireButton(trainingBackButton, ShowMainMenu);
        WireButton(trainingStartButton, StartTrainingMode);

        WireButton(examBackButton, ShowMainMenu);
        WireButton(examStartButton, StartExamMode);
    }

    [ContextMenu("Setup/Unwire Button Listeners")]
    public void UnwireButtonListeners()
    {
        UnwireButton(learningModeButton, ShowLearningPanel);
        UnwireButton(trainingModeButton, ShowTrainingPanel);
        UnwireButton(examModeButton, ShowExamPanel);

        UnwireButton(learningBackButton, ShowMainMenu);
        UnwireButton(learningStartButton, StartLearningMode);

        UnwireButton(trainingBackButton, ShowMainMenu);
        UnwireButton(trainingStartButton, StartTrainingMode);

        UnwireButton(examBackButton, ShowMainMenu);
        UnwireButton(examStartButton, StartExamMode);
    }

    private void HandleModeChanged(SimulatorMode mode)
    {
        if (mode == SimulatorMode.Menu)
            ShowMainMenu();
        else
            HideModeSelectionPanels();
    }

    private void HideModeSelectionPanels()
    {
        SetPanels();
    }

    private void SetPanels(bool mainMenu = false, bool learning = false, bool training = false, bool exam = false)
    {
        SetActive(mainMenuPanel, mainMenu);
        SetActive(learningModePanel, learning);
        SetActive(trainingModePanel, training);
        SetActive(examModePanel, exam);
        SetActive(completePanel, false);
        SetActive(examFailedPanel, false);
    }

    private void SetActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private Button FindButtonInPanel(GameObject panel, params string[] names)
    {
        if (panel == null || names == null)
            return null;

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (string name in names)
        {
            foreach (Button button in buttons)
            {
                if (button != null && button.name == name)
                    return button;
            }
        }

        foreach (string name in names)
        {
            foreach (Button button in buttons)
            {
                if (button != null && button.name.Contains(name))
                    return button;
            }
        }

        return null;
    }

    private void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void UnwireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        button.onClick.RemoveListener(action);
    }
}
