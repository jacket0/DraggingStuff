using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ShelfItem))]
public class ShelfItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ShelfItemDragController _dragController;
    private ShelfItem _item;
    private bool _isDragging;

    private void Awake()
    {
        _item = GetComponent<ShelfItem>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragController = GetComponentInParent<ShelfItemDragController>();

        if (_dragController == null )
            return;

        _isDragging = _dragController.TryBeginDrag(_item, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging)
            return;

        _dragController.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging)
            return;

        _dragController.EndDrag(eventData.position);
        _isDragging = false;
        _dragController = null;
    }
}
