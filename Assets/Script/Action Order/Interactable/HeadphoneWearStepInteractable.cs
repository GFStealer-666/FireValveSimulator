using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class HeadphoneWearStepInteractable : MonoBehaviour
{
    [SerializeField] private ActionOrderManager actionOrderManager;
    [SerializeField] private Transform wearTarget;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private float wearDistance = 0.45f;
    [SerializeField] private Vector3 wearTargetLocalOffset = Vector3.zero;
    [SerializeField] private bool onlyCompleteWhenCurrentStep = true;
    [SerializeField] private bool autoConfigureGrabInteractable = true;
    [SerializeField] private bool restoreWhenStepBecomesCurrent = true;

    private XRGrabInteractable grabInteractable;
    private bool isGrabbed;
    private bool isCompleted;

    private void Awake()
    {
        ResolveReferences();
        ConfigureGrabInteractable();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeGrabEvents();
        ActionOrderManager.OnCurrentStepChanged += HandleCurrentStepChanged;
    }

    private void OnDisable()
    {
        UnsubscribeGrabEvents();
        ActionOrderManager.OnCurrentStepChanged -= HandleCurrentStepChanged;
    }

    private void Update()
    {
        if (isCompleted)
            return;

        if (grabInteractable != null)
            isGrabbed = grabInteractable.isSelected;

        if (isGrabbed)
            TryCompleteWearStep();
    }

    public bool TryCompleteWearStep()
    {
        if (isCompleted || !IsWithinWearDistance() || !CanCompleteCurrentStep())
            return false;

        isCompleted = true;
        SetWearObjectVisible(false);

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);

        if (actionOrderManager != null)
            actionOrderManager.RegisterAction(tag, ActionType.WearHeadphone);

        return true;
    }

    public bool IsWithinWearDistance()
    {
        Transform target = ResolveWearTarget();
        if (target == null)
            return false;

        float maxDistance = Mathf.Max(0f, wearDistance);
        Vector3 targetPosition = target.TransformPoint(wearTargetLocalOffset);
        return (transform.position - targetPosition).sqrMagnitude <= maxDistance * maxDistance;
    }

    private void HandleSelectEntered(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        TryCompleteWearStep();
    }

    private void HandleSelectExited(SelectExitEventArgs args)
    {
        isGrabbed = false;
    }

    private void HandleCurrentStepChanged(ActionStep step, int stepIndex)
    {
        if (!restoreWhenStepBecomesCurrent || step == null || step.actionType != ActionType.WearHeadphone)
            return;

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);

        if (actionOrderManager != null && actionOrderManager.CurrentStepRequiresTag(tag))
        {
            isCompleted = false;
            SetWearObjectVisible(true);
        }
    }

    private bool CanCompleteCurrentStep()
    {
        if (!onlyCompleteWhenCurrentStep)
            return true;

        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);

        ActionStep currentStep = actionOrderManager != null ? actionOrderManager.GetCurrentStep() : null;
        return currentStep != null &&
               currentStep.actionType == ActionType.WearHeadphone &&
               actionOrderManager.CurrentStepRequiresTag(tag);
    }

    private void ResolveReferences()
    {
        if (actionOrderManager == null)
            actionOrderManager = FindAnyObjectByType<ActionOrderManager>(FindObjectsInactive.Include);

        if (modelRoot == null)
            modelRoot = transform;

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private Transform ResolveWearTarget()
    {
        if (wearTarget != null)
            return wearTarget;

        XROrigin xrOrigin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            wearTarget = xrOrigin.Camera.transform;
            return wearTarget;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            wearTarget = mainCamera.transform;

        return wearTarget;
    }

    private void ConfigureGrabInteractable()
    {
        if (!autoConfigureGrabInteractable)
            return;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (!HasUsableCollider())
            AddBoundsCollider();

        if (grabInteractable == null)
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();

        grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grabInteractable.throwOnDetach = false;
    }

    private bool HasUsableCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider != null && !collider.isTrigger)
                return true;
        }

        return false;
    }

    private void AddBoundsCollider()
    {
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        if (!TryGetRendererLocalBounds(out Bounds localBounds))
        {
            box.size = Vector3.one * 0.25f;
            return;
        }

        box.center = localBounds.center;
        box.size = Vector3.Max(localBounds.size, Vector3.one * 0.05f);
    }

    private bool TryGetRendererLocalBounds(out Bounds localBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        localBounds = new Bounds(Vector3.zero, Vector3.zero);

        if (renderers.Length == 0)
            return false;

        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Bounds rendererBounds = renderer.bounds;
            EncapsulateWorldBounds(rendererBounds, ref localBounds, ref hasBounds);
        }

        return hasBounds;
    }

    private void EncapsulateWorldBounds(Bounds worldBounds, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 worldCorner = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);

                    Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
        }
    }

    private void SubscribeGrabEvents()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
        grabInteractable.selectExited.RemoveListener(HandleSelectExited);
        grabInteractable.selectEntered.AddListener(HandleSelectEntered);
        grabInteractable.selectExited.AddListener(HandleSelectExited);
    }

    private void UnsubscribeGrabEvents()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
        grabInteractable.selectExited.RemoveListener(HandleSelectExited);
    }

    private void SetWearObjectVisible(bool visible)
    {
        Transform root = modelRoot != null ? modelRoot : transform;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider != null)
                collider.enabled = visible;
        }

        if (grabInteractable != null)
            grabInteractable.enabled = visible;
    }

    public void SetWearTargetForTesting(Transform target)
    {
        wearTarget = target;
    }

    public void SetWearDistanceForTesting(float distance)
    {
        wearDistance = distance;
    }
}
