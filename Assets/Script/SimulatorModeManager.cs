namespace FireValveSimulator
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.Serialization;
    using UnityEngine.XR.Interaction.Toolkit.Interactors;

    public class SimulatorModeManager : MonoBehaviour
    {
        [SerializeField] private SimulatorMode currentMode = SimulatorMode.Menu;
        [SerializeField] private bool showMenuOnStart = true;

        [Header("Exam Skip")]
        [FormerlySerializedAs("allowExamStepSkip")]
        [SerializeField] private bool isExamSkippable = false;
        [Min(0f)]
        [SerializeField] private float examSkipPenaltySeconds = 20f;

        [Header("XR Far Interaction")]
        [Tooltip("Keeps the hand ray available in the menu and refreshes it when a simulator mode starts.")]
        [SerializeField] private bool manageFarInteractors = true;
        [Tooltip("Optional explicit references. When empty, every Near-Far Interactor in the scene is found automatically.")]
        [SerializeField] private NearFarInteractor[] farInteractors;

        public UnityEvent onEnterMenu;
        public UnityEvent onEnterLearning;
        public UnityEvent onEnterTraining;
        public UnityEvent onEnterExam;
        public UnityEvent onModeChanged;

        public static event Action<SimulatorMode> OnModeChanged;

        public SimulatorMode CurrentMode => currentMode;
        public bool IsExamSkippable => isExamSkippable;
        public float ExamSkipPenaltySeconds => Mathf.Max(0f, examSkipPenaltySeconds);

        private Coroutine restoreFarInteractorsCoroutine;

        private void Awake()
        {
            if (!manageFarInteractors)
                return;

            ResolveFarInteractors();

            // The menu must be usable as soon as the scene appears. If the scene is
            // configured to begin inside a mode, PublishModeChanged refreshes the ray.
            SetFarInteractionEnabled(currentMode == SimulatorMode.Menu);
        }

        public bool IsStepSkipAllowedInMode(SimulatorMode mode)
        {
            return mode == SimulatorMode.Learning ||
                   mode == SimulatorMode.Training ||
                   (mode == SimulatorMode.Exam && isExamSkippable);
        }

        private void Start()
        {
            if (showMenuOnStart)
                ShowMenu();
            else
                PublishModeChanged();
        }

        public void ShowMenu()
        {
            StartMode(SimulatorMode.Menu);
        }

        public void StartLearningMode()
        {
            StartMode(SimulatorMode.Learning);
        }

        public void StartTrainingMode()
        {
            StartMode(SimulatorMode.Training);
        }

        public void StartExamMode()
        {
            StartMode(SimulatorMode.Exam);
        }

        public void StartMode(SimulatorMode mode)
        {
            currentMode = mode;
            PublishModeChanged();
        }

        public bool TryStartModeBySceneName(string sceneOrModeName)
        {
            if (!TryGetModeFromSceneName(sceneOrModeName, out SimulatorMode mode))
                return false;

            StartMode(mode);
            return true;
        }

        public void ResetCurrentMode()
        {
            PublishModeChanged();
        }

        private void PublishModeChanged()
        {
            RefreshFarInteractionForMode();

            OnModeChanged?.Invoke(currentMode);
            onModeChanged?.Invoke();

            if (currentMode == SimulatorMode.Menu)
                onEnterMenu?.Invoke();
            else if (currentMode == SimulatorMode.Learning)
                onEnterLearning?.Invoke();
            else if (currentMode == SimulatorMode.Training)
                onEnterTraining?.Invoke();
            else if (currentMode == SimulatorMode.Exam)
                onEnterExam?.Invoke();
        }

        private void RefreshFarInteractionForMode()
        {
            if (!manageFarInteractors)
                return;

            if (restoreFarInteractorsCoroutine != null)
            {
                StopCoroutine(restoreFarInteractorsCoroutine);
                restoreFarInteractorsCoroutine = null;
            }

            ResolveFarInteractors();

            if (currentMode == SimulatorMode.Menu)
            {
                SetFarInteractionEnabled(true);
                return;
            }

            // Drop any menu hover/selection state while the session resets, then
            // restore the ray on the next frame after the new mode has initialized.
            SetFarInteractionEnabled(false);
            restoreFarInteractorsCoroutine = StartCoroutine(RestoreFarInteractionAfterModeStart());
        }

        private IEnumerator RestoreFarInteractionAfterModeStart()
        {
            yield return null;

            ResolveFarInteractors();
            SetFarInteractionEnabled(true);
            restoreFarInteractorsCoroutine = null;
        }

        private void ResolveFarInteractors()
        {
            if (farInteractors != null && farInteractors.Length > 0 &&
                !Array.Exists(farInteractors, interactor => interactor == null))
            {
                return;
            }

            farInteractors = FindObjectsByType<NearFarInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private void SetFarInteractionEnabled(bool enabled)
        {
            if (farInteractors == null)
                return;

            foreach (NearFarInteractor interactor in farInteractors)
            {
                if (interactor != null)
                    interactor.enableFarCasting = enabled;
            }
        }

        private bool TryGetModeFromSceneName(string sceneOrModeName, out SimulatorMode mode)
        {
            mode = SimulatorMode.Menu;

            if (string.IsNullOrWhiteSpace(sceneOrModeName))
                return false;

            string normalized = sceneOrModeName.Trim().ToLowerInvariant().Replace(" ", "");
            switch (normalized)
            {
                case "mainmenu":
                case "menu":
                    mode = SimulatorMode.Menu;
                    return true;

                case "learningscene":
                case "learning":
                    mode = SimulatorMode.Learning;
                    return true;

                case "trainningscene":
                case "trainingscene":
                case "trainning":
                case "training":
                    mode = SimulatorMode.Training;
                    return true;

                case "examscene":
                case "exam":
                    mode = SimulatorMode.Exam;
                    return true;

                default:
                    return false;
            }
        }
    }
}
