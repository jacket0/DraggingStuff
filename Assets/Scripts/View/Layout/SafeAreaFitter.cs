using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _appliedSafeArea;
    private Vector2Int _appliedScreenSize;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void OnEnable()
    {
        ApplySafeArea();
    }

    private void Update()
    {
        if (_appliedSafeArea != Screen.safeArea || _appliedScreenSize.x != Screen.width || _appliedScreenSize.y != Screen.height)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safeArea = Screen.safeArea;
        Vector2 anchorMinimum = safeArea.position;
        Vector2 anchorMaximum = safeArea.position + safeArea.size;
        anchorMinimum.x /= Screen.width;
        anchorMinimum.y /= Screen.height;
        anchorMaximum.x /= Screen.width;
        anchorMaximum.y /= Screen.height;

        _rectTransform.anchorMin = anchorMinimum;
        _rectTransform.anchorMax = anchorMaximum;
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
        _appliedSafeArea = safeArea;
        _appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}
