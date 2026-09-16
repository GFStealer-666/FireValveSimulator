namespace FireValveSimulator.Editor
{
    using UnityEditor;
    using UnityEditor.Events;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;
    using UnityEngine.XR.Interaction.Toolkit.UI;

    public static class EdusimReturnButtonSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/Scene_MainScene.unity";
        private const string RootName = "Edusim Return To Launcher";
        private const string LauncherPackageName = "CheeseTech.Edusim_Launcher";

        [MenuItem("Edusim/Install Return To Launcher Button")]
        public static void InstallIntoMainScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InstallIntoOpenScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[EdusimReturnButton] Installed return button into " + ScenePath);
        }

        private static void InstallIntoOpenScene(Scene scene)
        {
            ReturnToLauncherButton controller =
                Object.FindAnyObjectByType<ReturnToLauncherButton>(FindObjectsInactive.Include);

            GameObject root = controller != null ? controller.gameObject : new GameObject(RootName, typeof(RectTransform));
            root.name = RootName;
            root.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : root.layer;
            SceneManager.MoveGameObjectToScene(root, scene);

            RectTransform rootRect = EnsureComponent<RectTransform>(root);
            rootRect.SetParent(null, false);
            rootRect.localPosition = new Vector3(6.1f, 4.0f, 6.8f);
            rootRect.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rootRect.localScale = Vector3.one * 0.0018f;
            rootRect.sizeDelta = new Vector2(420f, 120f);

            Canvas canvas = EnsureComponent<Canvas>(root);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = EnsureComponent<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 10f;

            EnsureComponent<GraphicRaycaster>(root);
            EnsureComponent<TrackedDeviceGraphicRaycaster>(root);

            if (controller == null)
                controller = root.AddComponent<ReturnToLauncherButton>();

            Button button = FindOrCreateButton(rootRect);
            Text label = FindOrCreateText(button.transform as RectTransform, "Label", "Return to launcher", 30, TextAnchor.MiddleCenter);
            Text status = FindOrCreateText(rootRect, "Status", "", 18, TextAnchor.UpperCenter);

            RectTransform statusRect = status.transform as RectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -8f);
            statusRect.sizeDelta = new Vector2(0f, 40f);

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("launcherPackageName").stringValue = LauncherPackageName;
            serialized.FindProperty("buttonLabel").stringValue = "Return to launcher";
            serialized.FindProperty("cameraLocalPosition").vector3Value = new Vector3(0.42f, -0.32f, 1.25f);
            serialized.FindProperty("canvasSize").vector2Value = new Vector2(420f, 120f);
            serialized.FindProperty("canvasScale").floatValue = 0.0018f;
            serialized.FindProperty("returnOnEscape").boolValue = true;
            serialized.FindProperty("canvas").objectReferenceValue = canvas;
            serialized.FindProperty("returnButton").objectReferenceValue = button;
            serialized.FindProperty("labelText").objectReferenceValue = label;
            serialized.FindProperty("attachToCameraOnStart").boolValue = true;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, controller.ReturnToLauncher);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(label);
            EditorUtility.SetDirty(status);
        }

        private static Button FindOrCreateButton(RectTransform parent)
        {
            Button button = parent.GetComponentInChildren<Button>(true);
            GameObject buttonObject;
            if (button != null)
            {
                buttonObject = button.gameObject;
            }
            else
            {
                buttonObject = new GameObject("Return Button", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.layer = parent.gameObject.layer;
                buttonObject.transform.SetParent(parent, false);
                button = buttonObject.GetComponent<Button>();
            }

            RectTransform rect = buttonObject.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.16f, 0.88f);
            image.raycastTarget = true;

            button.targetGraphic = image;
            button.colors = ButtonColors();
            return button;
        }

        private static Text FindOrCreateText(RectTransform parent, string name, string text, int fontSize, TextAnchor alignment)
        {
            Transform existing = parent.Find(name);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.layer = parent.gameObject.layer;
            textObject.transform.SetParent(parent, false);

            RectTransform rect = EnsureComponent<RectTransform>(textObject);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 8f);
            rect.offsetMax = new Vector2(-16f, -8f);

            Text label = EnsureComponent<Text>(textObject);
            label.text = text;
            label.font = BuiltInFont();
            label.fontSize = fontSize;
            label.fontStyle = fontSize >= 30 ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
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

        private static Font BuiltInFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
