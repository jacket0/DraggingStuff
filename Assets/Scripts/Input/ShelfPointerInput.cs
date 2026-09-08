using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ShelfPointerInput : MonoBehaviour
{
    private const int MousePointer = -1;
    private const int NoPointer = int.MinValue;

    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private GameSession _levelSession;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private Camera _camera;
    [SerializeField, Min(0f)] private float _selectionPaddingPixels = 48f;

    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

    private ShelfItemTargetResolver _targetResolver;
    private ShelfItem _pressedItem;
    private ShelfItem _hoveredItem;
    private Vector2 _pressPosition;
    private int _activePointerId = NoPointer;
    private bool _ownsDrag;

    public event Action Interacted;

    private void Awake()
    {
        if (_dragController == null)
            throw new InvalidOperationException(nameof(_dragController));

        if (_levelSession == null)
            throw new InvalidOperationException(nameof(_levelSession));
    }

    private void OnEnable()
    {
        _targetResolver = new ShelfItemTargetResolver(_camera, _shelfBoard, _selectionPaddingPixels);
    }

    private void OnDisable()
    {
        CancelPress();
        ClearHover();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelPress();
    }

    private void Update()
    {
        if (!_levelSession.CanInteract || Time.timeScale <= 0f)
        {
            ClearHover();
            CancelPress();
            return;
        }

        if (Input.touchCount > 0)
        {
            ClearHover();
            UpdateTouch();
            return;
        }

        if (_activePointerId >= 0)
            CancelPress();

        UpdateMouse();
    }

    private void UpdateMouse()
    {
        Vector2 pointerPosition = Input.mousePosition;

        if (!_dragController.IsDragging)
            UpdateHover(pointerPosition);
        else
            ClearHover();

        if (Input.GetMouseButtonDown(0))
            BeginPress(MousePointer, pointerPosition);

        if (Input.GetMouseButton(0))
            ContinuePress(pointerPosition);

        if (Input.GetMouseButtonUp(0))
            EndPress(pointerPosition);
    }

    private void UpdateTouch()
    {
        if (_activePointerId == NoPointer)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase != TouchPhase.Began)
                    continue;

                BeginPress(touch.fingerId, touch.position);
                break;
            }
        }

        if (_activePointerId == NoPointer)
            return;

        if (!TryGetTouch(_activePointerId, out Touch activeTouch))
        {
            CancelPress();
            return;
        }

        if (activeTouch.phase == TouchPhase.Moved || activeTouch.phase == TouchPhase.Stationary)
            ContinuePress(activeTouch.position);

        if (activeTouch.phase == TouchPhase.Canceled)
            CancelPress();
        else if (activeTouch.phase == TouchPhase.Ended)
            EndPress(activeTouch.position);
    }

    private void BeginPress(int pointerId, Vector2 pointerPosition)
    {
        if (_activePointerId != NoPointer || IsPointerOverInteractiveUi(pointerId, pointerPosition))
            return;

        _activePointerId = pointerId;
        _pressPosition = pointerPosition;
        _targetResolver.TryResolve(pointerPosition, out _pressedItem);
        Interacted?.Invoke();
    }

    private void ContinuePress(Vector2 pointerPosition)
    {
        if (_activePointerId == NoPointer)
            return;

        if (_ownsDrag)
        {
            _dragController.UpdateDrag(pointerPosition);
            Interacted?.Invoke();
            return;
        }

        if (_pressedItem == null || _dragController.IsDragging)
            return;

        float dragThreshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10f;

        if (Vector2.SqrMagnitude(pointerPosition - _pressPosition) < dragThreshold * dragThreshold)
            return;

        _ownsDrag = _dragController.TryBeginDrag(_pressedItem, _pressPosition);

        if (_ownsDrag)
        {
            ClearHover();
            _dragController.UpdateDrag(pointerPosition);
        }
    }

    private void EndPress(Vector2 pointerPosition)
    {
        if (_activePointerId == NoPointer)
            return;

        if (_ownsDrag)
            _dragController.EndDrag(pointerPosition);

        Interacted?.Invoke();
        ClearPress();
    }

    private void UpdateHover(Vector2 pointerPosition)
    {
        if (IsPointerOverInteractiveUi(MousePointer, pointerPosition))
        {
            ClearHover();
            return;
        }

        _targetResolver.TryResolve(pointerPosition, out ShelfItem item);

        if (item == _hoveredItem)
            return;

        ClearHover();
        _hoveredItem = item;

        if (_hoveredItem != null)
            ShelfItemOutlineView.GetRequired(_hoveredItem).Show();
    }

    private void ClearHover()
    {
        if (_hoveredItem != null)
            ShelfItemOutlineView.GetRequired(_hoveredItem).Hide();

        _hoveredItem = null;
    }

    private void ClearPress()
    {
        _pressedItem = null;
        _activePointerId = NoPointer;
        _ownsDrag = false;
    }

    private void CancelPress()
    {
        if (_ownsDrag)
            _dragController.CancelDrag();

        ClearPress();
    }

    private bool IsPointerOverInteractiveUi(int pointerId, Vector2 pointerPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            pointerId = pointerId,
            position = pointerPosition
        };

        _raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, _raycastResults);

        foreach (RaycastResult result in _raycastResults)
        {
            if (!(result.module is GraphicRaycaster))
                continue;

            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject) != null ||
                ExecuteEvents.GetEventHandler<IBeginDragHandler>(result.gameObject) != null)
                return true;
        }

        return false;
    }

    private static bool TryGetTouch(int fingerId, out Touch touch)
    {
        foreach (Touch candidate in Input.touches)
        {
            if (candidate.fingerId != fingerId)
                continue;

            touch = candidate;
            return true;
        }

        touch = default;
        return false;
    }
}
