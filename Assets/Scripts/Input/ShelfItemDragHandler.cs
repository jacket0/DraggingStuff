using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ShelfItem))]
public class ShelfItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ShelfItem _item;
    private LevelInputController _inputController;
    private bool _isDragging;

    private void Awake()
    {
        _item = GetComponent<ShelfItem>();
        _inputController = GetComponentInParent<LevelInputController>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = _inputController.BeginDrag(_item, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging)
            return;

        _inputController.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging)
            return;

        _inputController.EndDrag(eventData.position);
        _isDragging = false;
    }
}
