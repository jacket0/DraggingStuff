using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfItemPlacementAnimator : IDisposable
{
    private readonly Dictionary<ShelfItem, Sequence> _animations = new Dictionary<ShelfItem, Sequence>();
    private bool _isDisposed;

    public bool HasAnimations => _animations.Count > 0;
    public bool IsAnimating(ShelfItem item) => item != null && _animations.ContainsKey(item);

    public void Place(ShelfItem item, float duration, Action completed)
    {
        Animate(item, Vector3.zero, Quaternion.identity, Vector3.one, duration, Ease.OutBack, Ease.OutCubic, completed);
    }

    public void Return(ShelfItem item, Vector3 position, Quaternion rotation, Vector3 scale, float duration, Ease ease)
    {
        Animate(item, position, rotation, scale, duration, ease, ease, null);
    }

    public void Complete(ShelfItem item)
    {
        if (item != null && _animations.TryGetValue(item, out Sequence animation))
            animation.Complete(true);
    }

    public void Dispose()
    {
        _isDisposed = true;
        Sequence[] animations = new Sequence[_animations.Count];
        _animations.Values.CopyTo(animations, 0);
        _animations.Clear();

        foreach (Sequence animation in animations)
            animation.Kill();
    }

    private void Animate(
        ShelfItem item,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        float duration,
        Ease positionEase,
        Ease shapeEase,
        Action completed)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ShelfItemPlacementAnimator));

        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (IsAnimating(item))
            throw new InvalidOperationException(nameof(item));

        Transform itemTransform = item.transform;
        Sequence animation = DOTween.Sequence();
        animation.Join(itemTransform.DOLocalMove(position, duration).SetEase(positionEase, 1.25f));
        animation.Join(itemTransform.DOLocalRotateQuaternion(rotation, duration).SetEase(shapeEase));
        animation.Join(itemTransform.DOScale(scale, duration).SetEase(shapeEase));

        void Finish()
        {
            if (!_animations.TryGetValue(item, out Sequence currentAnimation) || currentAnimation != animation)
                return;

            _animations.Remove(item);

            if (item != null)
            {
                itemTransform.localPosition = position;
                itemTransform.localRotation = rotation;
                itemTransform.localScale = scale;
            }

            completed?.Invoke();
        }

        _animations.Add(item, animation);
        animation.OnComplete(Finish);
        animation.OnKill(Finish);
        animation.SetLink(item.gameObject, LinkBehaviour.KillOnDisable);
    }
}
