namespace FireValveSimulator
{
    using EPOOutline;
    using UnityEngine;

    [DisallowMultipleComponent]
    public class ObjectHighlighter : MonoBehaviour
    {
        [Header("Outline Hint")]
        [SerializeField] private Color outlineColor = new Color(1f, 0.85f, 0f, 1f);
        [SerializeField, Range(0f, 1f)] private float dilateShift = 1f;
        [SerializeField, Range(0f, 1f)] private float blurShift = 1f;

        [Header("Legacy Material Fallback")]
        public Material highlightMaterial;

        private Material originalMaterial;
        private Renderer objectRenderer;
        private Outlinable outlinable;
        private bool outlinableWasEnabled;
        private bool initialized;

        private void Awake()
        {
            Initialize();
            RemoveHighlight();
        }

        public void Highlight()
        {
            Initialize();

            if (outlinable != null && outlinable.OutlineTargetsCount > 0)
            {
                outlinable.enabled = true;
                return;
            }

            if (objectRenderer == null || highlightMaterial == null)
                return;

            objectRenderer.material = highlightMaterial;
        }

        public void RemoveHighlight()
        {
            Initialize();

            if (outlinable != null && outlinable.OutlineTargetsCount > 0)
                outlinable.enabled = outlinableWasEnabled;

            if (objectRenderer == null || originalMaterial == null)
                return;

            objectRenderer.material = originalMaterial;
        }

        public bool HasHighlightTarget()
        {
            Initialize();
            return (outlinable != null && outlinable.OutlineTargetsCount > 0) ||
                   (objectRenderer != null && highlightMaterial != null);
        }

        private void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer != null)
                originalMaterial = objectRenderer.material;

            outlinable = GetComponent<Outlinable>();
            bool createdOutlinable = outlinable == null;
            if (createdOutlinable)
                outlinable = gameObject.AddComponent<Outlinable>();

            outlinableWasEnabled = !createdOutlinable && outlinable.enabled;

            if (outlinable.OutlineTargetsCount == 0)
                outlinable.AddAllChildRenderersToRenderingList(RenderersAddingMode.All);

            outlinable.RenderStyle = RenderStyle.Single;
            outlinable.OutlineParameters.Color = outlineColor;
            outlinable.OutlineParameters.DilateShift = dilateShift;
            outlinable.OutlineParameters.BlurShift = blurShift;
            outlinable.enabled = outlinableWasEnabled;
        }
    }
}
