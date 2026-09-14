using System;
using UnityEngine;

public class ShelfItemDragController : MonoBehaviour
{
    [SerializeField] private GameSession _levelSession;
    [SerializeField] private ShelfItemDragMover _dragMover;
    [SerializeField] private ShelfColumnRaycaster _columnRaycaster;

    private ShelfColumnView _sourceColumn;
    private bool _isDragging;

    public event Action<ShelfItem> DragStarting;
    public event Action InteractionOccurred;

    public bool IsDragging => _isDragging;

    private void Awake()
    {
        _dragMover.Initialize(_levelSession.ItemAnimations);
    }

    private void OnEnable()
    {
        _levelSession.StateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_levelSession != null)
            _levelSession.StateChanged -= HandleStateChanged;
        CancelDrag();
    }

    public bool TryBeginDrag(ShelfItem item, Vector2 pressScreenPosition)
    {
        if (!_levelSession.CanInteract || item == null || _isDragging)
            return false;

        ShelfColumnView sourceColumn = item.GetComponentInParent<ShelfColumnView>();

        if (sourceColumn == null || sourceColumn.FrontItem != item || !_levelSession.TryBeginDrag(sourceColumn))
            return false;

        Vector3 positionBeforeDragStarting = item.transform.position;
        DragStarting?.Invoke(item);
        bool snapToPointer = (item.transform.position - positionBeforeDragStarting).sqrMagnitude > Mathf.Epsilon;

        if (!_dragMover.TryBeginMove(item, pressScreenPosition, snapToPointer))
        {
            _levelSession.EndDrag();
            return false;
        }

        _sourceColumn = sourceColumn;
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

        if (!_columnRaycaster.TryGetColumn(_sourceColumn, pointerPosition, out ShelfColumnView targetColumn))
        {
            CancelDrag();
            return;
        }

        _dragMover.PreparePlacement();
        MoveOutcome outcome = _levelSession.TryStartMove(_sourceColumn, targetColumn, out Action completePlacement);

        if (!outcome.IsSuccessful)
        {
            CancelDrag();
            return;
        }

        _dragMover.Place(completePlacement);
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
        if (_levelSession != null)
            _levelSession.EndDrag();

        _sourceColumn = null;
        _isDragging = false;
    }

    private void HandleStateChanged(LevelState state)
    {
        if (state != LevelState.Playing)
            CancelDrag();
    }
}
