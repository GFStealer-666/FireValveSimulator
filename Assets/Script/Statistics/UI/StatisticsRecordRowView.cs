namespace FireValveSimulator.Statistics.UI
{
    using System;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class StatisticsRecordRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text userNameLabel;
        [SerializeField] private TMP_Text dateLabel;
        [SerializeField] private TMP_Text durationLabel;
        [SerializeField] private TMP_Text outcomeLabel;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private GameObject completedIndicator;
        [SerializeField] private GameObject failedIndicator;
        [SerializeField] private GameObject abandonedIndicator;

        private Action clickAction;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
                button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(ExamSessionStatistics session, string formattedDate, string formattedDuration, Action onClick)
        {
            clickAction = onClick;

            if (userNameLabel != null)
                userNameLabel.text = session?.userName ?? string.Empty;
            if (dateLabel != null)
                dateLabel.text = formattedDate;
            if (durationLabel != null)
                durationLabel.text = formattedDuration;
            if (outcomeLabel != null)
                outcomeLabel.text = session != null ? FormatOutcome(session.outcome) : string.Empty;

            SetActive(completedIndicator, session?.outcome == ExamSessionOutcome.Completed);
            SetActive(failedIndicator, session?.outcome == ExamSessionOutcome.Failed);
            SetActive(abandonedIndicator, session?.outcome == ExamSessionOutcome.Abandoned);
        }

        public void SetSelected(bool selected)
        {
            SetActive(selectedIndicator, selected);
        }

        private void HandleClicked()
        {
            clickAction?.Invoke();
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

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
