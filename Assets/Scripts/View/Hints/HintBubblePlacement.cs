using UnityEngine;

public static class HintBubblePlacement
{
    public static void PlaceAt(RectTransform bubble, Camera worldCamera, Vector3 worldPoint, Vector2 offset)
    {
        RectTransform parent = (RectTransform)bubble.parent;
        Canvas canvas = bubble.GetComponentInParent<Canvas>().rootCanvas;
        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = worldCamera.WorldToScreenPoint(worldPoint);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, canvasCamera, out Vector2 localPoint))
            return;

        bubble.anchoredPosition = localPoint - AnchorReference(bubble, parent) + offset;
        KeepInside(bubble, parent);
    }

    // In narrow windows a target near the screen edge would push the bubble out of view.
    private static void KeepInside(RectTransform bubble, RectTransform parent)
    {
        Vector3[] corners = new Vector3[4];
        bubble.GetWorldCorners(corners);
        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        Rect bounds = parent.rect;
        Vector2 shift = new Vector2(
            Mathf.Max(0f, bounds.xMin - min.x) - Mathf.Max(0f, max.x - bounds.xMax),
            Mathf.Max(0f, bounds.yMin - min.y) - Mathf.Max(0f, max.y - bounds.yMax));
        bubble.anchoredPosition += shift;
    }

    private static Vector2 AnchorReference(RectTransform bubble, RectTransform parent)
    {
        Vector2 anchor = (bubble.anchorMin + bubble.anchorMax) * 0.5f;
        Rect rect = parent.rect;
        return new Vector2(rect.xMin + rect.width * anchor.x, rect.yMin + rect.height * anchor.y);
    }
}
