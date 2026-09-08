using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(HorizontalLayoutGroup))]
public sealed class BonusButtonEdgeLayout : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private Camera _worldCamera;
    [SerializeField, Min(0f)] private float _edgeInset = 32f;
    [SerializeField, Min(0f)] private float _bottomInset = 32f;
    [SerializeField, Min(0f)] private float _boardGap = 24f;

    private RectTransform _rectTransform;
    private HorizontalLayoutGroup _layoutGroup;
    private Rect _appliedSafeArea;
    private Vector2Int _appliedScreenSize;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _layoutGroup = GetComponent<HorizontalLayoutGroup>();
        ValidateDependencies();
        ApplyLayout();
    }

    private void OnEnable()
    {
        ApplyLayout();
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        ApplyLayout();
    }

    private void LateUpdate()
    {
        if (_appliedSafeArea != Screen.safeArea || _appliedScreenSize.x != Screen.width || _appliedScreenSize.y != Screen.height)
            ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_rectTransform == null || _layoutGroup == null || _rectTransform.childCount == 0)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        float canvasScaleFactor = canvas != null ? canvas.scaleFactor : 1f;
        Rect safeArea = Screen.safeArea;
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransform parent = _rectTransform.parent as RectTransform;

        if (parent == null)
            throw new InvalidOperationException(nameof(parent));

        Vector2 screenPosition = safeArea.min + new Vector2(_edgeInset, _bottomInset) * canvasScaleFactor;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, canvasCamera, out Vector2 localPosition);

        Vector2 preferredSize = CalculatePreferredSize();
        float availableWidth = CalculateAvailableWidth(screenPosition.x, canvasScaleFactor);
        float panelScale = preferredSize.x > 0f
            ? Mathf.Clamp01(availableWidth / (preferredSize.x * canvasScaleFactor))
            : 1f;

        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.zero;
        _rectTransform.pivot = Vector2.zero;
        _rectTransform.anchoredPosition = localPosition - parent.rect.min;
        _rectTransform.sizeDelta = preferredSize;
        _rectTransform.localScale = new Vector3(panelScale, panelScale, 1f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);

        _appliedSafeArea = safeArea;
        _appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
    }

    private Vector2 CalculatePreferredSize()
    {
        float width = _layoutGroup.padding.horizontal;
        float height = 0f;
        int buttonCount = 0;

        foreach (RectTransform button in _rectTransform)
        {
            if (!button.gameObject.activeSelf)
                continue;

            width += LayoutUtility.GetPreferredWidth(button);
            height = Mathf.Max(height, LayoutUtility.GetPreferredHeight(button));
            buttonCount++;
        }

        width += _layoutGroup.spacing * Mathf.Max(0, buttonCount - 1);
        height += _layoutGroup.padding.vertical;
        return new Vector2(width, height);
    }

    private float CalculateAvailableWidth(float panelLeft, float canvasScaleFactor)
    {
        float boardLeft = CalculateBoardLeft();
        float panelRightLimit = Mathf.Min(Screen.safeArea.xMax, boardLeft - _boardGap * canvasScaleFactor);
        return Mathf.Max(0f, panelRightLimit - panelLeft);
    }

    private float CalculateBoardLeft()
    {
        float boardLeft = Screen.safeArea.xMax;

        foreach (Shelf shelf in _shelfBoard.Shelves)
        {
            if (shelf == null || !shelf.gameObject.activeInHierarchy)
                continue;

            Renderer[] renderers = shelf.GetComponentsInChildren<Renderer>(false);

            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled)
                    continue;

                boardLeft = Mathf.Min(boardLeft, CalculateRendererLeft(renderer));
            }
        }

        return boardLeft;
    }

    private float CalculateRendererLeft(Renderer renderer)
    {
        Bounds bounds = renderer.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float left = Screen.safeArea.xMax;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 screenPoint = _worldCamera.WorldToScreenPoint(corner);

                    if (screenPoint.z > 0f)
                        left = Mathf.Min(left, screenPoint.x);
                }
            }
        }

        return left;
    }

    private void ValidateDependencies()
    {
        if (_layoutGroup == null)
            throw new MissingComponentException(nameof(HorizontalLayoutGroup));

        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_worldCamera == null)
            throw new InvalidOperationException(nameof(_worldCamera));
    }
}
