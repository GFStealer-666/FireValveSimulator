namespace FireValveSimulator.Statistics.UI
{
    using TMPro;
    using UnityEngine;

    public sealed class StatisticsStepRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text stepNumberLabel;
        [SerializeField] private TMP_Text stepNameLabel;
        [SerializeField] private TMP_Text timeLabel;
        [SerializeField] private TMP_Text correctLabel;
        [SerializeField] private TMP_Text wrongLabel;
        [SerializeField] private TMP_Text outcomeLabel;
        [SerializeField] private GameObject correctIndicator;
        [SerializeField] private GameObject mistakeIndicator;
        [SerializeField] private GameObject incompleteIndicator;
        [SerializeField] private GameObject skippedIndicator;

        public void Bind(ExamStepStatistics step, string formattedDuration)
        {
            if (stepNumberLabel != null)
                stepNumberLabel.text = step != null ? (step.stepIndex + 1).ToString() : string.Empty;
            if (stepNameLabel != null)
                stepNameLabel.text = step?.stepName ?? string.Empty;
            if (timeLabel != null)
                timeLabel.text = formattedDuration;
            if (correctLabel != null)
                correctLabel.text = step?.correctCount.ToString() ?? "0";
            if (wrongLabel != null)
                wrongLabel.text = step?.wrongCount.ToString() ?? "0";
            if (outcomeLabel != null)
                outcomeLabel.text = step != null ? FormatOutcome(step.outcome) : string.Empty;

            SetActive(correctIndicator, step?.outcome == ExamStepOutcome.Correct);
            SetActive(mistakeIndicator, step?.outcome == ExamStepOutcome.CompletedAfterMistake);
            SetActive(incompleteIndicator,
                step?.outcome == ExamStepOutcome.Incomplete || step?.outcome == ExamStepOutcome.NotAttempted);
            SetActive(skippedIndicator, step?.outcome == ExamStepOutcome.Skipped);
        }

        private static string FormatOutcome(ExamStepOutcome outcome)
        {
            return outcome switch
            {
                ExamStepOutcome.Correct => "Correct",
                ExamStepOutcome.CompletedAfterMistake => "Completed after mistake",
                ExamStepOutcome.Incomplete => "Incomplete",
                ExamStepOutcome.Skipped => "Skipped",
                _ => "Not attempted"
            };
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
