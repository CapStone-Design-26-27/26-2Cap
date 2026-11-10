using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class PlaySafeArea : MonoBehaviour
{
    [SerializeField, Min(0)] private float bottomReservedHeight = 196f;
    [SerializeField] private RectTransform bottomControls;
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
        if (Screen.width <= 0 || Screen.height <= 0) return;
        if (bottomControls != null)
            bottomControls.anchoredPosition = new Vector2(0f, ExtraBottomPadding);
        Rect safe = Screen.safeArea;
        Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        if (content.anchorMin == min && content.anchorMax == max) return;
        content.anchorMin = min;
        content.anchorMax = max;
        content.offsetMin = content.offsetMax = Vector2.zero;
    }

    public void FitVertically(Bounds bounds, float padding, ref Vector2 center, ref float size)
    {
        if (canvas == null || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        float top = safe.yMax / Screen.height;
        float bottom = (safe.yMin + (bottomReservedHeight + ExtraBottomPadding) * canvas.scaleFactor) / Screen.height;
        float available = top - bottom;
        if (available < .25f) return;
        size = Mathf.Max(size, (bounds.extents.y + padding) / available);
        center.y -= (bottom + top - 1f) * size;
    }

    // The authored button edge is 32 canvas units above the bottom. Small screens still get 24 pixels.
    private float ExtraBottomPadding => canvas == null ? 0f : Mathf.Max(0f, 24f / Mathf.Max(canvas.scaleFactor, .01f) - 32f);
}
