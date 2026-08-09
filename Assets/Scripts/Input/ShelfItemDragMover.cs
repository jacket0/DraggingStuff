using DG.Tweening;
using UnityEngine;

public class ShelfItemDragMover : MonoBehaviour
{
    [SerializeField] private Ease _moveEase = Ease.OutCubic;
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _dragRoot;
    [SerializeField, Min(0.01f)] private float _dragOffset = 0.25f;
    [SerializeField, Min(0.01f)] private float _pickupDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float _returnDuration = 0.2f;

    private ShelfItem _draggedItem;
    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;
    private Plane _dragPlane;
    private Vector3 _pointerOffset;
    private Tween _movementTween;

    public bool TryBeginMove(ShelfItem item, Vector2 pointerPosition)
    {
        if (item == null || _draggedItem != null)
            return false;

        Vector3 liftedItemPosition = item.transform.position - _camera.transform.forward * _dragOffset;

        _dragPlane = new Plane(_camera.transform.forward, liftedItemPosition);

        if (!TryGetDragPoint(_dragPlane, pointerPosition, out Vector3 dragPoint))
        {
            _dragPlane = default;
            return false;
        }

        _originalParent = item.transform.parent;
        _originalLocalPosition = item.transform.localPosition;
        _originalLocalRotation = item.transform.localRotation;
        _originalLocalScale = item.transform.localScale;

        _pointerOffset = liftedItemPosition - dragPoint;
        _dragRoot.position = dragPoint + _pointerOffset;

        item.transform.SetParent(_dragRoot, true);
        _draggedItem = item;

        _movementTween?.Kill();
        _movementTween = item.transform.DOLocalMove(Vector3.zero, _pickupDuration).SetEase(_moveEase);

        return true;
    }

    public void Move(Vector2 pointerPosition)
    {
        if (_draggedItem == null)
            return;

        if (!TryGetDragPoint(_dragPlane, pointerPosition, out Vector3 dragPoint))
            return;

        _dragRoot.position = dragPoint + _pointerOffset;
    }

    public void Cancel()
    {
        if (_draggedItem == null)
            return;

        _movementTween?.Kill();

        ShelfItem returningItem = _draggedItem;
        returningItem.transform.SetParent(_originalParent, true);

        Sequence returnSequence = DOTween.Sequence();

        returnSequence.Join(returningItem.transform.DOLocalMove(_originalLocalPosition, _returnDuration).SetEase(_moveEase));
        returnSequence.Join(returningItem.transform.DOLocalRotateQuaternion(_originalLocalRotation, _returnDuration).SetEase(_moveEase));
        returnSequence.Join(returningItem.transform.DOScale(_originalLocalScale, _returnDuration).SetEase(_moveEase));

        returnSequence.OnComplete(() =>
        {
            returningItem.transform.localPosition = _originalLocalPosition;
            returningItem.transform.localRotation = _originalLocalRotation;
            returningItem.transform.localScale = _originalLocalScale;

            ClearState();
        });

        _movementTween = returnSequence;
    }

    public void Complete()
    {
        _movementTween?.Kill();
        ClearState();
    }

    private void ClearState()
    {
        _draggedItem = null;
        _originalParent = null;
        _pointerOffset = Vector3.zero;
        _dragPlane = default;
        _movementTween = null;
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
