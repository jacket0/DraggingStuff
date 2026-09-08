using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ShelfItem))]
public class ShelfItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const int NoPointer = int.MinValue;

    private ShelfItemDragController _dragController;
    private ShelfItem _item;
    private bool _isDragging;
    private int _activePointerId = NoPointer;

    private void Awake()
    {
        _item = GetComponent<ShelfItem>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isDragging || eventData.button != PointerEventData.InputButton.Left)
            return;

        ShelfItemDragController dragController = GetComponentInParent<ShelfItemDragController>();

        if (dragController == null)
            return;

        ShelfPointerInput pointerInput = dragController.GetComponent<ShelfPointerInput>();

        if (pointerInput != null && pointerInput.isActiveAndEnabled)
            return;

        if (!dragController.TryBeginDrag(_item, eventData.pressPosition))
            return;

        _dragController = dragController;
        _activePointerId = eventData.pointerId;
        _isDragging = true;
        _dragController.UpdateDrag(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        _dragController.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        ShelfItemDragController dragController = _dragController;
        ClearDragState();
        dragController.EndDrag(eventData.position);
    }

    private bool IsActivePointer(PointerEventData eventData)
    {
        if (!_isDragging || eventData.button != PointerEventData.InputButton.Left || eventData.pointerId != _activePointerId)
            return false;

        if (_dragController != null)
            return true;

        ClearDragState();
        return false;
    }

    private void ClearDragState()
    {
        _isDragging = false;
        _dragController = null;
        _activePointerId = NoPointer;
    }
}
