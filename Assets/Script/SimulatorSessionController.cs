using UnityEngine;

public class SimulatorSessionController : MonoBehaviour
{
    [SerializeField] private SimulatorModeManager modeManager;
    [SerializeField] private PlayerPoseResetter playerPoseResetter;
    [SerializeField] private SceneStateResetter sceneStateResetter;
    [SerializeField] private ActionOrderManager actionOrderManager;
    [SerializeField] private ExamManager examManager;
    [SerializeField] private WaitTimer waitTimer;
    [SerializeField] private PressureSimulator pressureSimulator;
    [SerializeField] private bool startStepHelpersInGuidedModes = true;
    [SerializeField] private bool startSequenceWithoutExamManager = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        SimulatorModeManager.OnModeChanged += HandleModeChanged;
    }

    private void OnDisable()
    {
        SimulatorModeManager.OnModeChanged -= HandleModeChanged;
    }

    public void RestartCurrentMode()
    {
        ResolveReferences();

        if (modeManager != null)
            HandleModeChanged(modeManager.CurrentMode);
    }

    public void ResetSessionState()
    {
        ResolveReferences();

        if (examManager != null)
            examManager.StopExam();

        StopStepHelpers();

        if (actionOrderManager != null)
        {
            actionOrderManager.SetExamMode(false);
            actionOrderManager.ResetToIdle();
        }

        if (sceneStateResetter != null)
            sceneStateResetter.ResetState();

        if (playerPoseResetter != null)
            playerPoseResetter.ResetPlayerPose();
    }

    private void HandleModeChanged(SimulatorMode mode)
    {
        ResetSessionState();

        if (mode == SimulatorMode.Menu)
            return;

        if (mode == SimulatorMode.Exam)
        {
            StartExamSession();
            return;
        }

        StartGuidedSession();
    }

    private void StartGuidedSession()
    {
        if (actionOrderManager != null)
        {
            actionOrderManager.SetExamMode(false);
            actionOrderManager.ResetSequence();
        }

        if (startStepHelpersInGuidedModes)
            StartStepHelpers();
    }

    private void StartExamSession()
    {
        if (examManager != null)
        {
            examManager.StartExam();
            return;
        }

        if (!startSequenceWithoutExamManager || actionOrderManager == null)
            return;

        actionOrderManager.SetExamMode(true);
        actionOrderManager.ResetSequence();
        StartStepHelpers();
    }

    private void StartStepHelpers()
    {
        if (waitTimer != null)
            waitTimer.isActive = true;

        if (pressureSimulator != null)
            pressureSimulator.isActive = true;
    }

    private void StopStepHelpers()
    {
        if (waitTimer != null)
        {
            waitTimer.isActive = false;
            waitTimer.ResetTimer();
        }

        if (pressureSimulator != null)
        {
            pressureSimulator.isActive = false;
            pressureSimulator.ResetPressure();
        }
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
            modeManager = FindAnyObjectByType<SimulatorModeManager>(FindObjectsInactive.Include);

        if (playerPoseResetter == null)
            playerPoseResetter = FindAnyObjectByType<PlayerPoseResetter>(FindObjectsInactive.Include);

        if (sceneStateResetter == null)
            sceneStateResetter = FindAnyObjectByType<SceneStateResetter>(FindObjectsInactive.Include);

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);

        if (examManager == null)
            examManager = FindAnyObjectByType<ExamManager>(FindObjectsInactive.Include);

        if (waitTimer == null)
            waitTimer = FindAnyObjectByType<WaitTimer>(FindObjectsInactive.Include);

        if (pressureSimulator == null)
            pressureSimulator = FindAnyObjectByType<PressureSimulator>(FindObjectsInactive.Include);
    }
}
