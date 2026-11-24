using UnityEngine;
using System.Collections;

public class ToolHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = Color.yellow;
    public float pulseSpeed  = 2.0f;
    public float minIntensity = 0.2f;
    public float maxIntensity = 1.0f;

    private Renderer[] renderers;
    private Color[] originalColors;
    private bool isHighlighting = false;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;
    }

    public void StartHighlight()
    {
        if (isHighlighting) return;
        isHighlighting = true;
        StartCoroutine(PulseHighlight());
    }

    public void StopHighlight()
    {
        isHighlighting = false;
        StopAllCoroutines();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = originalColors[i];
    }

    IEnumerator PulseHighlight()
    {
        while (isHighlighting)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
            Color current = Color.Lerp(Color.white, highlightColor, intensity);
            foreach (var r in renderers)
                r.material.color = current;
            yield return null;
        }
    }
}