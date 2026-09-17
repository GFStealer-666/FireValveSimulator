namespace FireValveSimulator
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.Events;

    public class ExamManager : MonoBehaviour
    {
        public float examDuration = 300f;
        [SerializeField] private float remainingTimeFloat;
        [SerializeField] private float currentTimeFloat = 0f;
        private bool examRunning = false;
        [SerializeField] private bool isExamTimeLimited = true;
        [SerializeField] private TextMeshProUGUI countdownLabel;
        [SerializeField] private TextMeshProUGUI currentTimeLabel;
        private ActionOrderManager actionOrderManager;

        public UnityEvent onExamStart;
        public UnityEvent onExamFailed;
        public UnityEvent onExamFinish;
        public UnityEvent onResetExam;
        public WaitTimer waitTimer;
        public PressureSimulator pressureSimulator;

        private void Awake()
        {
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
        }

        private void OnEnable()
        {
            ActionOrderManager.OnAllStepsCompleted += HandleExamCompleted;
        }

        private void OnDisable()
        {
            ActionOrderManager.OnAllStepsCompleted -= HandleExamCompleted;
        }

        private void Start()
        {
            LockSystem();
        }

        private void Update()
        {
            if (!examRunning)
                return;

            remainingTimeFloat -= Time.deltaTime;
            currentTimeFloat += Time.deltaTime;

            if (countdownLabel != null)
                countdownLabel.text = FormatRemainingTime(remainingTimeFloat);

            if (currentTimeLabel != null)
                currentTimeLabel.text = FormatCurrentTime(currentTimeFloat);

            if (remainingTimeFloat <= 0f && isExamTimeLimited)
                FailedExam();
        }

        public void StartExam()
        {
            if (actionOrderManager == null)
                actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

            if (actionOrderManager == null)
            {
                Debug.LogWarning("Exam cannot start because no ActionOrderManager was found.");
                return;
            }

            Debug.Log("Exam Started!");
            ResetStepHelpers();

            actionOrderManager.SetExamMode(true);
            actionOrderManager.ResetSequence();

            examRunning = true;
            remainingTimeFloat = examDuration;
            currentTimeFloat = 0f;

            if (countdownLabel != null)
                countdownLabel.text = FormatRemainingTime(remainingTimeFloat);

            if (currentTimeLabel != null)
                currentTimeLabel.text = FormatCurrentTime(currentTimeFloat);

            onExamStart?.Invoke();
            UnlockSystem();

            if (waitTimer != null)
                waitTimer.isActive = true;

            if (pressureSimulator != null)
                pressureSimulator.isActive = true;
        }

        public void LockSystem()
        {
        }

        public void UnlockSystem()
        {
            foreach (UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(FindObjectsSortMode.None))
                interactable.enabled = true;
        }

        public void ResetExam()
        {
            examRunning = false;
            remainingTimeFloat = examDuration;
            currentTimeFloat = 0f;

            if (countdownLabel != null)
                countdownLabel.text = FormatRemainingTime(remainingTimeFloat);

            if (currentTimeLabel != null)
                currentTimeLabel.text = FormatCurrentTime(currentTimeFloat);

            ResetStepHelpers();

            if (actionOrderManager == null)
                actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

            if (actionOrderManager != null)
            {
                actionOrderManager.ResetSequence();
                actionOrderManager.SetExamMode(false);
            }

            LockSystem();
            onResetExam?.Invoke();
        }

        public void StopExam()
        {
            examRunning = false;
            ResetStepHelpers();

            if (countdownLabel != null)
                countdownLabel.text = "";

            if (currentTimeLabel != null)
                currentTimeLabel.text = "";
        }

        public bool ApplyTimePenalty(float penaltySeconds)
        {
            if (!examRunning)
            {
                Debug.LogWarning("Cannot apply exam time penalty because the exam is not running.");
                return false;
            }

            float penalty = Mathf.Max(0f, penaltySeconds);
            if (penalty <= 0f)
                return true;

            remainingTimeFloat = Mathf.Max(0f, remainingTimeFloat - penalty);

            if (countdownLabel != null)
                countdownLabel.text = FormatRemainingTime(remainingTimeFloat);

            Debug.Log($"Exam time penalty applied: -{penalty:0.#} seconds.");

            if (remainingTimeFloat <= 0f)
            {
                FailedExam();
                return false;
            }

            return true;
        }

        private void FailedExam()
        {
            if (!examRunning)
                return;

            Debug.Log("Time's up. Exam over.");
            examRunning = false;
            ResetStepHelpers();
            onExamFailed?.Invoke();

            if (actionOrderManager != null)
                actionOrderManager.TriggerFailure();
        }

        private void HandleExamCompleted()
        {
            Debug.Log("Exam completed successfully.");
            examRunning = false;
            ResetStepHelpers();
            onExamFinish?.Invoke();
        }

        private void ResetStepHelpers()
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

        private string FormatRemainingTime(float timeSeconds)
        {
            float clampedTime = Mathf.Max(0f, timeSeconds);

            if (clampedTime >= 60f)
            {
                int totalSeconds = Mathf.CeilToInt(clampedTime);
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return $"{minutes:00}:{seconds:00}";
            }

            int totalCentiseconds = Mathf.Clamp(Mathf.CeilToInt(clampedTime * 100f), 0, 5999);
            int wholeSeconds = totalCentiseconds / 100;
            int centiseconds = totalCentiseconds % 100;
            return $"{wholeSeconds:00}:{centiseconds:00}";
        }

        private string FormatCurrentTime(float timeSeconds)
        {
            int totalSeconds = Mathf.FloorToInt(timeSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
