using UnityEngine;

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

    private void Awake()
    {
        if (modeManager == null)
            modeManager = FindAnyObjectByType<SimulatorModeManager>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        SimulatorModeManager.OnModeChanged += HandleModeChanged;
    }

    private void OnDisable()
    {
        SimulatorModeManager.OnModeChanged -= HandleModeChanged;
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

    public void ShowLearningPanel()
    {
        SetPanels(learning: true);
    }

    public void ShowTrainingPanel()
    {
        SetPanels(training: true);
    }

    public void ShowExamPanel()
    {
        SetPanels(exam: true);
    }

    public void StartLearningMode()
    {
        if (modeManager != null)
            modeManager.StartLearningMode();
    }

    public void StartTrainingMode()
    {
        if (modeManager != null)
            modeManager.StartTrainingMode();
    }

    public void StartExamMode()
    {
        if (modeManager != null)
            modeManager.StartExamMode();
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
}
