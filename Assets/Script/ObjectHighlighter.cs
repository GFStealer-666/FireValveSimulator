using UnityEngine;

public class ObjectHighlighter : MonoBehaviour
{
    private Material originalMaterial;
    public Material highlightMaterial;

    private Renderer objectRenderer;

    private void Awake()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
            originalMaterial = objectRenderer.material;
    }

    public void Highlight()
    {
        if (objectRenderer == null || highlightMaterial == null)
            return;

        objectRenderer.material = highlightMaterial;
    }

    public void RemoveHighlight()
    {
        if (objectRenderer == null || originalMaterial == null)
            return;

        objectRenderer.material = originalMaterial;
    }
}
