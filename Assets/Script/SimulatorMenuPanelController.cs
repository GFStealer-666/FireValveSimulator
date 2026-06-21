using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class SimulatorMenuPanelController : MonoBehaviour
{
    private const string DefaultHoldSpinnerAssetPath = "Assets/Animated Loading Icons/Prefabs/Spinner/Spinner 1.prefab";

    [SerializeField] private SimulatorModeManager modeManager;
    [SerializeField] private ActionOrderManager actionOrderManager;
    [SerializeField] private ExamManager examManager;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject learningModePanel;
    [SerializeField] private GameObject trainingModePanel;
    [SerializeField] private GameObject examModePanel;
    [SerializeField] private GameObject completePanel;
    [SerializeField] private GameObject examFailedPanel;
    [SerializeField] private bool showMainMenuOnStart = true;
    [SerializeField] private bool wireButtonListenersOnEnable = true;

    [Header("Hold To Confirm")]
    [SerializeField] private bool requireHoldToConfirm = true;
    [SerializeField, Min(0f)] private float holdToConfirmSeconds = 0.5f;
    [SerializeField] private GameObject holdSpinnerPrefab;
    [SerializeField] private Vector2 holdSpinnerSize = new Vector2(72f, 72f);
    [SerializeField] private Vector2 holdSpinnerAnchoredOffset = Vector2.zero;

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

    [Header("In-Mode Return Button")]
    [SerializeField] private GameObject returnToMenuButtonRoot;
    [SerializeField] private Button returnToMenuButton;

    [Header("Learning/Training Skip Button")]
    [SerializeField] private GameObject skipStepButtonRoot;
    [SerializeField] private Button skipStepButton;

    private void Awake()
    {
#if UNITY_EDITOR
        ResolveDefaultSpinnerPrefabInEditor();
#endif

        if (modeManager == null)
            modeManager = FindAnyObjectByType<SimulatorModeManager>(FindObjectsInactive.Include);

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);

        if (examManager == null)
            examManager = FindAnyObjectByType<ExamManager>(FindObjectsInactive.Include);

        ResolveButtonReferences();
        EnsureSkipStepButtonExists();
        ConfigureHoldToConfirmButtons();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDefaultSpinnerPrefabInEditor();
    }

    private void OnValidate()
    {
        holdToConfirmSeconds = Mathf.Max(0f, holdToConfirmSeconds);
        holdSpinnerSize.x = Mathf.Max(0f, holdSpinnerSize.x);
        holdSpinnerSize.y = Mathf.Max(0f, holdSpinnerSize.y);
        ResolveDefaultSpinnerPrefabInEditor();
    }

    private void ResolveDefaultSpinnerPrefabInEditor()
    {
        if (holdSpinnerPrefab == null)
            holdSpinnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultHoldSpinnerAssetPath);
    }

    [ContextMenu("Setup/Attach Hold To Confirm Components To Buttons")]
    private void AttachHoldToConfirmComponentsToButtons()
    {
        ResolveDefaultSpinnerPrefabInEditor();
        ResolveButtonReferences();
        EnsureSkipStepButtonExists();
        ConfigureHoldToConfirmButtons();
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    private void OnEnable()
    {
        SimulatorModeManager.OnModeChanged += HandleModeChanged;
        ActionOrderManager.OnAllStepsCompleted += HandleAllStepsCompleted;

        if (wireButtonListenersOnEnable)
            WireButtonListeners();

        ConfigureHoldToConfirmButtons();
    }

    private void OnDisable()
    {
        SimulatorModeManager.OnModeChanged -= HandleModeChanged;
        ActionOrderManager.OnAllStepsCompleted -= HandleAllStepsCompleted;

        if (wireButtonListenersOnEnable)
            UnwireButtonListeners();
    }

    private void Start()
    {
        if (showMainMenuOnStart)
            ShowMainMenu();

        RefreshReturnToMenuButtonVisibility();
        RefreshSkipStepButtonVisibility();
    }

    public void ShowMainMenu()
    {
        SetReturnToMenuButtonVisible(false);
        SetSkipStepButtonVisible(false);
        SetPanels(mainMenu: true);
    }

    [ContextMenu("Test/Show Complete Panel")]
    public void ShowCompletePanel()
    {
        SetReturnToMenuButtonVisible(false);
        SetSkipStepButtonVisible(false);
        SetPanels(complete: true);
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
        ReturnToMainMenu();
    }

    [ContextMenu("Test/Return To Main Menu")]
    public void ReturnToMainMenu()
    {
        if (modeManager != null)
        {
            modeManager.ShowMenu();
            return;
        }

        Debug.LogWarning("Cannot return through SimulatorModeManager because it is not assigned. Showing the menu panels directly.");
        SetReturnToMenuButtonVisible(false);
        SetSkipStepButtonVisible(false);
        ShowMainMenu();
    }

    public void RefreshReturnToMenuButtonVisibility()
    {
        if (modeManager != null)
            SetReturnToMenuButtonVisible(IsInModePanelVisible(modeManager.CurrentMode));
        else
            SetReturnToMenuButtonVisible(false);
    }

    public void RefreshSkipStepButtonVisibility()
    {
        if (modeManager != null)
            SetSkipStepButtonVisible(IsSkipStepMode(modeManager.CurrentMode));
        else
            SetSkipStepButtonVisible(false);
    }

    public void SkipCurrentStep()
    {
        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);

        if (actionOrderManager == null)
        {
            Debug.LogWarning("Cannot skip the current step because no ActionOrderManager was found.");
            return;
        }

        if (modeManager == null)
            modeManager = FindAnyObjectByType<SimulatorModeManager>(FindObjectsInactive.Include);

        SimulatorMode currentMode = modeManager != null ? modeManager.CurrentMode : SimulatorMode.Menu;
        if (currentMode == SimulatorMode.Exam)
        {
            if (modeManager == null || !modeManager.IsExamSkippable)
            {
                Debug.LogWarning("Cannot skip the current exam step because exam skip is disabled in SimulatorModeManager.");
                return;
            }

            if (!actionOrderManager.CanSkipCurrentStep())
            {
                actionOrderManager.SkipCurrentStep();
                return;
            }

            float penaltySeconds = modeManager.ExamSkipPenaltySeconds;
            if (penaltySeconds > 0f)
            {
                if (examManager == null)
                    examManager = FindAnyObjectByType<ExamManager>(FindObjectsInactive.Include);

                if (examManager == null)
                {
                    Debug.LogWarning("Cannot skip the current exam step because no ExamManager was found to apply the time penalty.");
                    return;
                }

                if (!examManager.ApplyTimePenalty(penaltySeconds))
                    return;
            }
        }

        actionOrderManager.SkipCurrentStep();
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

        if (returnToMenuButton == null)
            returnToMenuButton = FindButtonInPanel(returnToMenuButtonRoot, "ReturnToMenuButton", "Return To Menu", "MainMenuButton", "MenuButton", "BackButton", "Back");

        if (returnToMenuButtonRoot == null && returnToMenuButton != null)
            returnToMenuButtonRoot = returnToMenuButton.gameObject;

        if (skipStepButton == null)
        {
            GameObject skipSearchRoot = skipStepButtonRoot != null ? skipStepButtonRoot : returnToMenuButtonRoot;
            skipStepButton = FindButtonInPanel(skipSearchRoot, "SkipStepButton", "Skip Step", "SkipButton", "NextStepButton", "NextButton");
        }

        if (skipStepButtonRoot == null && skipStepButton != null)
            skipStepButtonRoot = skipStepButton.gameObject;
    }

    [ContextMenu("Setup/Create Skip Step Button If Missing")]
    public void EnsureSkipStepButtonExists()
    {
        if (skipStepButton != null)
            return;

        if (returnToMenuButtonRoot == null || returnToMenuButton == null)
            return;

        GameObject buttonObject = new GameObject("SkipStepButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(returnToMenuButtonRoot.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        RectTransform sourceRect = returnToMenuButton.GetComponent<RectTransform>();
        if (sourceRect != null)
        {
            buttonRect.anchorMin = sourceRect.anchorMin;
            buttonRect.anchorMax = sourceRect.anchorMax;
            buttonRect.pivot = sourceRect.pivot;
            buttonRect.sizeDelta = sourceRect.sizeDelta;
            float verticalOffset = sourceRect.sizeDelta.y + 20f;
            buttonRect.anchoredPosition = sourceRect.anchoredPosition + Vector2.up * verticalOffset;
        }
        else
        {
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(220f, 67f);
            buttonRect.anchoredPosition = new Vector2(0f, 145f);
        }

        Image buttonImage = buttonObject.GetComponent<Image>();
        Image sourceImage = returnToMenuButton.targetGraphic as Image;
        if (sourceImage != null)
        {
            buttonImage.sprite = sourceImage.sprite;
            buttonImage.type = sourceImage.type;
            buttonImage.preserveAspect = sourceImage.preserveAspect;
            buttonImage.fillCenter = sourceImage.fillCenter;
            buttonImage.color = sourceImage.color;
            buttonImage.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
        }
        else
        {
            buttonImage.color = new Color(0.235f, 0.408f, 0.424f, 1f);
        }

        Button button = buttonObject.GetComponent<Button>();
        button.transition = returnToMenuButton.transition;
        button.colors = returnToMenuButton.colors;
        button.spriteState = returnToMenuButton.spriteState;
        button.animationTriggers = returnToMenuButton.animationTriggers;
        button.navigation = returnToMenuButton.navigation;
        button.targetGraphic = buttonImage;

        TextMeshProUGUI sourceText = returnToMenuButton.GetComponentInChildren<TextMeshProUGUI>(true);
        GameObject textObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = "Skip step";
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = true;
        label.color = sourceText != null ? sourceText.color : Color.white;
        label.fontSize = sourceText != null ? sourceText.fontSize : 25f;
        label.enableAutoSizing = sourceText != null && sourceText.enableAutoSizing;
        label.fontSizeMin = sourceText != null ? sourceText.fontSizeMin : 18f;
        label.fontSizeMax = sourceText != null ? sourceText.fontSizeMax : 36f;

        if (sourceText != null)
        {
            label.font = sourceText.font;
            label.fontStyle = sourceText.fontStyle;
        }

        skipStepButtonRoot = buttonObject;
        skipStepButton = button;
        SetSkipStepButtonVisible(false);
        ConfigureHoldToConfirmButton(skipStepButton);

        if (Application.isPlaying)
            WireButton(skipStepButton, SkipCurrentStep);
    }

    [ContextMenu("Setup/Wire Button Listeners")]
    public void WireButtonListeners()
    {
        ResolveButtonReferences();
        ConfigureHoldToConfirmButtons();

        WireButton(learningModeButton, ShowLearningPanel);
        WireButton(trainingModeButton, ShowTrainingPanel);
        WireButton(examModeButton, ShowExamPanel);

        WireButton(learningBackButton, ShowMainMenu);
        WireButton(learningStartButton, StartLearningMode);

        WireButton(trainingBackButton, ShowMainMenu);
        WireButton(trainingStartButton, StartTrainingMode);

        WireButton(examBackButton, ShowMainMenu);
        WireButton(examStartButton, StartExamMode);

        WireButton(returnToMenuButton, ReturnToMainMenu);
        WireButton(skipStepButton, SkipCurrentStep);
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

        UnwireButton(returnToMenuButton, ReturnToMainMenu);
        UnwireButton(skipStepButton, SkipCurrentStep);
    }

    private void HandleModeChanged(SimulatorMode mode)
    {
        SetReturnToMenuButtonVisible(IsInModePanelVisible(mode));
        SetSkipStepButtonVisible(IsSkipStepMode(mode));

        if (mode == SimulatorMode.Menu)
            ShowMainMenu();
        else
            HideModeSelectionPanels();
    }

    private void HandleAllStepsCompleted()
    {
        ShowCompletePanel();
    }

    private void HideModeSelectionPanels()
    {
        SetPanels();
    }

    private void SetPanels(bool mainMenu = false, bool learning = false, bool training = false, bool exam = false, bool complete = false, bool examFailed = false)
    {
        SetActive(mainMenuPanel, mainMenu);
        SetActive(learningModePanel, learning);
        SetActive(trainingModePanel, training);
        SetActive(examModePanel, exam);
        SetActive(completePanel, complete);
        SetActive(examFailedPanel, examFailed);
    }

    private void SetReturnToMenuButtonVisible(bool visible)
    {
        SetActive(returnToMenuButtonRoot, visible);
    }

    private void SetSkipStepButtonVisible(bool visible)
    {
        SetActive(skipStepButtonRoot, visible);
    }

    private bool IsSkipStepMode(SimulatorMode mode)
    {
        if (modeManager != null)
            return modeManager.IsStepSkipAllowedInMode(mode);

        return mode == SimulatorMode.Learning || mode == SimulatorMode.Training;
    }

    private bool IsInModePanelVisible(SimulatorMode mode)
    {
        if (mode == SimulatorMode.Menu)
            return false;

        if (mode == SimulatorMode.Exam && modeManager != null)
            return modeManager.IsExamSkippable;

        return true;
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

    private void ConfigureHoldToConfirmButtons()
    {
        HashSet<Button> buttons = new HashSet<Button>();

        AddButton(buttons, learningModeButton);
        AddButton(buttons, trainingModeButton);
        AddButton(buttons, examModeButton);
        AddButton(buttons, learningBackButton);
        AddButton(buttons, learningStartButton);
        AddButton(buttons, trainingBackButton);
        AddButton(buttons, trainingStartButton);
        AddButton(buttons, examBackButton);
        AddButton(buttons, examStartButton);
        AddButton(buttons, returnToMenuButton);
        AddButton(buttons, skipStepButton);

        AddButtonsFromRoot(buttons, mainMenuPanel);
        AddButtonsFromRoot(buttons, learningModePanel);
        AddButtonsFromRoot(buttons, trainingModePanel);
        AddButtonsFromRoot(buttons, examModePanel);
        AddButtonsFromRoot(buttons, completePanel);
        AddButtonsFromRoot(buttons, examFailedPanel);
        AddButtonsFromRoot(buttons, returnToMenuButtonRoot);
        AddButtonsFromRoot(buttons, skipStepButtonRoot);

        foreach (Button button in buttons)
            ConfigureHoldToConfirmButton(button);
    }

    private void ConfigureHoldToConfirmButton(Button button)
    {
        if (button == null)
            return;

        HoldToConfirmButton holdButton = button.GetComponent<HoldToConfirmButton>();

        if (!requireHoldToConfirm)
        {
            if (holdButton != null)
                holdButton.enabled = false;

            return;
        }

        if (holdButton == null)
            holdButton = button.gameObject.AddComponent<HoldToConfirmButton>();

        holdButton.enabled = true;
        holdButton.Configure(button, holdToConfirmSeconds, holdSpinnerPrefab, holdSpinnerSize, holdSpinnerAnchoredOffset);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(holdButton);
            EditorUtility.SetDirty(button.gameObject);
            EditorSceneManager.MarkSceneDirty(button.gameObject.scene);
        }
#endif
    }

    private void AddButton(HashSet<Button> buttons, Button button)
    {
        if (button != null)
            buttons.Add(button);
    }

    private void AddButtonsFromRoot(HashSet<Button> buttons, GameObject root)
    {
        if (root == null)
            return;

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            AddButton(buttons, button);
    }
}
