namespace FireValveSimulator
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using UnityEngine.XR.Interaction.Toolkit.UI;

    [DisallowMultipleComponent]
    public class ReturnToLauncherButton : MonoBehaviour
    {
        private const string LaunchExtraKey = "edusim_data";
        private const string ResultExtraKey = "edusim_result";

#pragma warning disable CS0414
        [SerializeField] private string launcherPackageName = "CheeseTech.Edusim_Launcher";
#pragma warning restore CS0414
        [SerializeField] private string buttonLabel = "Return to launcher";
        [SerializeField] private Vector3 cameraLocalPosition = new Vector3(0.42f, -0.32f, 1.25f);
        [SerializeField] private Vector2 canvasSize = new Vector2(420f, 120f);
        [SerializeField] private float canvasScale = 0.0018f;
        [SerializeField] private bool returnOnEscape = true;
        [SerializeField] private bool openLauncherWhenStartedDirectly = true;
        [SerializeField] private float directLaunchReturnDelaySeconds = 1f;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Button returnButton;
        [SerializeField] private Text labelText;
        [SerializeField] private bool attachToCameraOnStart = true;

        private string receivedToken = "poc-fake-token-12345";
        private string receivedModuleId = "";
        private string startTimeUtc;
        private bool hasLaunchPayload;
        [SerializeField] private Text statusText;

        private void Awake()
        {
            startTimeUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            hasLaunchPayload = ReadLaunchPayload();
        }

        private void Start()
        {
            EnsureEventSystem();
            EnsureButtonHierarchy();
            WireButton();

            if (attachToCameraOnStart)
                AttachButtonToCamera();

            if (openLauncherWhenStartedDirectly && !hasLaunchPayload)
            {
                SetStatus("Open this module from the Edusim launcher.");
                StartCoroutine(OpenLauncherAfterDirectEntryDelay());
            }
        }

        private void Update()
        {
            if (returnOnEscape && Input.GetKeyDown(KeyCode.Escape))
                ReturnToLauncher();
        }

        public void ReturnToLauncher()
        {
            string result = JsonUtility.ToJson(new ResultPayload
            {
                content_id = Application.identifier,
                module_id = receivedModuleId,
                token = receivedToken,
                start_time = startTimeUtc,
                end_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                score = "",
                pass = "",
                status = "returned"
            });

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = up.GetStatic<AndroidJavaObject>("currentActivity");
                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                var launchIntent = pm.Call<AndroidJavaObject>("getLaunchIntentForPackage", launcherPackageName);
                if (launchIntent == null)
                {
                    SetStatus("Launcher not visible: " + launcherPackageName);
                    Debug.LogError("[ReturnToLauncher] Launcher not visible: " + launcherPackageName);
                    return;
                }

                const int FLAG_ACTIVITY_NEW_TASK = 0x10000000;
                const int FLAG_ACTIVITY_CLEAR_TOP = 0x04000000;
                const int FLAG_ACTIVITY_SINGLE_TOP = 0x20000000;
                launchIntent.Call<AndroidJavaObject>("putExtra", ResultExtraKey, result);
                launchIntent.Call<AndroidJavaObject>(
                    "addFlags",
                    FLAG_ACTIVITY_NEW_TASK | FLAG_ACTIVITY_CLEAR_TOP | FLAG_ACTIVITY_SINGLE_TOP);
                activity.Call("startActivity", launchIntent);
                launchIntent.Dispose();
                activity.Call("finish");
            }
            catch (Exception e)
            {
                SetStatus("Return failed: " + e.Message);
                Debug.LogException(e);
            }
#else
            SetStatus("Editor return payload ready");
            Debug.Log("[ReturnToLauncher] " + result);
#endif
        }

        private IEnumerator OpenLauncherAfterDirectEntryDelay()
        {
            if (directLaunchReturnDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(directLaunchReturnDelaySeconds);

            OpenLauncherWithoutResult();
        }

        private void OpenLauncherWithoutResult()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = up.GetStatic<AndroidJavaObject>("currentActivity");
                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                var launchIntent = pm.Call<AndroidJavaObject>("getLaunchIntentForPackage", launcherPackageName);
                if (launchIntent == null)
                {
                    SetStatus("Launcher not visible: " + launcherPackageName);
                    Debug.LogError("[ReturnToLauncher] Launcher not visible: " + launcherPackageName);
                    return;
                }

                const int FLAG_ACTIVITY_NEW_TASK = 0x10000000;
                const int FLAG_ACTIVITY_CLEAR_TOP = 0x04000000;
                const int FLAG_ACTIVITY_SINGLE_TOP = 0x20000000;
                launchIntent.Call<AndroidJavaObject>(
                    "addFlags",
                    FLAG_ACTIVITY_NEW_TASK | FLAG_ACTIVITY_CLEAR_TOP | FLAG_ACTIVITY_SINGLE_TOP);
                activity.Call("startActivity", launchIntent);
                launchIntent.Dispose();
                activity.Call("finish");
            }
            catch (Exception e)
            {
                SetStatus("Open launcher failed: " + e.Message);
                Debug.LogException(e);
            }
#else
            Debug.Log("[ReturnToLauncher] Direct-entry gate would open launcher on Android.");
#endif
        }

        private void AttachButtonToCamera()
        {
            Camera camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning("[ReturnToLauncher] No camera found; return button was not created.");
                return;
            }

            Transform cameraTransform = camera.transform;
            transform.SetParent(cameraTransform, false);
            transform.localPosition = cameraLocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * canvasScale;

            if (canvas != null)
                canvas.worldCamera = camera;
        }

        private void EnsureButtonHierarchy()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1000;

            var rect = GetComponent<RectTransform>();
            if (rect == null)
                rect = gameObject.AddComponent<RectTransform>();
            rect.sizeDelta = canvasSize;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 10f;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
            if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            if (returnButton == null)
                returnButton = GetComponentInChildren<Button>(true);

            if (returnButton == null)
                CreateButton(rect);

            if (labelText != null)
                labelText.text = buttonLabel;
        }

        private void CreateButton(RectTransform parent)
        {
            var buttonObject = new GameObject("Return Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.16f, 0.88f);
            image.raycastTarget = true;

            returnButton = buttonObject.GetComponent<Button>();
            returnButton.targetGraphic = image;
            returnButton.colors = ButtonColors();

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonRect, false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 8f);
            labelRect.offsetMax = new Vector2(-16f, -8f);

            labelText = labelObject.GetComponent<Text>();
            labelText.text = buttonLabel;
            labelText.font = BuiltInFont();
            labelText.fontSize = 30;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.raycastTarget = false;

            var statusObject = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusObject.transform.SetParent(parent, false);

            var statusRect = (RectTransform)statusObject.transform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -8f);
            statusRect.sizeDelta = new Vector2(0f, 40f);

            statusText = statusObject.GetComponent<Text>();
            statusText.text = "";
            statusText.font = BuiltInFont();
            statusText.fontSize = 18;
            statusText.alignment = TextAnchor.UpperCenter;
            statusText.color = new Color(1f, 1f, 1f, 0.9f);
            statusText.raycastTarget = false;
        }

        private void WireButton()
        {
            if (returnButton == null)
                return;

            returnButton.onClick.RemoveListener(ReturnToLauncher);
            if (!HasPersistentReturnListener())
                returnButton.onClick.AddListener(ReturnToLauncher);
        }

        private bool HasPersistentReturnListener()
        {
            int listenerCount = returnButton.onClick.GetPersistentEventCount();
            for (int i = 0; i < listenerCount; i++)
            {
                if (returnButton.onClick.GetPersistentTarget(i) == this &&
                    returnButton.onClick.GetPersistentMethodName(i) == nameof(ReturnToLauncher))
                {
                    return true;
                }
            }

            return false;
        }

        private static ColorBlock ButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.62f, 0.86f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.colorMultiplier = 1f;
            return colors;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static Font BuiltInFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private bool ReadLaunchPayload()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = up.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                string raw = intent.Call<string>("getStringExtra", LaunchExtraKey);
                if (string.IsNullOrEmpty(raw)) return false;

                var payload = JsonUtility.FromJson<LaunchPayload>(raw);
                if (payload != null && !string.IsNullOrEmpty(payload.token))
                    receivedToken = payload.token;
                if (payload != null && !string.IsNullOrEmpty(payload.module_id))
                    receivedModuleId = payload.module_id;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ReturnToLauncher] Could not read launch payload: " + e.Message);
            }
#endif
            return false;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        [Serializable]
#pragma warning disable CS0649
        private class LaunchPayload
        {
            public string content_id;
            public string module_id;
            public string token;
        }
#pragma warning restore CS0649

        [Serializable]
        private class ResultPayload
        {
            public string content_id;
            public string module_id;
            public string token;
            public string start_time;
            public string end_time;
            public string score;
            public string pass;
            public string status;
        }
    }
}
