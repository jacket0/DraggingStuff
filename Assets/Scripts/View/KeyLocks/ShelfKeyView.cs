using System;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfKeyView : MonoBehaviour
{
    [SerializeField] private Transform _body;
    [SerializeField] private Transform _model;
    [SerializeField] private ParticleSystem _halo;
    [SerializeField] private ParticleSystem _trail;
    [SerializeField] private ParticleSystem _sparks;
    [SerializeField, Min(0.01f)] private float _hopDuration = 0.12f;
    [SerializeField, Min(0f)] private float _hopHeight = 0.12f;
    [SerializeField, Min(0f)] private float _hopPunchScale = 0.3f;
    [SerializeField, Min(0.01f)] private float _flightDuration = 0.4f;
    [SerializeField, Min(0f)] private float _arcHeight = 0.25f;
    [SerializeField, Min(0f)] private float _approachDistance = 0.06f;
    [SerializeField, Min(0f)] private float _insertedDistance = 0.005f;
    [SerializeField, Min(0.01f)] private float _insertDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float _turnDuration = 0.15f;
    [SerializeField] private float _turnAngle = -90f;

    private Sequence _collection;

    private void Awake()
    {
        if (_body == null)
            throw new InvalidOperationException(nameof(_body));

        if (_model == null)
            throw new InvalidOperationException(nameof(_model));

        if (_halo == null)
            throw new InvalidOperationException(nameof(_halo));

        if (_trail == null)
            throw new InvalidOperationException(nameof(_trail));

        if (_sparks == null)
            throw new InvalidOperationException(nameof(_sparks));
    }

    public void PlayCollection(Transform lockPoint, Action inserted)
    {
        if (lockPoint == null)
            throw new ArgumentNullException(nameof(lockPoint));

        Cancel();
        transform.SetParent(null, true);
        transform.position = _body.position;
        _body.localPosition = Vector3.zero;
        Vector3 start = transform.position;
        Vector3 hop = start + Vector3.up * _hopHeight;
        Vector3 approach = lockPoint.position + lockPoint.forward * _approachDistance;
        Vector3 apex = (hop + approach) * 0.5f + Vector3.up * _arcHeight;
        Vector3 insertedPosition = lockPoint.position + lockPoint.forward * _insertedDistance;
        Quaternion insertedRotation = Quaternion.LookRotation(-lockPoint.forward, lockPoint.up);
        Quaternion turnedRotation = insertedRotation * Quaternion.Euler(0f, 0f, _turnAngle);

        Sequence collection = DOTween.Sequence();
        _collection = collection;
        collection.AppendCallback(() => _sparks.Play(true));
        collection.Append(transform.DOMove(hop, _hopDuration).SetEase(Ease.OutQuad));
        collection.Join(_model.DOPunchScale(Vector3.one * _hopPunchScale, _hopDuration, 4, 0.5f));
        collection.AppendCallback(() => _trail.Play(true));
        collection.Append(transform.DOPath(new[] { hop, apex, approach }, _flightDuration, PathType.CatmullRom).SetEase(Ease.InOutSine));
        collection.Join(transform.DORotateQuaternion(insertedRotation, _flightDuration).SetEase(Ease.InOutSine));
        collection.Join(_model.DOLocalRotate(new Vector3(0f, 360f, 0f), _flightDuration, RotateMode.LocalAxisAdd).SetEase(Ease.OutSine));
        collection.AppendCallback(() =>
        {
            _trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _halo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        });
        collection.Append(transform.DOMove(insertedPosition, _insertDuration).SetEase(Ease.InQuad));
        collection.Append(transform.DORotateQuaternion(turnedRotation, _turnDuration).SetEase(Ease.OutBack));
        collection.OnComplete(() =>
        {
            if (_collection != collection)
                return;

            _collection = null;
            transform.SetParent(lockPoint, true);
            inserted?.Invoke();
        });
        collection.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void Cancel()
    {
        Sequence collection = _collection;
        _collection = null;
        collection?.Kill();
    }

    private void OnDestroy() => Cancel();
}
