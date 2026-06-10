using UnityEngine;

public class ValveRotationGuideArrow : MonoBehaviour
{
    [Header("Step Visibility")]
    [SerializeField] private ValveStateTracker linkedValve;
    [SerializeField] private bool onlyShowDuringValveStep = false;
    [SerializeField] private bool hideInExamMode = true;

    [Header("Arrow Visual")]
    [SerializeField] private Transform arrowVisual;

    [Header("Target Pose")]
    [Tooltip("Optional. If assigned, the arrow moves/rotates to this transform instead of using the manual offsets below.")]
    [SerializeField] private Transform targetPose;
    [SerializeField] private bool useTargetPoseRotation = true;
    [SerializeField] private bool addRotationOffsetToTargetPose = true;

    [Header("Motion")]
    [SerializeField] private Vector3 localMoveOffset = new Vector3(0f, -0.15f, 0f);
    [SerializeField] private Vector3 localRotationOffset = new Vector3(0f, 0f, 25f);
    [SerializeField] private float moveOutDuration = 0.45f;
    [SerializeField] private float holdDuration = 0.2f;
    [SerializeField] private float returnDuration = 0.45f;
    [SerializeField] private float pauseDuration = 0.25f;
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private ActionOrderManager actionOrderManager;
    private Renderer[] renderers;
    private Collider[] colliders;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private bool isVisible = true;
    private bool hasStartPose;

    private void Awake()
    {
        if (arrowVisual == null)
            arrowVisual = transform;

        if (linkedValve == null)
            linkedValve = GetComponentInParent<ValveStateTracker>();

        actionOrderManager = FindAnyObjectByType<ActionOrderManager>();
        CacheVisuals();
        CaptureStartPose();
        SetVisible(ShouldAnimateNow());
    }

    private void OnEnable()
    {
        if (!hasStartPose)
            CaptureStartPose();
    }

    private void OnDisable()
    {
        ResetPose();
    }

    private void Update()
    {
        bool shouldAnimate = ShouldAnimateNow();
        SetVisible(shouldAnimate);

        if (!shouldAnimate)
        {
            ResetPose();
            return;
        }

        AnimateArrow();
    }

    [ContextMenu("Capture Current Arrow Pose")]
    public void CaptureStartPose()
    {
        if (arrowVisual == null)
            return;

        startLocalPosition = arrowVisual.localPosition;
        startLocalRotation = arrowVisual.localRotation;
        hasStartPose = true;
    }

    private void AnimateArrow()
    {
        if (!hasStartPose)
            CaptureStartPose();

        float outTime = Mathf.Max(0.01f, moveOutDuration);
        float holdTime = Mathf.Max(0f, holdDuration);
        float returnTime = Mathf.Max(0.01f, returnDuration);
        float pauseTime = Mathf.Max(0f, pauseDuration);
        float totalTime = outTime + holdTime + returnTime + pauseTime;
        float loopTime = Mathf.Repeat(Time.time, totalTime);

        float progress;
        if (loopTime < outTime)
        {
            progress = loopTime / outTime;
        }
        else if (loopTime < outTime + holdTime)
        {
            progress = 1f;
        }
        else if (loopTime < outTime + holdTime + returnTime)
        {
            float returnProgress = (loopTime - outTime - holdTime) / returnTime;
            progress = 1f - returnProgress;
        }
        else
        {
            progress = 0f;
        }

        float curvedProgress = motionCurve != null ? motionCurve.Evaluate(progress) : progress;
        if (targetPose != null)
        {
            Transform parent = arrowVisual.parent;
            Vector3 startWorldPosition = parent != null ? parent.TransformPoint(startLocalPosition) : startLocalPosition;
            Quaternion startWorldRotation = parent != null ? parent.rotation * startLocalRotation : startLocalRotation;
            Quaternion targetWorldRotation = useTargetPoseRotation ? targetPose.rotation : startWorldRotation;

            if (addRotationOffsetToTargetPose)
                targetWorldRotation *= Quaternion.Euler(localRotationOffset);

            arrowVisual.position = Vector3.LerpUnclamped(startWorldPosition, targetPose.position, curvedProgress);
            arrowVisual.rotation = Quaternion.SlerpUnclamped(startWorldRotation, targetWorldRotation, curvedProgress);
            return;
        }

        arrowVisual.localPosition = Vector3.LerpUnclamped(startLocalPosition, startLocalPosition + localMoveOffset, curvedProgress);
        arrowVisual.localRotation = startLocalRotation * Quaternion.Euler(localRotationOffset * curvedProgress);
    }

    private bool ShouldAnimateNow()
    {
        if (!onlyShowDuringValveStep)
            return true;

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>();

        if (actionOrderManager == null || linkedValve == null)
            return false;

        if (hideInExamMode && actionOrderManager.isExam)
            return false;

        ActionStep currentStep = actionOrderManager.GetCurrentStep();
        return currentStep != null &&
               (currentStep.actionType == ActionType.TurnOnValve || currentStep.actionType == ActionType.TurnOffValve) &&
               actionOrderManager.CurrentStepRequiresTag(linkedValve.tag);
    }

    private void ResetPose()
    {
        if (!hasStartPose || arrowVisual == null)
            return;

        arrowVisual.localPosition = startLocalPosition;
        arrowVisual.localRotation = startLocalRotation;
    }

    private void CacheVisuals()
    {
        if (arrowVisual == null)
            return;

        renderers = arrowVisual.GetComponentsInChildren<Renderer>(true);
        colliders = arrowVisual.GetComponentsInChildren<Collider>(true);
    }

    private void SetVisible(bool visible)
    {
        if (isVisible == visible)
            return;

        isVisible = visible;

        if (arrowVisual != null && arrowVisual != transform)
            arrowVisual.gameObject.SetActive(visible);

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }
    }
}
