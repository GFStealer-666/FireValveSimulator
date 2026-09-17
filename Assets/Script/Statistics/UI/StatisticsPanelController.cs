namespace FireValveSimulator.Statistics.UI
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class StatisticsPanelController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private OfflineStatisticsRepository repository;

        [Header("Panel and Tabs")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject personalView;
        [SerializeField] private GameObject overviewView;
        [SerializeField] private Button personalTabButton;
        [SerializeField] private Button overviewTabButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject personalTabSelectedIndicator;
        [SerializeField] private GameObject overviewTabSelectedIndicator;

        [Header("Record List")]
        [SerializeField] private Transform recordContent;
        [SerializeField] private StatisticsRecordRowView recordRowPrefab;
        [SerializeField] private TMP_Text capacityLabel;
        [SerializeField] private GameObject noRecordsMessage;

        [Header("Selected Record")]
        [SerializeField] private GameObject selectedRecordRoot;
        [SerializeField] private TMP_Text selectedUserLabel;
        [SerializeField] private TMP_Text selectedDateAndOutcomeLabel;
        [SerializeField] private TMP_Text selectedDurationLabel;
        [SerializeField] private TMP_Text selectedCorrectLabel;
        [SerializeField] private TMP_Text selectedWrongLabel;
        [SerializeField] private Transform stepContent;
        [SerializeField] private StatisticsStepRowView stepRowPrefab;

        [Header("Overview")]
        [SerializeField, Min(1)] private int overviewRowLimit = 3;
        [SerializeField] private TMP_Text overviewSessionCountLabel;
        [SerializeField] private Transform mostWrongContent;
        [SerializeField] private StatisticsOverviewRowView mostWrongRowPrefab;
        [SerializeField] private GameObject noWrongDataMessage;
        [SerializeField] private Transform mostTimeContent;
        [SerializeField] private StatisticsOverviewRowView mostTimeRowPrefab;
        [SerializeField] private GameObject noTimeDataMessage;

        private readonly List<StatisticsRecordRowView> recordRows = new List<StatisticsRecordRowView>();
        private readonly List<StatisticsStepRowView> stepRows = new List<StatisticsStepRowView>();
        private readonly List<StatisticsOverviewRowView> wrongRows = new List<StatisticsOverviewRowView>();
        private readonly List<StatisticsOverviewRowView> timeRows = new List<StatisticsOverviewRowView>();

        private string selectedSessionId;
        private bool showingOverview;
        private bool listenersWired;

        private void Awake()
        {
            ResolveRepository();
            WireButtons();

            if (panelRoot == null)
                panelRoot = gameObject;
        }

        private void OnEnable()
        {
            ResolveRepository();
            if (repository != null)
                repository.DataChanged += HandleDataChanged;

            RefreshAll();
        }

        private void OnDisable()
        {
            if (repository != null)
                repository.DataChanged -= HandleDataChanged;
        }

        private void OnDestroy()
        {
            UnwireButtons();
        }

        private void OnValidate()
        {
            overviewRowLimit = Mathf.Max(1, overviewRowLimit);
        }

        public void Open()
        {
            showingOverview = false;

            if (panelRoot != null && !panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
                return;
            }

            ShowPersonal();
            RefreshAll();
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        public void ShowPersonal()
        {
            showingOverview = false;
            RefreshTabVisibility();
        }

        public void ShowOverview()
        {
            showingOverview = true;
            RefreshTabVisibility();
            RefreshOverview();
        }

        [ContextMenu("Statistics/Refresh Panel")]
        public void RefreshAll()
        {
            ResolveRepository();
            RefreshTabVisibility();

            if (repository == null)
            {
                HideAllRows();
                return;
            }

            RefreshRecords();
            RefreshOverview();
        }

        private void RefreshRecords()
        {
            IReadOnlyList<ExamSessionStatistics> sessions = repository.Sessions;

            if (capacityLabel != null)
                capacityLabel.text = $"{sessions.Count} of {repository.MaximumRecords} offline records saved";

            SetActive(noRecordsMessage, sessions.Count == 0);
            SetActive(selectedRecordRoot, sessions.Count > 0);

            if (sessions.Count == 0)
            {
                selectedSessionId = null;
                DeactivateRows(recordRows, 0);
                DeactivateRows(stepRows, 0);
                return;
            }

            if (string.IsNullOrEmpty(selectedSessionId) || !sessions.Any(session => session.sessionId == selectedSessionId))
                selectedSessionId = sessions[sessions.Count - 1].sessionId;

            int rowIndex = 0;
            for (int index = sessions.Count - 1; index >= 0; index--)
            {
                ExamSessionStatistics session = sessions[index];
                StatisticsRecordRowView row = GetOrCreateRow(recordRows, recordRowPrefab, recordContent, rowIndex);
                if (row == null)
                    break;

                string sessionId = session.sessionId;
                row.Bind(
                    session,
                    FormatDate(session.startedAtUtc),
                    FormatDuration(session.totalTimeSeconds),
                    () => SelectSession(sessionId));
                row.SetSelected(session.sessionId == selectedSessionId);
                rowIndex++;
            }

            DeactivateRows(recordRows, rowIndex);
            SelectSession(selectedSessionId, false);
        }

        private void SelectSession(string sessionId, bool updateRecordSelection = true)
        {
            selectedSessionId = sessionId;
            ExamSessionStatistics selected = repository?.Sessions
                .FirstOrDefault(session => session != null && session.sessionId == selectedSessionId);

            if (selected == null)
            {
                SetActive(selectedRecordRoot, false);
                DeactivateRows(stepRows, 0);
                return;
            }

            SetActive(selectedRecordRoot, true);

            if (selectedUserLabel != null)
                selectedUserLabel.text = selected.userName;
            if (selectedDateAndOutcomeLabel != null)
                selectedDateAndOutcomeLabel.text = $"{FormatDate(selected.startedAtUtc)} · {FormatOutcome(selected.outcome)}";
            if (selectedDurationLabel != null)
                selectedDurationLabel.text = FormatDuration(selected.totalTimeSeconds);
            if (selectedCorrectLabel != null)
                selectedCorrectLabel.text = selected.correctCount.ToString();
            if (selectedWrongLabel != null)
                selectedWrongLabel.text = selected.wrongCount.ToString();

            if (updateRecordSelection)
            {
                IReadOnlyList<ExamSessionStatistics> sessions = repository.Sessions;
                int visibleIndex = 0;
                for (int index = sessions.Count - 1; index >= 0 && visibleIndex < recordRows.Count; index--)
                {
                    recordRows[visibleIndex].SetSelected(sessions[index].sessionId == selectedSessionId);
                    visibleIndex++;
                }
            }

            int rowIndex = 0;
            IEnumerable<ExamStepStatistics> orderedSteps = selected.steps?
                .Where(step => step != null)
                .OrderBy(step => step.stepIndex) ?? Enumerable.Empty<ExamStepStatistics>();

            foreach (ExamStepStatistics step in orderedSteps)
            {
                StatisticsStepRowView row = GetOrCreateRow(stepRows, stepRowPrefab, stepContent, rowIndex);
                if (row == null)
                    break;

                row.Bind(step, FormatDuration(step.timeSeconds));
                rowIndex++;
            }

            DeactivateRows(stepRows, rowIndex);
        }

        private void RefreshOverview()
        {
            if (repository == null)
                return;

            StatisticsOverview overview = ExamStatisticsUtility.BuildOverview(repository.Sessions);
            if (overviewSessionCountLabel != null)
                overviewSessionCountLabel.text = $"All {overview.SessionCount} saved exam records";

            int wrongCount = PopulateWrongRows(overview.MostWrongSteps);
            int timeCount = PopulateTimeRows(overview.MostTimeUsedSteps);
            SetActive(noWrongDataMessage, wrongCount == 0);
            SetActive(noTimeDataMessage, timeCount == 0);
        }

        private int PopulateWrongRows(IReadOnlyList<StepAggregateStatistics> source)
        {
            List<StepAggregateStatistics> rows = source
                .Where(step => step.WrongCount > 0)
                .Take(overviewRowLimit)
                .ToList();

            float maximum = rows.Count > 0 ? rows[0].WrongCount : 0f;
            for (int index = 0; index < rows.Count; index++)
            {
                StatisticsOverviewRowView row = GetOrCreateRow(
                    wrongRows,
                    mostWrongRowPrefab,
                    mostWrongContent,
                    index);
                if (row == null)
                    return index;

                StepAggregateStatistics value = rows[index];
                row.Bind(index + 1, FormatStepName(value), value.WrongCount.ToString(), value.WrongCount / maximum);
            }

            DeactivateRows(wrongRows, rows.Count);
            return rows.Count;
        }

        private int PopulateTimeRows(IReadOnlyList<StepAggregateStatistics> source)
        {
            List<StepAggregateStatistics> rows = source
                .Where(step => step.AttemptCount > 0)
                .Take(overviewRowLimit)
                .ToList();

            float maximum = rows.Count > 0 ? rows[0].AverageTimeSeconds : 0f;
            for (int index = 0; index < rows.Count; index++)
            {
                StatisticsOverviewRowView row = GetOrCreateRow(
                    timeRows,
                    mostTimeRowPrefab,
                    mostTimeContent,
                    index);
                if (row == null)
                    return index;

                StepAggregateStatistics value = rows[index];
                float normalized = maximum > 0f ? value.AverageTimeSeconds / maximum : 0f;
                row.Bind(index + 1, FormatStepName(value), FormatDuration(value.AverageTimeSeconds), normalized);
            }

            DeactivateRows(timeRows, rows.Count);
            return rows.Count;
        }

        private void RefreshTabVisibility()
        {
            SetActive(personalView, !showingOverview);
            SetActive(overviewView, showingOverview);
            SetActive(personalTabSelectedIndicator, !showingOverview);
            SetActive(overviewTabSelectedIndicator, showingOverview);
        }

        private void HandleDataChanged()
        {
            if (panelRoot == null || panelRoot.activeInHierarchy)
                RefreshAll();
        }

        private void WireButtons()
        {
            if (listenersWired)
                return;

            if (personalTabButton != null)
                personalTabButton.onClick.AddListener(ShowPersonal);
            if (overviewTabButton != null)
                overviewTabButton.onClick.AddListener(ShowOverview);
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            listenersWired = true;
        }

        private void UnwireButtons()
        {
            if (!listenersWired)
                return;

            if (personalTabButton != null)
                personalTabButton.onClick.RemoveListener(ShowPersonal);
            if (overviewTabButton != null)
                overviewTabButton.onClick.RemoveListener(ShowOverview);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            listenersWired = false;
        }

        private void ResolveRepository()
        {
            if (repository == null)
                repository = FindAnyObjectByType<OfflineStatisticsRepository>(FindObjectsInactive.Include);
        }

        private void HideAllRows()
        {
            DeactivateRows(recordRows, 0);
            DeactivateRows(stepRows, 0);
            DeactivateRows(wrongRows, 0);
            DeactivateRows(timeRows, 0);
            SetActive(noRecordsMessage, true);
            SetActive(selectedRecordRoot, false);
        }

        private static T GetOrCreateRow<T>(List<T> pool, T prefab, Transform parent, int index) where T : Component
        {
            if (index < pool.Count)
            {
                T existing = pool[index];
                existing.gameObject.SetActive(true);
                return existing;
            }

            if (prefab == null || parent == null)
                return null;

            T created = Instantiate(prefab, parent);
            created.gameObject.SetActive(true);
            pool.Add(created);
            return created;
        }

        private static void DeactivateRows<T>(List<T> rows, int firstUnusedIndex) where T : Component
        {
            for (int index = Mathf.Max(0, firstUnusedIndex); index < rows.Count; index++)
            {
                if (rows[index] != null)
                    rows[index].gameObject.SetActive(false);
            }
        }

        private static string FormatDuration(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainingSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        private static string FormatDate(string utcDate)
        {
            if (DateTime.TryParse(
                    utcDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsed))
            {
                return parsed.ToLocalTime().ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
            }

            return utcDate ?? string.Empty;
        }

        private static string FormatOutcome(ExamSessionOutcome outcome)
        {
            return outcome switch
            {
                ExamSessionOutcome.Completed => "Completed",
                ExamSessionOutcome.Failed => "Failed",
                _ => "Abandoned"
            };
        }

        private static string FormatStepName(StepAggregateStatistics step)
        {
            return $"{step.StepIndex + 1} · {step.StepName}";
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
