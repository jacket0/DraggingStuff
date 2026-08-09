using UnityEngine;

public class ShelfItemDragController : MonoBehaviour
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private ShelfItemDragMover _dragMover;
    [SerializeField] private ShelfSlotRaycaster _slotRaycaster;

    private ShelfSlot _sourceSlot;
    private bool _isDragging;

    public bool TryBeginDrag(ShelfItem item, Vector2 pressScreenPosition)
    {
        if (!_levelSession.IsPlaying || item == null || _isDragging)
            return false;

        ShelfSlot sourceSlot = item.GetComponentInParent<ShelfSlot>();

        if (sourceSlot == null || sourceSlot.Item != item)
            return false;

        if (!_dragMover.TryBeginMove(item, pressScreenPosition))
            return false;

        _sourceSlot = sourceSlot;
        _isDragging = true;

        return true;
    }

    public void UpdateDrag(Vector2 pointerPosition)
    {
        if (!_isDragging) return;

        _dragMover.Move(pointerPosition);
    }

    public void EndDrag(Vector2 pointerPosition)
    {
        if (!_isDragging) return;

        if (!_slotRaycaster.TryGetSlot(pointerPosition, out ShelfSlot targetSlot))
        {
            CancelDrag();
            return;
        }

        MoveOutcome outcome = _levelSession.TryMove(_sourceSlot, targetSlot);

        if (!outcome.IsSuccessful)
        {
            CancelDrag();
            return;
        }

        _dragMover.Complete();
        ClearDragState();
    }

    private void CancelDrag()
    {
        _dragMover.Cancel();
        ClearDragState();
    }

    private void ClearDragState()
    {
        _sourceSlot = null;
        _isDragging = false;
    }
}
