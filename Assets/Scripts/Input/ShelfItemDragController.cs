using System;
using UnityEngine;

public class ShelfItemDragController : MonoBehaviour
{
    [SerializeField] private GameSession _levelSession;
    [SerializeField] private ShelfItemDragMover _dragMover;
    [SerializeField] private ShelfSlotRaycaster _slotRaycaster;

    private ShelfSlot _sourceSlot;
    private bool _isDragging;

    public event Action<ShelfItem> DragStarting;
    public event Action InteractionOccurred;

    public bool IsDragging => _isDragging;

    private void OnDisable()
    {
        CancelDrag();
    }

    public bool TryBeginDrag(ShelfItem item, Vector2 pressScreenPosition)
    {
        if (!_levelSession.CanInteract || item == null || _isDragging)
            return false;

        ShelfSlot sourceSlot = item.GetComponentInParent<ShelfSlot>();

        if (sourceSlot == null || sourceSlot.Item != item)
            return false;

        Vector3 positionBeforeDragStarting = item.transform.position;
        DragStarting?.Invoke(item);
        bool snapToPointer = (item.transform.position - positionBeforeDragStarting).sqrMagnitude > Mathf.Epsilon;

        if (!_dragMover.TryBeginMove(item, pressScreenPosition, snapToPointer))
            return false;

        _sourceSlot = sourceSlot;
        _isDragging = true;
        InteractionOccurred?.Invoke();

        return true;
    }

    public void UpdateDrag(Vector2 pointerPosition)
    {
        if (!_isDragging) return;

        _dragMover.Move(pointerPosition);
        InteractionOccurred?.Invoke();
    }

    public void EndDrag(Vector2 pointerPosition)
    {
        if (!_isDragging) return;

        if (!_slotRaycaster.TryGetSlot(_sourceSlot, pointerPosition, out ShelfSlot targetSlot))
        {
            CancelDrag();
            return;
        }

        _dragMover.PreparePlacement();
        MoveOutcome outcome = _levelSession.TryStartMove(_sourceSlot, targetSlot);

        if (!outcome.IsSuccessful)
        {
            CancelDrag();
            return;
        }

        _dragMover.Place(_levelSession.CompleteMovePlacement);
        ClearDragState();
    }

    public void CancelDrag()
    {
        if (!_isDragging)
            return;

        _dragMover.Cancel();
        InteractionOccurred?.Invoke();
        ClearDragState();
    }

    private void ClearDragState()
    {
        _sourceSlot = null;
        _isDragging = false;
    }
}
