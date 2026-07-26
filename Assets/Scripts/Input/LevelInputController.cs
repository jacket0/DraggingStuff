using UnityEngine;

public class LevelInputController : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _dragRoot;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private LayerMask _slotLayerMask;

    private ShelfItem _draggedItem;
    private ShelfSlot _sourceSlot;

    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;    

    private Plane _dragPlane;
    private Vector3 _pointerOffset;

    public bool BeginDrag(ShelfItem item, Vector2 pointerPosition)
    {
        if (item == null || _draggedItem != null)
            return false;

        ShelfSlot sourceSlot = item.GetComponentInParent<ShelfSlot>();

        if (sourceSlot == null || sourceSlot.Item != item)
            return false;

        _originalParent = item.transform.parent;
        _originalLocalPosition = item.transform.localPosition;
        _originalLocalRotation = item.transform.localRotation;
        _originalLocalScale = item.transform.localScale;

        _dragPlane = new Plane(_camera.transform.forward, item.transform.position);

        if (TryGetDragPoint(pointerPosition, out Vector3 dragPoint))
        {
            _pointerOffset = item.transform.position - dragPoint;
            item.transform.SetParent(_dragRoot, true);

            _draggedItem = item;
            _sourceSlot = sourceSlot;

            return true;
        }

        return false;
    }

    public void UpdateDrag(Vector2 pointerPosition)
    {
        if (_draggedItem == null)
            return;

        if (!TryGetDragPoint(pointerPosition, out Vector3 dragPoint))
            return;

        _draggedItem.transform.position = dragPoint + _pointerOffset;
    }

    public void EndDrag(Vector2 pointerPosition)
    {
        if (_draggedItem == null)
            return;

        if (!TryFindTargetSlot(pointerPosition, out ShelfSlot targetSlot))
        {
            CancelDrag();
            return;
        }

        if (!_shelfBoard.TryMove(_sourceSlot, targetSlot))
        {
            CancelDrag();
            return;
        }

        ClearDragState();
    }

    private void CancelDrag()
    {
        _draggedItem.transform.SetParent(_originalParent, false);
        _draggedItem.transform.localPosition = _originalLocalPosition;
        _draggedItem.transform.localRotation = _originalLocalRotation;
        _draggedItem.transform.localScale = _originalLocalScale;

        ClearDragState();
    }

    private bool TryGetDragPoint(Vector2 pointerPosition, out Vector3 dragPoint)
    {
        Ray ray = _camera.ScreenPointToRay(pointerPosition);

        if (_dragPlane.Raycast(ray, out float distance))
        {
            dragPoint = ray.GetPoint(distance);
            return true;
        }

        dragPoint = default;
        return false;
    }

    private void ClearDragState()
    {
        _draggedItem = null;
        _sourceSlot = null;
        _originalParent = null;
        _pointerOffset = Vector3.zero;
    }

    private bool TryFindTargetSlot(Vector2 pointerPosition, out ShelfSlot targetSlot)
    {
        Ray ray = _camera.ScreenPointToRay(pointerPosition);

        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _slotLayerMask, QueryTriggerInteraction.Collide);

        if (!hasHit)
        {
            targetSlot = null;
            return false;
        }

        targetSlot = hit.collider.GetComponentInParent<ShelfSlot>();
        return targetSlot != null;
    }
}
