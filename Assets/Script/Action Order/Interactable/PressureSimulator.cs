namespace FireValveSimulator
{
    using System.Collections;
    using TMPro;
    using UnityEngine;

    public class PressureSimulator : MonoBehaviour
    {
        public ActionOrderManager actionOrderManager;
        public TMP_Text pressureText;
        [Tooltip("{0} is replaced with the rounded pressure value.")]
        [SerializeField] private string pressureLabelFormat = "Pressure: {0} PSI";
        public float pressureIncreaseRate = 10f;

        private float currentPressure = 0f;
        private Coroutine pressureCoroutine;
        private bool hasLoggedInvalidLabelFormat;

        public bool isActive = false;

        private void Awake()
        {
            if (actionOrderManager == null)
                actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
        }

        private void Update()
        {
            if (!isActive || pressureCoroutine != null)
                return;

            if (actionOrderManager == null)
                actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

            if (actionOrderManager == null)
                return;

            ActionStep currentStep = actionOrderManager.GetCurrentStep();
            if (currentStep != null && currentStep.actionType == ActionType.CheckPSI)
                pressureCoroutine = StartCoroutine(PressureBuildUp(currentStep, currentStep.PressureTarget));
        }

        public void ResetPressure()
        {
            if (pressureCoroutine != null)
            {
                StopCoroutine(pressureCoroutine);
                pressureCoroutine = null;
            }

            currentPressure = 0f;
            if (pressureText != null)
                pressureText.text = "";
        }

        private IEnumerator PressureBuildUp(ActionStep step, float targetPressure)
        {
            while (currentPressure < targetPressure)
            {
                currentPressure += pressureIncreaseRate * Time.deltaTime;

                if (pressureText != null)
                    pressureText.text = FormatPressureLabel(currentPressure);

                yield return null;
            }

            if (pressureText != null)
                pressureText.text = FormatPressureLabel(targetPressure);

            pressureCoroutine = null;
            currentPressure = 0f;

            if (actionOrderManager != null && actionOrderManager.GetCurrentStep() == step)
                actionOrderManager.RegisterAction("", ActionType.CheckPSI);

            if (pressureText != null)
                pressureText.text = "";
        }

        private string FormatPressureLabel(float pressure)
        {
            string format = string.IsNullOrWhiteSpace(pressureLabelFormat)
                ? "Pressure: {0} PSI"
                : pressureLabelFormat;

            try
            {
                string label = string.Format(format, Mathf.RoundToInt(pressure));
                hasLoggedInvalidLabelFormat = false;
                return label;
            }
            catch (System.FormatException)
            {
                if (!hasLoggedInvalidLabelFormat)
                {
                    Debug.LogWarning($"Invalid pressure label format '{format}'. Use {{0}} for the pressure value.", this);
                    hasLoggedInvalidLabelFormat = true;
                }

                return $"Pressure: {Mathf.RoundToInt(pressure)} PSI";
            }
        }
    }
}
