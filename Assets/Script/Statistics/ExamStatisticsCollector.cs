namespace FireValveSimulator.Statistics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using UnityEngine;

    [DefaultExecutionOrder(100)]
    public sealed class ExamStatisticsCollector : MonoBehaviour
    {
        [SerializeField] private OfflineStatisticsRepository repository;
        [SerializeField] private ActionOrderManager actionOrderManager;
        [SerializeField] private ExamManager examManager;
        [SerializeField] private SimulatorModeManager modeManager;

        private ExamSessionStatistics activeSession;
        private ExamStepStatistics activeStep;
        private double sessionStartedAt;
        private double stepStartedAt;

        public event Action<ExamSessionStatistics> SessionSaved;

        public bool HasActiveSession => activeSession != null;
        public ExamSessionStatistics ActiveSession => activeSession;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            ActionOrderManager.OnCurrentStepChanged += HandleCurrentStepChanged;
            ActionOrderManager.OnStepFinished += HandleStepFinished;
            ActionOrderManager.OnStepFailed += HandleStepFailed;
            SimulatorModeManager.OnModeChanged += HandleModeChanged;

            if (examManager != null)
            {
                examManager.onExamStart.AddListener(HandleExamStarted);
                examManager.onExamFinish.AddListener(HandleExamCompleted);
                examManager.onExamFailed.AddListener(HandleExamFailed);
            }
        }

        private void OnDisable()
        {
            ActionOrderManager.OnCurrentStepChanged -= HandleCurrentStepChanged;
            ActionOrderManager.OnStepFinished -= HandleStepFinished;
            ActionOrderManager.OnStepFailed -= HandleStepFailed;
            SimulatorModeManager.OnModeChanged -= HandleModeChanged;

            if (examManager != null)
            {
                examManager.onExamStart.RemoveListener(HandleExamStarted);
                examManager.onExamFinish.RemoveListener(HandleExamCompleted);
                examManager.onExamFailed.RemoveListener(HandleExamFailed);
            }

            FinalizeSession(ExamSessionOutcome.Abandoned);
        }

        private void HandleExamStarted()
        {
            if (repository == null)
            {
                Debug.LogError("Exam statistics cannot start because OfflineStatisticsRepository is not assigned.", this);
                return;
            }

            if (activeSession != null)
                FinalizeSession(ExamSessionOutcome.Abandoned);

            activeSession = new ExamSessionStatistics
            {
                sessionId = Guid.NewGuid().ToString("N"),
                userName = repository.ReserveNextUserName(),
                startedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                outcome = ExamSessionOutcome.Abandoned,
                steps = CreateStepRecords()
            };

            sessionStartedAt = Time.timeAsDouble;

            if (actionOrderManager != null)
                BeginStep(actionOrderManager.GetCurrentStep(), actionOrderManager.CurrentStepIndex);
        }

        private void HandleExamCompleted()
        {
            FinalizeSession(ExamSessionOutcome.Completed);
        }

        private void HandleExamFailed()
        {
            FinalizeSession(ExamSessionOutcome.Failed);
        }

        private void HandleModeChanged(SimulatorMode mode)
        {
            if (activeSession != null && mode != SimulatorMode.Exam)
                FinalizeSession(ExamSessionOutcome.Abandoned);
        }

        private void HandleCurrentStepChanged(ActionStep step, int stepIndex)
        {
            if (activeSession == null)
                return;

            if (activeStep != null)
                CloseActiveStep(false);

            if (step != null && stepIndex >= 0)
                BeginStep(step, stepIndex);
        }

        private void HandleStepFinished(ActionStep step, bool wasSkipped)
        {
            if (activeSession == null)
                return;

            int stepIndex = actionOrderManager != null ? actionOrderManager.CurrentStepIndex : -1;
            if (activeStep == null)
                BeginStep(step, stepIndex);

            CloseActiveStep(!wasSkipped, wasSkipped);
        }

        private void HandleStepFailed()
        {
            if (activeSession == null)
                return;

            if (activeStep == null && actionOrderManager != null)
                BeginStep(actionOrderManager.GetCurrentStep(), actionOrderManager.CurrentStepIndex);

            if (activeStep != null)
                activeStep.wrongCount++;
        }

        private void BeginStep(ActionStep step, int stepIndex)
        {
            if (activeSession == null || step == null || stepIndex < 0)
                return;

            activeStep = GetOrCreateStepRecord(step, stepIndex);
            if (activeStep.outcome == ExamStepOutcome.NotAttempted)
                activeStep.outcome = ExamStepOutcome.Incomplete;

            stepStartedAt = Time.timeAsDouble;
        }

        private void CloseActiveStep(bool completed, bool skipped = false)
        {
            if (activeStep == null)
                return;

            activeStep.timeSeconds += Mathf.Max(0f, (float)(Time.timeAsDouble - stepStartedAt));

            if (skipped)
            {
                activeStep.outcome = ExamStepOutcome.Skipped;
            }
            else if (completed)
            {
                activeStep.correctCount++;
                activeStep.outcome = activeStep.wrongCount > 0
                    ? ExamStepOutcome.CompletedAfterMistake
                    : ExamStepOutcome.Correct;
            }
            else if (activeStep.outcome == ExamStepOutcome.NotAttempted)
            {
                activeStep.outcome = ExamStepOutcome.Incomplete;
            }

            activeStep = null;
        }

        private void FinalizeSession(ExamSessionOutcome outcome)
        {
            if (activeSession == null)
                return;

            CloseActiveStep(false);

            activeSession.endedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            activeSession.totalTimeSeconds = Mathf.Max(0f, (float)(Time.timeAsDouble - sessionStartedAt));
            activeSession.outcome = outcome;
            RecalculateSessionTotals(activeSession);

            ExamSessionStatistics completedSession = activeSession;
            activeSession = null;

            if (repository != null)
                repository.AddSession(completedSession);

            SessionSaved?.Invoke(completedSession);
        }

        private List<ExamStepStatistics> CreateStepRecords()
        {
            List<ExamStepStatistics> records = new List<ExamStepStatistics>();
            if (actionOrderManager == null || actionOrderManager.orderedSteps == null)
                return records;

            for (int index = 0; index < actionOrderManager.orderedSteps.Count; index++)
            {
                ActionStep step = actionOrderManager.orderedSteps[index];
                if (step == null)
                    continue;

                records.Add(CreateStepRecord(step, index));
            }

            return records;
        }

        private ExamStepStatistics GetOrCreateStepRecord(ActionStep step, int stepIndex)
        {
            foreach (ExamStepStatistics record in activeSession.steps)
            {
                if (record != null && record.stepIndex == stepIndex)
                    return record;
            }

            ExamStepStatistics created = CreateStepRecord(step, stepIndex);
            activeSession.steps.Add(created);
            return created;
        }

        private static ExamStepStatistics CreateStepRecord(ActionStep step, int stepIndex)
        {
            return new ExamStepStatistics
            {
                stepId = step.GetStatisticsId(stepIndex),
                stepIndex = stepIndex,
                stepName = string.IsNullOrWhiteSpace(step.stepName) ? step.name : step.stepName,
                outcome = ExamStepOutcome.NotAttempted
            };
        }

        private static void RecalculateSessionTotals(ExamSessionStatistics session)
        {
            session.correctCount = 0;
            session.wrongCount = 0;

            if (session.steps == null)
                return;

            foreach (ExamStepStatistics step in session.steps)
            {
                if (step == null)
                    continue;

                session.correctCount += Mathf.Max(0, step.correctCount);
                session.wrongCount += Mathf.Max(0, step.wrongCount);
            }
        }

        private void ResolveReferences()
        {
            if (repository == null)
                repository = FindAnyObjectByType<OfflineStatisticsRepository>(FindObjectsInactive.Include);

            if (actionOrderManager == null)
                actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);

            if (examManager == null)
                examManager = FindAnyObjectByType<ExamManager>(FindObjectsInactive.Include);

            if (modeManager == null)
                modeManager = FindAnyObjectByType<SimulatorModeManager>(FindObjectsInactive.Include);
        }
    }
}
