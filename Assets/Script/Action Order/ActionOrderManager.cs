namespace FireValveSimulator
{
    using System;
    using System.Collections.Generic;
    using EPOOutline;
    using TMPro;
    using UnityEngine;
    using UnityEngine.Events;

    public class ActionOrderManager : MonoBehaviour
    {
        [Serializable]
        public class StepOutlineTargetBinding
        {
            [Tooltip("Optional. For multi-object steps, use the object's completion tag so its outline turns off as soon as that object is completed.")]
            public string completionTag;

            [Tooltip("The Outlinable component on the visible valve, switch, panel, or tool.")]
            public Outlinable target;
        }

        [Serializable]
        public class StepOutlineBinding
        {
            public ActionStep step;
            public List<StepOutlineTargetBinding> targets = new List<StepOutlineTargetBinding>();
        }

        public List<ActionStep> orderedSteps;
        private int currentStepIndex = 0;
        [SerializeField] private ActionStep currentStep;
        private bool sequenceActive = false;
        private readonly HashSet<string> completedTagsInCurrentStep = new HashSet<string>();

        public UnityEvent onAllStepsCompleted;
        public UnityEvent onStepFailed;
        public UnityEvent onStepSuccess;
        public UnityEvent OnSubStepCompleted;

        public static event Action OnAllStepsCompleted, OnStepFailed, OnStepSuccess;
        public static event Action<ActionStep> OnStepCompleted;
        public static event Action<ActionStep, bool> OnStepFinished;
        public static event Action<ActionStep, int> OnCurrentStepChanged;
        public event Action<ActionStep, int> CurrentStepChanged;

        private readonly List<ObjectHighlighter> activeHighlighters = new List<ObjectHighlighter>();
        private readonly List<Outlinable> activeStepOutlines = new List<Outlinable>();

        [Header("Learning / Training Outline Hints")]
        [Tooltip("Optional explicit Inspector mapping. When a step has no configured target, tag-based automatic highlighting is used as a fallback.")]
        [SerializeField] private List<StepOutlineBinding> stepOutlineBindings = new List<StepOutlineBinding>();

        private readonly Dictionary<int, StepStateSnapshot> stepStateSnapshots = new Dictionary<int, StepStateSnapshot>();
        public bool isExam = false;
        public TMP_Text stepText;

        [Header("Timer / PSI Action")]
        [Tooltip("Shared panel shown only while the current action is Wait Limited Time or Check PSI.")]
        [SerializeField] private GameObject timerPsiPanel;
        [SerializeField] private WaitTimer waitTimer;
        [SerializeField] private PressureSimulator pressureSimulator;

        public int CurrentStepIndex => sequenceActive ? currentStepIndex : -1;
        public int StepCount => orderedSteps != null ? orderedSteps.Count : 0;

        private void Awake()
        {
            DisableAllConfiguredOutlines();
            ResolveTimerPsiReferences();
            RefreshTimerPsiActionState();
        }

        [ContextMenu("Progress To Next Step")]
        public void CompleteCurrentStepFromContextMenu()
        {
            SkipCurrentStep();
        }

        public void SkipCurrentStep()
        {
            TrySkipCurrentStep();
        }

        public bool CanSkipCurrentStep()
        {
            return Application.isPlaying && sequenceActive && currentStep != null;
        }

        public bool CanGoToPreviousStep()
        {
            return Application.isPlaying && sequenceActive && currentStep != null && currentStepIndex > 0;
        }

        public bool TrySkipCurrentStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Skip Current Step only works in Play Mode.");
                return false;
            }

            if (!sequenceActive)
            {
                Debug.LogWarning("Cannot skip the current step because the sequence is not active. Use Debug/Reset Sequence first.");
                return false;
            }

            if (currentStep == null)
            {
                Debug.LogWarning("Cannot skip the current step because no current step is assigned.");
                return false;
            }

            Debug.Log($"Skipping step {currentStepIndex + 1}: {currentStep.stepName}");
            CompleteStep(true);
            return true;
        }

        [ContextMenu("Reverse To Previous Step")]
        public void PreviousStep()
        {
            TryGoToPreviousStep();
        }

        public bool TryGoToPreviousStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Previous Step only works in Play Mode.");
                return false;
            }

            if (!sequenceActive || currentStep == null)
            {
                Debug.LogWarning("Cannot return to the previous step because the sequence is not active.");
                return false;
            }

            if (currentStepIndex <= 0)
            {
                Debug.LogWarning("Cannot return to the previous step because the sequence is already at step 1.");
                return false;
            }

            int stepBeingLeft = currentStepIndex;
            int previousStepIndex = currentStepIndex - 1;

            RestoreStepState(stepBeingLeft);
            RestoreStepState(previousStepIndex);
            RemoveStepSnapshotsFrom(previousStepIndex);
            ResetTransientStepHelpers();

            currentStepIndex = previousStepIndex;
            currentStep = orderedSteps[currentStepIndex];
            completedTagsInCurrentStep.Clear();

            UpdateCurrentStepUI();
            RefreshTimerPsiActionState();
            HighlightCurrentStepObjects();
            NotifyCurrentStepChanged();
            CaptureCurrentStepState();

            Debug.Log($"Returned to step {currentStepIndex + 1}: {currentStep.stepName}");
            return true;
        }

        [ContextMenu("Debug/Complete Current Mode")]
        public void CompleteCurrentModeFromContextMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Complete Current Mode only works in Play Mode.");
                return;
            }

            if (!sequenceActive)
            {
                Debug.LogWarning("Cannot complete the current mode because the sequence is not active. Use Debug/Reset Sequence first.");
                return;
            }

            if (!HasSteps() || currentStep == null)
            {
                Debug.LogWarning("Cannot complete the current mode because there is no current step.");
                return;
            }

            int startStep = currentStepIndex + 1;
            int safetyLimit = orderedSteps.Count + 1;
            int completedSteps = 0;

            while (sequenceActive && currentStep != null && completedSteps < safetyLimit)
            {
                CompleteStep();
                completedSteps++;
            }

            if (sequenceActive)
            {
                Debug.LogWarning("Stopped completing the current mode because the safety limit was reached.");
                return;
            }

            Debug.Log($"Debug completed current mode from step {startStep}. Completed {completedSteps} step(s).");
        }

        [ContextMenu("Debug/Reset Sequence")]
        public void ResetSequenceFromContextMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Reset Sequence only works in Play Mode.");
                return;
            }

            ResetSequence();
        }

        [ContextMenu("Debug/Reset To Idle")]
        public void ResetToIdleFromContextMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Reset To Idle only works in Play Mode.");
                return;
            }

            ResetToIdle();
        }

        public void InitializeSequence()
        {
            ResetTransientStepHelpers();
            stepStateSnapshots.Clear();

            if (!HasSteps())
            {
                sequenceActive = false;
                currentStep = null;
                UpdateCurrentStepUI();
                RefreshTimerPsiActionState();
                NotifyCurrentStepChanged();
                Debug.LogWarning("ActionOrderManager cannot initialize because no action steps are assigned.");
                return;
            }

            sequenceActive = true;
            currentStepIndex = Mathf.Clamp(currentStepIndex, 0, orderedSteps.Count - 1);
            currentStep = orderedSteps[currentStepIndex];
            UpdateCurrentStepUI();
            RefreshTimerPsiActionState();

            if (!isExam)
                HighlightCurrentStepObjects();

            NotifyCurrentStepChanged();
            CaptureCurrentStepState();
        }

        public void RegisterAction(string objectTag, ActionType actionType)
        {
            if (!HasSteps() || currentStepIndex >= orderedSteps.Count)
                return;

            currentStep = orderedSteps[currentStepIndex];
            if (currentStep == null)
            {
                Debug.LogWarning($"Action step {currentStepIndex} is missing.");
                return;
            }

            if (currentStep.actionType != actionType)
            {
                Debug.LogWarning($"Wrong action type for {objectTag}");
                TriggerStepFailed();
                return;
            }

            if (currentStep.objectTags == null || currentStep.objectTags.Length == 0)
            {
                Debug.Log($"Action {actionType} completed for {currentStep.stepName}.");
                CompleteStep();
                return;
            }

            if (CurrentStepRequiresTag(objectTag))
            {
                completedTagsInCurrentStep.Add(objectTag);
                Debug.Log($"Sub-action {objectTag} completed for {currentStep.stepName}");
                OnSubStepCompleted?.Invoke();

                if (completedTagsInCurrentStep.Count >= currentStep.objectTags.Length)
                    CompleteStep();
                else if (!isExam)
                    HighlightCurrentStepObjects();
            }
            else
            {
                Debug.LogWarning($"Wrong object {objectTag} for current step");
                TriggerStepFailed();
            }
        }

        public ActionStep GetCurrentStep()
        {
            if (!sequenceActive)
                return null;

            if (HasSteps() && currentStepIndex < orderedSteps.Count)
                return orderedSteps[currentStepIndex];

            return null;
        }

        public bool CurrentStepRequiresTag(string objectTag)
        {
            if (string.IsNullOrEmpty(objectTag))
                return false;

            ActionStep step = GetCurrentStep();
            return step != null &&
                   step.objectTags != null &&
                   Array.Exists(step.objectTags, tag => tag == objectTag);
        }

        public bool CurrentStepTagCompleted(string objectTag)
        {
            return !string.IsNullOrEmpty(objectTag) && completedTagsInCurrentStep.Contains(objectTag);
        }

        public void ResetSequence()
        {
            Debug.Log("Resetting sequence...");
            ResetTransientStepHelpers();
            sequenceActive = true;
            currentStepIndex = 0;
            completedTagsInCurrentStep.Clear();
            stepStateSnapshots.Clear();

            if (!HasSteps())
            {
                sequenceActive = false;
                currentStep = null;
                ClearHighlights();
                UpdateCurrentStepUI();
                RefreshTimerPsiActionState();
                NotifyCurrentStepChanged();
                Debug.LogWarning("ActionOrderManager cannot reset because no action steps are assigned.");
                return;
            }

            currentStep = orderedSteps[currentStepIndex];
            RefreshTimerPsiActionState();

            if (!isExam)
                HighlightCurrentStepObjects();

            UpdateCurrentStepUI();
            NotifyCurrentStepChanged();
            CaptureCurrentStepState();
        }

        public void ResetToIdle()
        {
            ResetTransientStepHelpers();
            sequenceActive = false;
            currentStepIndex = 0;
            completedTagsInCurrentStep.Clear();
            stepStateSnapshots.Clear();
            currentStep = null;
            ClearHighlights();

            if (stepText != null)
                stepText.text = "";

            RefreshTimerPsiActionState();
            NotifyCurrentStepChanged();
        }

        public void SetExamMode(bool isExamMode)
        {
            isExam = isExamMode;

            if (isExam)
            {
                Debug.Log("Exam mode ON.");
                ClearHighlights();
            }
            else
            {
                HighlightCurrentStepObjects();
            }
        }

        public void TriggerFailure()
        {
            Debug.LogWarning("Exam failed due to timeout.");
            TriggerStepFailed();
        }

        public void UpdateCurrentStepUI()
        {
            if (stepText == null)
                return;

            int totalSteps = orderedSteps != null ? orderedSteps.Count : 0;
            if (totalSteps == 0)
            {
                stepText.text = "No steps assigned";
                return;
            }

            if (currentStepIndex < totalSteps)
            {
                int displayStep = currentStepIndex + 1;
                stepText.text = $"Step {displayStep} of {totalSteps}";
            }
            else
            {
                stepText.text = "All steps completed!";
            }
        }

        [ContextMenu("Setup/Resolve Timer / PSI References")]
        public void ResolveTimerPsiReferences()
        {
            if (waitTimer == null)
                waitTimer = FindAnyObjectByType<WaitTimer>(FindObjectsInactive.Include);

            if (pressureSimulator == null)
                pressureSimulator = FindAnyObjectByType<PressureSimulator>(FindObjectsInactive.Include);

            if (timerPsiPanel == null && waitTimer != null && waitTimer.timerText != null)
                timerPsiPanel = waitTimer.timerText.transform.parent != null
                    ? waitTimer.timerText.transform.parent.gameObject
                    : waitTimer.timerText.gameObject;

            if (timerPsiPanel == null && pressureSimulator != null && pressureSimulator.pressureText != null)
                timerPsiPanel = pressureSimulator.pressureText.transform.parent != null
                    ? pressureSimulator.pressureText.transform.parent.gameObject
                    : pressureSimulator.pressureText.gameObject;

            if (timerPsiPanel == null)
                timerPsiPanel = FindSceneObjectByName("PSI/CountdownPanel");

            if (timerPsiPanel == null)
                return;

            TMP_Text sharedValueLabel = timerPsiPanel.GetComponentInChildren<TMP_Text>(true);
            if (waitTimer != null && waitTimer.timerText == null)
                waitTimer.timerText = sharedValueLabel;

            if (pressureSimulator != null && pressureSimulator.pressureText == null)
                pressureSimulator.pressureText = sharedValueLabel;
        }

        private void RefreshTimerPsiActionState()
        {
            ResolveTimerPsiReferences();

            bool timerStepActive = sequenceActive && currentStep != null &&
                                   currentStep.actionType == ActionType.WaitLimitedTime;
            bool pressureStepActive = sequenceActive && currentStep != null &&
                                      currentStep.actionType == ActionType.CheckPSI;

            if (waitTimer != null)
            {
                if (!timerStepActive && waitTimer.isActive)
                    waitTimer.ResetTimer();

                waitTimer.isActive = timerStepActive;
            }

            if (pressureSimulator != null)
            {
                if (!pressureStepActive && pressureSimulator.isActive)
                    pressureSimulator.ResetPressure();

                pressureSimulator.isActive = pressureStepActive;
            }

            if (timerPsiPanel != null)
                timerPsiPanel.SetActive(timerStepActive || pressureStepActive);
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate != null &&
                    candidate.name == objectName &&
                    candidate.scene.IsValid() &&
                    candidate.scene.isLoaded)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void HighlightCurrentStepObjects()
        {
            if (isExam)
                return;

            ClearHighlights();

            ActionStep step = GetCurrentStep();
            if (step == null)
                return;

            if (HighlightConfiguredTargets(step))
                return;

            if (step.HintObjectTags == null)
                return;

            foreach (string tag in step.HintObjectTags)
            {
                if (string.IsNullOrEmpty(tag) || completedTagsInCurrentStep.Contains(tag))
                    continue;

                GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject obj in objs)
                {
                    ObjectHighlighter highlighter = FindOrCreateHighlighter(obj);
                    if (highlighter != null &&
                        highlighter.HasHighlightTarget() &&
                        !activeHighlighters.Contains(highlighter))
                    {
                        highlighter.Highlight();
                        activeHighlighters.Add(highlighter);
                    }
                }
            }
        }

        private void CompleteStep(bool wasSkipped = false)
        {
            ActionStep completedStep = currentStep;
            ResetTransientStepHelpers();
            Debug.Log($"Step {completedStep.stepName} completed!");
            OnStepCompleted?.Invoke(completedStep);
            OnStepFinished?.Invoke(completedStep, wasSkipped);
            onStepSuccess?.Invoke();
            OnStepSuccess?.Invoke();

            currentStepIndex++;
            completedTagsInCurrentStep.Clear();

            if (currentStepIndex >= orderedSteps.Count)
            {
                Debug.Log("All steps completed!");
                sequenceActive = false;
                currentStep = null;
                ClearHighlights();
                RefreshTimerPsiActionState();
                onAllStepsCompleted?.Invoke();
                OnAllStepsCompleted?.Invoke();
                UpdateCurrentStepUI();
                NotifyCurrentStepChanged();
            }
            else
            {
                currentStep = orderedSteps[currentStepIndex];
                UpdateCurrentStepUI();
                RefreshTimerPsiActionState();
                HighlightCurrentStepObjects();
                NotifyCurrentStepChanged();
                CaptureCurrentStepState();
            }
        }

        private bool HighlightConfiguredTargets(ActionStep step)
        {
            if (stepOutlineBindings == null)
                return false;

            foreach (StepOutlineBinding binding in stepOutlineBindings)
            {
                if (binding == null || binding.step != step || binding.targets == null)
                    continue;

                foreach (StepOutlineTargetBinding targetBinding in binding.targets)
                {
                    if (targetBinding == null || targetBinding.target == null)
                        continue;

                    if (!string.IsNullOrEmpty(targetBinding.completionTag) &&
                        completedTagsInCurrentStep.Contains(targetBinding.completionTag))
                    {
                        targetBinding.target.enabled = false;
                        continue;
                    }

                    targetBinding.target.enabled = true;
                    if (!activeStepOutlines.Contains(targetBinding.target))
                        activeStepOutlines.Add(targetBinding.target);
                }

                // The presence of a binding is authoritative. An empty Targets list
                // intentionally means that this step should not display an outline.
                return true;
            }

            return false;
        }

        private static ObjectHighlighter FindOrCreateHighlighter(GameObject taggedObject)
        {
            if (taggedObject == null)
                return null;

            ObjectHighlighter highlighter = taggedObject.GetComponentInChildren<ObjectHighlighter>(true);
            if (highlighter != null)
                return highlighter;

            Transform candidate = taggedObject.transform;
            while (candidate != null)
            {
                highlighter = candidate.GetComponent<ObjectHighlighter>();
                if (highlighter != null)
                    return highlighter;

                if (candidate.GetComponentInChildren<Renderer>(true) != null)
                    return candidate.gameObject.AddComponent<ObjectHighlighter>();

                candidate = candidate.parent;
            }

            Debug.LogWarning($"No renderer was found for highlighted object '{taggedObject.name}' ({taggedObject.tag}).");
            return null;
        }

        private void CaptureCurrentStepState()
        {
            if (!sequenceActive || currentStep == null || currentStepIndex < 0)
                return;

            stepStateSnapshots[currentStepIndex] = StepStateSnapshot.Capture(currentStep);
        }

        private void RestoreStepState(int stepIndex)
        {
            if (stepStateSnapshots.TryGetValue(stepIndex, out StepStateSnapshot snapshot))
                snapshot.Restore();
        }

        private void RemoveStepSnapshotsFrom(int firstStepIndex)
        {
            List<int> indexesToRemove = new List<int>();
            foreach (int index in stepStateSnapshots.Keys)
            {
                if (index >= firstStepIndex)
                    indexesToRemove.Add(index);
            }

            foreach (int index in indexesToRemove)
                stepStateSnapshots.Remove(index);
        }

        private static void ResetTransientStepHelpers()
        {
            foreach (WaitTimer timer in FindObjectsByType<WaitTimer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                timer.ResetTimer();

            foreach (PressureSimulator pressure in FindObjectsByType<PressureSimulator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                pressure.ResetPressure();
        }

        private void ClearHighlights()
        {
            foreach (Outlinable outline in activeStepOutlines)
            {
                if (outline != null)
                    outline.enabled = false;
            }

            activeStepOutlines.Clear();

            foreach (ObjectHighlighter highlighter in activeHighlighters)
            {
                if (highlighter != null)
                    highlighter.RemoveHighlight();
            }

            activeHighlighters.Clear();
        }

        private void DisableAllConfiguredOutlines()
        {
            if (stepOutlineBindings == null)
                return;

            foreach (StepOutlineBinding binding in stepOutlineBindings)
            {
                if (binding == null || binding.targets == null)
                    continue;

                foreach (StepOutlineTargetBinding targetBinding in binding.targets)
                {
                    if (targetBinding != null && targetBinding.target != null)
                        targetBinding.target.enabled = false;
                }
            }

            activeStepOutlines.Clear();
        }

        private void TriggerStepFailed()
        {
            onStepFailed?.Invoke();
            OnStepFailed?.Invoke();
        }

        private void NotifyCurrentStepChanged()
        {
            int stepIndex = sequenceActive ? currentStepIndex : -1;
            CurrentStepChanged?.Invoke(currentStep, stepIndex);
            OnCurrentStepChanged?.Invoke(currentStep, stepIndex);
        }

        private bool HasSteps()
        {
            return orderedSteps != null && orderedSteps.Count > 0;
        }

        private sealed class StepStateSnapshot
        {
            private readonly List<TransformState> transformStates = new List<TransformState>();
            private readonly List<GameObjectState> gameObjectStates = new List<GameObjectState>();
            private readonly List<RendererState> rendererStates = new List<RendererState>();
            private readonly List<ColliderState> colliderStates = new List<ColliderState>();
            private readonly List<RigidbodyState> rigidbodyStates = new List<RigidbodyState>();
            private readonly List<ValveState> valveStates = new List<ValveState>();
            private readonly List<SwitchState> switchStates = new List<SwitchState>();
            private readonly List<KnobTrackerState> knobTrackerStates = new List<KnobTrackerState>();
            private readonly List<TwoHandValveRotator> valveRotators = new List<TwoHandValveRotator>();

            public static StepStateSnapshot Capture(ActionStep step)
            {
                StepStateSnapshot snapshot = new StepStateSnapshot();
                if (step == null || step.objectTags == null)
                    return snapshot;

                HashSet<GameObject> roots = new HashSet<GameObject>();
                foreach (string objectTag in step.objectTags)
                {
                    if (string.IsNullOrEmpty(objectTag))
                        continue;

                    foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (candidate == null || !candidate.scene.IsValid() || !candidate.scene.isLoaded)
                            continue;

                        if (candidate.CompareTag(objectTag))
                            roots.Add(candidate);
                    }
                }

                snapshot.CaptureRootsAndEventTargets(roots);
                return snapshot;
            }

            public void Restore()
            {
                foreach (TransformState state in transformStates)
                    state.Restore();

                foreach (RendererState state in rendererStates)
                    state.Restore();

                foreach (ColliderState state in colliderStates)
                    state.Restore();

                foreach (RigidbodyState state in rigidbodyStates)
                    state.Restore();

                foreach (ValveState state in valveStates)
                    state.Restore();

                foreach (SwitchState state in switchStates)
                    state.Restore();

                foreach (KnobTrackerState state in knobTrackerStates)
                    state.Restore();

                foreach (TwoHandValveRotator valveRotator in valveRotators)
                {
                    if (valveRotator != null)
                        valveRotator.ResetProgress();
                }

                // Restore children first so enabling a parent exposes the complete captured hierarchy.
                gameObjectStates.Sort((left, right) => right.HierarchyDepth.CompareTo(left.HierarchyDepth));
                foreach (GameObjectState state in gameObjectStates)
                    state.Restore();
            }

            private void CaptureRootsAndEventTargets(HashSet<GameObject> roots)
            {
                Queue<GameObject> pendingRoots = new Queue<GameObject>(roots);
                HashSet<GameObject> discoveredRoots = new HashSet<GameObject>(roots);

                while (pendingRoots.Count > 0)
                {
                    GameObject root = pendingRoots.Dequeue();
                    if (root == null)
                        continue;

                    foreach (SwitchHitDetector detector in root.GetComponentsInChildren<SwitchHitDetector>(true))
                        AddPersistentTargets(detector.onSwitchHit, pendingRoots, discoveredRoots);

                    foreach (ValveStateTracker valve in root.GetComponentsInChildren<ValveStateTracker>(true))
                        AddPersistentTargets(valve.OnValveRotate, pendingRoots, discoveredRoots);
                }

                HashSet<Transform> capturedTransforms = new HashSet<Transform>();
                foreach (GameObject root in discoveredRoots)
                    CaptureTransformTree(root.transform, capturedTransforms);
            }

            private static void AddPersistentTargets(
                UnityEventBase unityEvent,
                Queue<GameObject> pendingRoots,
                HashSet<GameObject> discoveredRoots)
            {
                if (unityEvent == null)
                    return;

                for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
                {
                    UnityEngine.Object target = unityEvent.GetPersistentTarget(i);
                    GameObject targetObject = target as GameObject;
                    if (targetObject == null && target is Component component)
                        targetObject = component.gameObject;

                    if (targetObject == null || !targetObject.scene.IsValid() || !discoveredRoots.Add(targetObject))
                        continue;

                    pendingRoots.Enqueue(targetObject);
                }
            }

            private void CaptureTransformTree(Transform transform, HashSet<Transform> capturedTransforms)
            {
                if (transform == null || !capturedTransforms.Add(transform))
                    return;

                GameObject gameObject = transform.gameObject;
                transformStates.Add(new TransformState(transform));
                gameObjectStates.Add(new GameObjectState(gameObject));

                foreach (Renderer renderer in gameObject.GetComponents<Renderer>())
                    rendererStates.Add(new RendererState(renderer));

                foreach (Collider collider in gameObject.GetComponents<Collider>())
                    colliderStates.Add(new ColliderState(collider));

                foreach (Rigidbody body in gameObject.GetComponents<Rigidbody>())
                    rigidbodyStates.Add(new RigidbodyState(body));

                foreach (ValveStateTracker valve in gameObject.GetComponents<ValveStateTracker>())
                    valveStates.Add(new ValveState(valve));

                foreach (SwitchHitDetector detector in gameObject.GetComponents<SwitchHitDetector>())
                    switchStates.Add(new SwitchState(detector));

                foreach (ValveRotationTracker tracker in gameObject.GetComponents<ValveRotationTracker>())
                    knobTrackerStates.Add(new KnobTrackerState(tracker));

                foreach (TwoHandValveRotator valveRotator in gameObject.GetComponents<TwoHandValveRotator>())
                    valveRotators.Add(valveRotator);

                foreach (Transform child in transform)
                    CaptureTransformTree(child, capturedTransforms);
            }

            private sealed class TransformState
            {
                private readonly Transform transform;
                private readonly Vector3 localPosition;
                private readonly Quaternion localRotation;
                private readonly Vector3 localScale;

                public TransformState(Transform transform)
                {
                    this.transform = transform;
                    localPosition = transform.localPosition;
                    localRotation = transform.localRotation;
                    localScale = transform.localScale;
                }

                public void Restore()
                {
                    if (transform == null)
                        return;

                    transform.localPosition = localPosition;
                    transform.localRotation = localRotation;
                    transform.localScale = localScale;
                }
            }

            private sealed class GameObjectState
            {
                private readonly GameObject gameObject;
                private readonly bool activeSelf;

                public int HierarchyDepth { get; }

                public GameObjectState(GameObject gameObject)
                {
                    this.gameObject = gameObject;
                    activeSelf = gameObject.activeSelf;

                    int hierarchyDepth = 0;
                    Transform current = gameObject.transform;
                    while (current != null)
                    {
                        hierarchyDepth++;
                        current = current.parent;
                    }

                    HierarchyDepth = hierarchyDepth;
                }

                public void Restore()
                {
                    if (gameObject != null)
                        gameObject.SetActive(activeSelf);
                }
            }

            private sealed class RendererState
            {
                private readonly Renderer renderer;
                private readonly bool enabled;

                public RendererState(Renderer renderer)
                {
                    this.renderer = renderer;
                    enabled = renderer.enabled;
                }

                public void Restore()
                {
                    if (renderer != null)
                        renderer.enabled = enabled;
                }
            }

            private sealed class ColliderState
            {
                private readonly Collider collider;
                private readonly bool enabled;

                public ColliderState(Collider collider)
                {
                    this.collider = collider;
                    enabled = collider.enabled;
                }

                public void Restore()
                {
                    if (collider != null)
                        collider.enabled = enabled;
                }
            }

            private sealed class RigidbodyState
            {
                private readonly Rigidbody rigidbody;
                private readonly Vector3 linearVelocity;
                private readonly Vector3 angularVelocity;

                public RigidbodyState(Rigidbody rigidbody)
                {
                    this.rigidbody = rigidbody;
                    linearVelocity = rigidbody.linearVelocity;
                    angularVelocity = rigidbody.angularVelocity;
                }

                public void Restore()
                {
                    if (rigidbody == null)
                        return;

                    rigidbody.linearVelocity = linearVelocity;
                    rigidbody.angularVelocity = angularVelocity;
                    if (linearVelocity.sqrMagnitude <= Mathf.Epsilon && angularVelocity.sqrMagnitude <= Mathf.Epsilon)
                        rigidbody.Sleep();
                }
            }

            private sealed class ValveState
            {
                private readonly ValveStateTracker valve;
                private readonly ValveStateTracker.ValveState state;

                public ValveState(ValveStateTracker valve)
                {
                    this.valve = valve;
                    state = valve.currentState;
                }

                public void Restore()
                {
                    if (valve != null)
                        valve.SetStateWithoutNotification(state);
                }
            }

            private sealed class SwitchState
            {
                private readonly SwitchHitDetector detector;
                private readonly bool wasTriggered;

                public SwitchState(SwitchHitDetector detector)
                {
                    this.detector = detector;
                    wasTriggered = detector.IsTriggered;
                }

                public void Restore()
                {
                    if (detector != null)
                        detector.RestoreTriggeredState(wasTriggered);
                }
            }

            private sealed class KnobTrackerState
            {
                private readonly ValveRotationTracker tracker;
                private readonly float knobValue;
                private readonly bool wasEnabled;

                public KnobTrackerState(ValveRotationTracker tracker)
                {
                    this.tracker = tracker;
                    knobValue = tracker.CurrentKnobValue;
                    wasEnabled = tracker.enabled;
                }

                public void Restore()
                {
                    if (tracker != null)
                        tracker.RestoreForPreviousStep(knobValue, wasEnabled);
                }
            }
        }
    }
}
