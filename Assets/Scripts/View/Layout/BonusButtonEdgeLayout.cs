using System;
using System.Collections.Generic;
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
    [SerializeField, Min(0.1f)] private float _preferredScale = 1.1f;

    private RectTransform _rectTransform;
    private HorizontalLayoutGroup _layoutGroup;
    private Rect _appliedSafeArea;
    private Vector2Int _appliedScreenSize;
    private readonly List<Rect> _slotBounds = new List<Rect>();
    private Matrix4x4 _appliedCameraMatrix;
    private bool _layoutPending = true;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _layoutGroup = GetComponent<HorizontalLayoutGroup>();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        _layoutPending = true;
    }

    private void LateUpdate()
    {
        Matrix4x4 cameraMatrix = _worldCamera.projectionMatrix * _worldCamera.worldToCameraMatrix;

        if (_layoutPending || _appliedSafeArea != Screen.safeArea || _appliedScreenSize.x != Screen.width ||
            _appliedScreenSize.y != Screen.height || _appliedCameraMatrix != cameraMatrix)
        {
            Canvas.ForceUpdateCanvases();
            ApplyLayout();
            _appliedCameraMatrix = cameraMatrix;
            _layoutPending = false;
        }
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
        CollectSlotBounds(_boardGap * canvasScaleFactor);
        float panelScale = CalculatePanelScale(screenPosition, preferredSize * canvasScaleFactor);

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

    private float CalculatePanelScale(Vector2 position, Vector2 size)
    {
        float minimumScale = 0f;
        float maximumScale = _preferredScale;

        if (Fits(new Rect(position, size * maximumScale)))
            return maximumScale;

        for (int iteration = 0; iteration < 12; iteration++)
        {
            float scale = (minimumScale + maximumScale) * 0.5f;

            if (Fits(new Rect(position, size * scale)))
                minimumScale = scale;
            else
                maximumScale = scale;
        }

        return minimumScale;
    }

    private bool Fits(Rect panel)
    {
        if (panel.xMax > Screen.safeArea.xMax || panel.yMax > Screen.safeArea.yMax)
            return false;

        foreach (Rect columnView in _slotBounds)
        {
            if (panel.Overlaps(columnView))
                return false;
        }

        return true;
    }

    private void CollectSlotBounds(float padding)
    {
        _slotBounds.Clear();

        foreach (Shelf shelf in _shelfBoard.Shelves)
        {
            if (shelf == null || !shelf.gameObject.activeInHierarchy)
                continue;

            if (shelf.Capacity == 0)
                continue;

            foreach (ShelfColumnView columnView in shelf.ColumnViews)
            {
                if (columnView == null || !columnView.gameObject.activeInHierarchy)
                    continue;

                BoxCollider slotCollider = columnView.GetComponent<BoxCollider>();

                if (slotCollider == null || !slotCollider.enabled)
                    continue;

                if (!TryProjectBounds(slotCollider, out Rect bounds))
                    continue;

                bounds.min -= Vector2.one * padding;
                bounds.max += Vector2.one * padding;
                _slotBounds.Add(bounds);
            }
        }
    }

    private bool TryProjectBounds(BoxCollider slotCollider, out Rect rectangle)
    {
        Vector3 center = slotCollider.center;
        Vector3 extents = slotCollider.size * 0.5f;
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 screenPoint = _worldCamera.WorldToScreenPoint(slotCollider.transform.TransformPoint(corner));

                    if (screenPoint.z > 0f)
                    {
                        minimum = Vector2.Min(minimum, screenPoint);
                        maximum = Vector2.Max(maximum, screenPoint);
                    }
                }
            }
        }

        rectangle = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        return minimum.x <= maximum.x && minimum.y <= maximum.y;
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
