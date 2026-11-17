using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class PlaySafeArea : MonoBehaviour
{
    [SerializeField, Min(0)] private float bottomReservedHeight = 196f;
    [SerializeField] private bool fitDeviceSafeArea = true;
    private RectTransform content;
    private Canvas canvas;

    private void Awake()
    {
        content = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        Apply();
    }

    private void Update() => Apply();

    private void Apply()
    {
        if (!fitDeviceSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        if (content.anchorMin == min && content.anchorMax == max) return;
        content.anchorMin = min;
        content.anchorMax = max;
    }

    public void FitVertically(Bounds bounds, float padding, ref Vector2 center, ref float size,
        bool reserveOverviewHudSpace = false)
    {
        if (canvas == null || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        float top = safe.yMax / Screen.height;
        float bottom = (safe.yMin + bottomReservedHeight * canvas.scaleFactor) / Screen.height;
        if (reserveOverviewHudSpace)
        {
            const float topHudHeight = 212f;
            const float gapPixels = 24f;
            top -= (topHudHeight * canvas.scaleFactor + gapPixels) / Screen.height;
            bottom += gapPixels / Screen.height;
        }
        float available = top - bottom;
        if (available < .25f) return;
        size = Mathf.Max(size, (bounds.extents.y + padding) / available);
        center.y -= (bottom + top - 1f) * size;
    }

}
