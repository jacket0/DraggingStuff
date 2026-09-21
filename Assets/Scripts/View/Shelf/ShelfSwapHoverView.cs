using System;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfSwapHoverView : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _segmentDuration = 0.08f;
    [SerializeField, Min(0.1f)] private float _rotationStrength = 3f;

    private ShelfItem _item;
    private Quaternion _initialRotation;
    private Sequence _sequence;

    public bool IsShowing => _item != null && _sequence != null && _sequence.IsActive();

    public void Show(ShelfItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (_item == item)
            return;

        Stop();
        _item = item;
        _initialRotation = item.transform.localRotation;
        Vector3 initialEulerAngles = _initialRotation.eulerAngles;

        _sequence = DOTween.Sequence();
        _sequence.Append(item.transform.DOLocalRotate(initialEulerAngles + Vector3.forward * _rotationStrength, _segmentDuration).SetEase(Ease.InOutSine));
        _sequence.Append(item.transform.DOLocalRotate(initialEulerAngles - Vector3.forward * _rotationStrength, _segmentDuration * 2f).SetEase(Ease.InOutSine));
        _sequence.Append(item.transform.DOLocalRotate(initialEulerAngles, _segmentDuration).SetEase(Ease.InOutSine));
        _sequence.SetLoops(-1, LoopType.Restart);
        _sequence.SetLink(item.gameObject, LinkBehaviour.KillOnDisable);
    }

    public void Stop()
    {
        _sequence?.Kill();
        _sequence = null;

        if (_item != null)
            _item.transform.localRotation = _initialRotation;

        _item = null;
        _initialRotation = Quaternion.identity;
    }

    private void OnDisable()
    {
        Stop();
    }
}
