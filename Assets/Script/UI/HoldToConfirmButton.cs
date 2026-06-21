using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class HoldToConfirmButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
{
    [SerializeField] private Button button;
    [SerializeField, Min(0f)] private float holdDuration = 0.5f;
    [SerializeField] private GameObject spinnerPrefab;
    [SerializeField] private Vector2 spinnerSize = new Vector2(40f , 40f);
    [SerializeField] private Vector2 spinnerAnchoredOffset = Vector2.zero;
    [SerializeField] private bool useUnscaledTime = true;

    [SerializeField, HideInInspector] private GameObject spinnerInstance;
    [SerializeField, HideInInspector] private GameObject spinnerPrefabInUse;

    private RectTransform spinnerRect;
    private bool isHolding;
    private bool invoked;
    private float heldSeconds;
    private int activePointerId = int.MinValue;

    public void Configure(Button targetButton, float duration, GameObject spinner, Vector2 size, Vector2 anchoredOffset)
    {
        button = targetButton != null ? targetButton : GetComponent<Button>();
        holdDuration = Mathf.Max(0f, duration);
        spinnerPrefab = spinner;
        spinnerSize = size;
        spinnerAnchoredOffset = anchoredOffset;

        if (spinnerInstance != null && spinnerRect == null)
            spinnerRect = spinnerInstance.transform as RectTransform;

        if (spinnerInstance != null && spinnerPrefabInUse != spinnerPrefab)
        {
            DestroySpinnerInstance();
            spinnerInstance = null;
            spinnerRect = null;
            spinnerPrefabInUse = null;
        }

        EnsureSpinner();
    }

    private void Awake()
    {
        ResolveButton();
    }

    private void OnDisable()
    {
        CancelHold();
    }

    private void Update()
    {
        if (!isHolding || invoked)
            return;

        heldSeconds += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (heldSeconds >= holdDuration)
            InvokeHeldClick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        ResolveButton();
        if (button == null || !button.IsInteractable())
            return;

        BeginHold(eventData != null ? eventData.pointerId : int.MinValue);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        SuppressNativeClick(eventData);
        CancelHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        CancelHold();
    }

    public void OnCancel(BaseEventData eventData)
    {
        CancelHold();
    }

    private void BeginHold(int pointerId)
    {
        if (isHolding)
            return;

        isHolding = true;
        invoked = false;
        heldSeconds = 0f;
        activePointerId = pointerId;

        ShowSpinner();

        if (holdDuration <= 0f)
            InvokeHeldClick();
    }

    private void InvokeHeldClick()
    {
        if (invoked)
            return;

        invoked = true;
        button.onClick.Invoke();
    }

    private void CancelHold()
    {
        isHolding = false;
        invoked = false;
        heldSeconds = 0f;
        activePointerId = int.MinValue;
        HideSpinner();
    }

    private void ShowSpinner()
    {
        EnsureSpinner();
        if (spinnerInstance == null)
            return;

        spinnerInstance.SetActive(true);
        spinnerInstance.transform.SetAsLastSibling();
        RestartAnimations();
    }

    private void HideSpinner()
    {
        if (spinnerInstance != null)
            spinnerInstance.SetActive(false);
    }

    private void EnsureSpinner()
    {
        if (spinnerInstance != null || spinnerPrefab == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            spinnerInstance = PrefabUtility.InstantiatePrefab(spinnerPrefab, transform) as GameObject;
        else
            spinnerInstance = Instantiate(spinnerPrefab, transform);
#else
        spinnerInstance = Instantiate(spinnerPrefab, transform);
#endif

        if (spinnerInstance == null)
            return;

        spinnerPrefabInUse = spinnerPrefab;
        spinnerInstance.name = spinnerPrefab.name;
        spinnerRect = spinnerInstance.transform as RectTransform;

        if (spinnerRect != null)
        {
            spinnerRect.anchorMin = new Vector2(0.5f, 0.5f);
            spinnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            spinnerRect.pivot = new Vector2(0.5f, 0.5f);
            spinnerRect.anchoredPosition = spinnerAnchoredOffset;
            spinnerRect.localRotation = Quaternion.identity;
            spinnerRect.localScale = Vector3.one;

            if (spinnerSize.x > 0f && spinnerSize.y > 0f)
                spinnerRect.sizeDelta = spinnerSize;
        }

        CanvasGroup canvasGroup = spinnerInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = spinnerInstance.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        foreach (Graphic graphic in spinnerInstance.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        spinnerInstance.SetActive(false);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(spinnerInstance);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void RestartAnimations()
    {
        foreach (Animation animation in spinnerInstance.GetComponentsInChildren<Animation>(true))
        {
            animation.Stop();
            animation.Play();
        }
    }

    private bool IsActivePointer(PointerEventData eventData)
    {
        return activePointerId == int.MinValue ||
               eventData == null ||
               eventData.pointerId == activePointerId;
    }

    private void SuppressNativeClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        eventData.eligibleForClick = false;
    }

    private void ResolveButton()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    private void DestroySpinnerInstance()
    {
        if (spinnerInstance == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(spinnerInstance);
        else
            Destroy(spinnerInstance);
#else
        Destroy(spinnerInstance);
#endif
    }
}
