using System;
using DG.Tweening;
using UnityEngine;

public class ShelfItemDragMover : MonoBehaviour
{
    [SerializeField] private Ease _moveEase = Ease.OutCubic;
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _dragRoot;
    [SerializeField, Min(0.01f)] private float _dragOffset = 0.25f;
    [SerializeField, Min(0.01f)] private float _pickupDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float _returnDuration = 0.2f;
    [SerializeField, Min(1f)] private float _pickupScale = 1.08f;
    [SerializeField, Min(0.01f)] private float _placementDuration = 0.2f;

    private ShelfItem _draggedItem;
    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;
    private Plane _dragPlane;
    private Vector3 _pointerOffset;
    private Vector3 _placementWorldPosition;
    private Quaternion _placementWorldRotation;
    private Vector3 _placementLocalScale;
    private Tween _movementTween;

    private void OnDestroy()
    {
        _movementTween?.Kill();
    }

    public bool TryBeginMove(ShelfItem item, Vector2 pointerPosition, bool snapToPointer = false)
    {
        if (item == null || _draggedItem != null)
            return false;

        Vector3 liftedItemPosition = CalculateLiftedItemPosition(item.transform.position);

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

        _pointerOffset = snapToPointer ? Vector3.zero : liftedItemPosition - dragPoint;
        _dragRoot.position = dragPoint + _pointerOffset;

        item.transform.SetParent(_dragRoot, true);
        _draggedItem = item;

        _movementTween?.Kill();
        Sequence pickupSequence = DOTween.Sequence();
        pickupSequence.Join(item.transform.DOLocalMove(Vector3.zero, _pickupDuration).SetEase(_moveEase));
        pickupSequence.Join(item.transform.DOScale(_originalLocalScale * _pickupScale, _pickupDuration).SetEase(Ease.OutCubic));
        _movementTween = pickupSequence;

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

    public void PreparePlacement()
    {
        if (_draggedItem == null)
            throw new InvalidOperationException(nameof(_draggedItem));

        _movementTween?.Kill();
        _placementWorldPosition = _draggedItem.transform.position;
        _placementWorldRotation = _draggedItem.transform.rotation;
        _placementLocalScale = _draggedItem.transform.localScale;
    }

    public void Place(Action completed)
    {
        if (_draggedItem == null)
            throw new InvalidOperationException(nameof(_draggedItem));

        ShelfItem placedItem = _draggedItem;
        placedItem.transform.SetPositionAndRotation(_placementWorldPosition, _placementWorldRotation);
        placedItem.transform.localScale = _placementLocalScale;

        Sequence placementSequence = DOTween.Sequence();
        placementSequence.Join(placedItem.transform.DOLocalMove(Vector3.zero, _placementDuration).SetEase(Ease.OutBack, 1.25f));
        placementSequence.Join(placedItem.transform.DOLocalRotateQuaternion(Quaternion.identity, _placementDuration).SetEase(Ease.OutCubic));
        placementSequence.Join(placedItem.transform.DOScale(Vector3.one, _placementDuration).SetEase(Ease.OutCubic));
        placementSequence.OnComplete(() =>
        {
            placedItem.transform.localPosition = Vector3.zero;
            placedItem.transform.localRotation = Quaternion.identity;
            placedItem.transform.localScale = Vector3.one;
            ClearState();
            completed?.Invoke();
        });

        _movementTween = placementSequence;
    }

    private void ClearState()
    {
        _draggedItem = null;
        _originalParent = null;
        _pointerOffset = Vector3.zero;
        _placementWorldPosition = Vector3.zero;
        _placementWorldRotation = Quaternion.identity;
        _placementLocalScale = Vector3.one;
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

    private Vector3 CalculateLiftedItemPosition(Vector3 itemWorldPosition)
    {
        Vector3 itemScreenPosition = _camera.WorldToScreenPoint(itemWorldPosition);
        float minimumDepth = _camera.nearClipPlane + 0.01f;

        itemScreenPosition.z = Mathf.Max(
            itemScreenPosition.z - _dragOffset,
            minimumDepth);

        return _camera.ScreenToWorldPoint(itemScreenPosition);
    }
}
