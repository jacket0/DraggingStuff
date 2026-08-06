using UnityEngine;

public class ShelfItemDragMover : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _dragRoot;

    private ShelfItem _draggedItem;

    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;

    private Plane _dragPlane;
    private Vector3 _pointerOffset;

    public bool TryBeginMove(ShelfItem item, Vector2 pointerPosition)
    {
        if (item == null || _draggedItem != null)
            return false;

        var dragPlane = new Plane(_camera.transform.forward, item.transform.position);

        if (!TryGetDragPoint(dragPlane, pointerPosition, out Vector3 dragPoint))
            return false;

        _originalParent = item.transform.parent;
        _originalLocalPosition = item.transform.localPosition;
        _originalLocalRotation = item.transform.localRotation;
        _originalLocalScale = item.transform.localScale;

        _dragPlane = dragPlane;

        _pointerOffset = item.transform.position - dragPoint;
        item.transform.SetParent(_dragRoot, true);
        _draggedItem = item;

        return true;
    }

    public void Move(Vector2 pointerPosition)
    {
        if (_draggedItem == null)
            return;

        if (!TryGetDragPoint(_dragPlane, pointerPosition, out Vector3 dragPoint))
            return;

        _draggedItem.transform.position = dragPoint + _pointerOffset;
    }

    public void Cancel()
    {
        if (!_draggedItem)
            return;

        _draggedItem.transform.SetParent(_originalParent, false);
        _draggedItem.transform.localPosition = _originalLocalPosition;
        _draggedItem.transform.localRotation = _originalLocalRotation;
        _draggedItem.transform.localScale = _originalLocalScale;

        ClearState();
    }

    public void Complete()
    {
        ClearState();
    }

    private void ClearState()
    {
        _draggedItem = null;
        _originalParent = null;
        _pointerOffset = Vector3.zero;
    }

    private bool TryGetDragPoint(Plane dragPlane, Vector2 pointerPosition, out Vector3 dragPoint)
    {
        Ray ray = _camera.ScreenPointToRay(pointerPosition);

        if (dragPlane.Raycast(ray, out float distance))
        {
            dragPoint = ray.GetPoint(distance);
            return true;
        }

        dragPoint = default;
        return false;
    }
}
