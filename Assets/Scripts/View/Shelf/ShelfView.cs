using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfView : MonoBehaviour
{
    [SerializeField] private List<ShelfColumnView> _columns = new List<ShelfColumnView>();
    [SerializeField] private Vector3 _previewOffset;
    [SerializeField] private Material _previewMaterial;
    [SerializeField] private Material _farPreviewMaterial;
    [SerializeField, Min(1)] private int _visibleDepth = 2;
    [SerializeField, Range(0.1f, 1f)] private float _depthScaleFactor = 1f;
    [SerializeField, Min(0.01f)] private float _moveDuration = 0.3f;
    [SerializeField] private Ease _moveEase = Ease.OutCubic;

    private Sequence _transition;

    public bool IsAnimating => _transition != null;
    public IReadOnlyList<ShelfColumnView> Columns => _columns;

    public void Refresh()
    {
        if (IsAnimating)
            throw new InvalidOperationException("Cannot refresh a shelf during advancement.");

        foreach (ShelfColumnView view in _columns)
        {
            if (!view.IsEmpty)
            {
                ShelfItem item = view.FrontItem;
                SetPose(item, view.ItemAnchor, Vector3.zero, 1f);
                Presentation(item).ShowFront();
            }

            RefreshDepth(view);
        }
    }

    public void Advance(Action completed, ShelfItem placedItem = null)
    {
        if (IsAnimating)
            throw new InvalidOperationException("The shelf is already advancing.");

        Sequence transition = DOTween.Sequence();
        _transition = transition;

        foreach (ShelfColumnView view in _columns)
        {
            ShelfItem item = view.FrontItem;

            if (item != null && item != placedItem)
            {
                view.AttachFront(item);
                Presentation(item).ShowFront();
                transition.Join(item.transform.DOLocalMove(Vector3.zero, _moveDuration).SetEase(_moveEase));
                transition.Join(item.transform.DOLocalRotateQuaternion(Quaternion.identity, _moveDuration).SetEase(_moveEase));
                transition.Join(item.transform.DOScale(Vector3.one, _moveDuration).SetEase(_moveEase));
            }

            RefreshDepth(view);
        }

        transition.AppendInterval(0.001f);
        transition.OnComplete(() =>
        {
            if (_transition != transition)
                return;

            _transition = null;
            completed?.Invoke();
        });
        transition.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void Cancel()
    {
        Sequence transition = _transition;
        _transition = null;
        transition?.Kill();
    }

    private void OnDestroy() => Cancel();

    private void RefreshDepth(ShelfColumnView view)
    {
        for (int depth = 1; depth < view.Column.Count; depth++)
        {
            ShelfItem item = view.Column.Items[depth];
            SetPose(item, view.ItemAnchor, _previewOffset * depth, GetDepthScale(depth));

            if (depth < _visibleDepth)
                Presentation(item).ShowPreview(GetPreviewMaterial(depth));
            else
                Presentation(item).Hide();
        }
    }

    private float GetDepthScale(int depth) => Mathf.Pow(_depthScaleFactor, depth);

    private Material GetPreviewMaterial(int depth)
    {
        return depth > 1 && _farPreviewMaterial != null ? _farPreviewMaterial : _previewMaterial;
    }

    private static ShelfItemPresentation Presentation(ShelfItem item)
    {
        if (!item.TryGetComponent(out ShelfItemPresentation presentation))
            throw new MissingComponentException($"{item.name}: {nameof(ShelfItemPresentation)}");

        return presentation;
    }

    private static void SetPose(ShelfItem item, Transform anchor, Vector3 position, float scale)
    {
        item.transform.SetParent(anchor, false);
        item.transform.localPosition = position;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = Vector3.one * scale;
    }
}
