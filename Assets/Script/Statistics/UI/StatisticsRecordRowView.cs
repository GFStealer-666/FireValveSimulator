namespace FireValveSimulator.Statistics.UI
{
    using System;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class StatisticsRecordRowView : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private TMP_Text userNameLabel;
        [SerializeField] private TMP_Text dateLabel;
        [SerializeField] private TMP_Text durationLabel;
        [SerializeField] private GameObject completedIndicator;
        [SerializeField] private GameObject failedIndicator;
        [SerializeField] private GameObject abandonedIndicator;

        private Action clickAction;

        private void Awake()
        {
            if (toggle == null)
                toggle = GetComponent<Toggle>();

            if (toggle != null)
                toggle.onValueChanged.AddListener(HandleToggleChanged);
        }

        private void OnDestroy()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(HandleToggleChanged);
        }

        public void SetGroup(ToggleGroup group)
        {
            if (toggle != null)
                toggle.group = group;
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
            SetActive(completedIndicator, session?.outcome == ExamSessionOutcome.Completed);
            SetActive(failedIndicator, session?.outcome == ExamSessionOutcome.Failed);
            SetActive(abandonedIndicator, session?.outcome == ExamSessionOutcome.Abandoned);
        }

        public void SetSelected(bool selected)
        {
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(selected);
        }

        private void HandleToggleChanged(bool isSelected)
        {
            if (!isSelected && toggle != null && toggle.group == null)
            {
                toggle.SetIsOnWithoutNotify(true);
                return;
            }

            if (isSelected)
                clickAction?.Invoke();
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
