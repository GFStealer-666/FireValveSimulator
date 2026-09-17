namespace FireValveSimulator.Statistics.UI
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public sealed class StatisticsOverviewRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private TMP_Text stepNameLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private Image fillImage;

        public void Bind(int rank, string stepName, string value, float normalizedFill)
        {
            if (rankLabel != null)
                rankLabel.text = rank.ToString();
            if (stepNameLabel != null)
                stepNameLabel.text = stepName;
            if (valueLabel != null)
                valueLabel.text = value;
            if (fillImage != null)
                fillImage.fillAmount = Mathf.Clamp01(normalizedFill);
        }
    }
}
