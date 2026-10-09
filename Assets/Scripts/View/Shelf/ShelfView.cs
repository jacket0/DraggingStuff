using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfView : MonoBehaviour
{
    private const float ReturnMaterialSwitchProgress = 0.33f;

    [SerializeField] private List<ShelfColumnView> _columns = new List<ShelfColumnView>();
    [SerializeField] private Vector3 _previewOffset;
    [SerializeField] private Material _previewMaterial;
    [SerializeField] private Material _farPreviewMaterial;
    [SerializeField, Min(1)] private int _visibleDepth = 2;
    [SerializeField, Range(0.1f, 1f)] private float _depthScaleFactor = 1f;
    [SerializeField, Min(0.01f)] private float _moveDuration = 0.3f;
    [SerializeField] private Ease _moveEase = Ease.OutCubic;
    [SerializeField] private Vector3 _conveyorReturnDrop;

    private Sequence _transition;

    public bool IsAnimating => _transition != null;
    public IReadOnlyList<ShelfColumnView> Columns => _columns;

    public void Refresh()
    {
        if (IsAnimating)
            throw new InvalidOperationException("Cannot refresh a shelf during advancement.");

        foreach (ShelfColumnView view in _columns)
        {
            Component front = GetFront(view.Column);

            if (front != null)
            {
                SetPose(front, view.ItemAnchor, Vector3.zero, 1f);
                Presentation(front).ShowFront();
            }

            RefreshDepth(view);
        }
    }

    public void ApplyQueueLayout(Vector3 previewOffset, int visibleDepth, float depthScaleFactor)
    {
        if (visibleDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(visibleDepth));

        if (depthScaleFactor <= 0f || depthScaleFactor > 1f)
            throw new ArgumentOutOfRangeException(nameof(depthScaleFactor));

        _previewOffset = previewOffset;
        _visibleDepth = visibleDepth;
        _depthScaleFactor = depthScaleFactor;
        Refresh();
    }

    public void Advance(Action completed, ShelfItem placedItem = null)
    {
        if (IsAnimating)
            throw new InvalidOperationException("The shelf is already advancing.");

        Sequence transition = DOTween.Sequence();
        _transition = transition;

        foreach (ShelfColumnView view in _columns)
        {
            Component front = GetFront(view.Column);

            if (front != null && front != placedItem)
            {
                AttachFront(view, front);
                Presentation(front).ShowFront();
                transition.Join(front.transform.DOLocalMove(Vector3.zero, _moveDuration).SetEase(_moveEase));
                transition.Join(front.transform.DOLocalRotateQuaternion(Quaternion.identity, _moveDuration).SetEase(_moveEase));
                transition.Join(front.transform.DOScale(Vector3.one, _moveDuration).SetEase(_moveEase));
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

    public void PlayConveyorShift(float duration, Action completed)
    {
        if (IsAnimating)
            throw new InvalidOperationException("The shelf is already animating.");

        Sequence transition = DOTween.Sequence();
        _transition = transition;

        foreach (ShelfColumnView view in _columns)
        {
            if (view.Column.Count >= 2)
                InsertConveyorShift(transition, view, duration);
        }

        transition.AppendInterval(0.001f);
        transition.OnComplete(() =>
        {
            if (_transition != transition)
                return;

            _transition = null;
            Refresh();
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
        ShelfColumn column = view.Column;

        foreach (ShelfItem item in column.Items)
            PlaceInQueue(view, item, column.GetQueueSlot(item));

        foreach (ShelfKey key in column.Keys)
            PlaceInQueue(view, key, column.GetQueueSlot(key));
    }

    private void PlaceInQueue(ShelfColumnView view, Component element, int slot)
    {
        if (slot == 0)
            return;

        SetPose(element, view.ItemAnchor, GetDepthPosition(view, slot), GetDepthScale(slot));
        ShowAtDepth(element, slot);
    }

    private static Component GetFront(ShelfColumn column) => column.FrontKey != null ? column.FrontKey : (Component)column.FrontItem;

    private static void AttachFront(ShelfColumnView view, Component front)
    {
        if (front is ShelfItem item)
            view.AttachFront(item);
        else
            front.transform.SetParent(view.ItemAnchor, true);
    }

    private void InsertConveyorShift(Sequence transition, ShelfColumnView view, float duration)
    {
        IReadOnlyList<ShelfItem> items = view.Column.Items;
        int returningDepth = items.Count - 1;

        for (int depth = 0; depth < returningDepth; depth++)
        {
            ShelfItem item = items[depth];
            ShowAtDepth(item, depth);
            transition.Insert(0f, item.transform.DOLocalMove(GetDepthPosition(view, depth), duration).SetEase(Ease.InOutSine));
            transition.Insert(0f, item.transform.DOScale(Vector3.one * GetDepthScale(depth), duration).SetEase(Ease.InOutSine));
        }

        ShelfItem returningItem = items[returningDepth];
        Vector3 returningPosition = GetDepthPosition(view, returningDepth);
        Vector3 returnDrop = ToAnchorSpace(view, _conveyorReturnDrop);
        Vector3[] returnPath =
        {
            returningItem.transform.localPosition + returnDrop,
            returningPosition + returnDrop,
            returningPosition
        };
        transition.Insert(0f, returningItem.transform.DOLocalPath(returnPath, duration, PathType.CatmullRom).SetEase(Ease.InOutSine));
        transition.Insert(0f, returningItem.transform.DOScale(Vector3.one * GetDepthScale(returningDepth), duration).SetEase(Ease.InOutSine));
        transition.InsertCallback(duration * ReturnMaterialSwitchProgress, () => ShowAtDepth(returningItem, returningDepth));
    }

    private void ShowAtDepth(Component element, int depth)
    {
        if (depth == 0)
            Presentation(element).ShowFront();
        else if (depth < _visibleDepth)
            Presentation(element).ShowPreview(GetPreviewMaterial(depth));
        else
            Presentation(element).Hide();
    }

    private Vector3 GetDepthPosition(ShelfColumnView view, int depth) => ToAnchorSpace(view, _previewOffset * depth);

    private Vector3 ToAnchorSpace(ShelfColumnView view, Vector3 shelfOffset)
    {
        return view.ItemAnchor.InverseTransformVector(transform.TransformVector(shelfOffset));
    }

    private float GetDepthScale(int depth) => Mathf.Pow(_depthScaleFactor, depth);

    private Material GetPreviewMaterial(int depth)
    {
        return depth > 1 && _farPreviewMaterial != null ? _farPreviewMaterial : _previewMaterial;
    }

    private static ShelfItemPresentation Presentation(Component element)
    {
        if (!element.TryGetComponent(out ShelfItemPresentation presentation))
            throw new MissingComponentException($"{element.name}: {nameof(ShelfItemPresentation)}");

        return presentation;
    }

    private static void SetPose(Component element, Transform anchor, Vector3 position, float scale)
    {
        element.transform.SetParent(anchor, false);
        element.transform.localPosition = position;
        element.transform.localRotation = Quaternion.identity;
        element.transform.localScale = Vector3.one * scale;
    }
}
