using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class SlimeSourceView : MonoBehaviour
{
    [SerializeField] private List<Transform> _blobs = new List<Transform>();
    [SerializeField] private Vector3 _retreatOffset = new Vector3(0f, -0.15f, 0f);
    [SerializeField] private Vector3 _pulse = new Vector3(0f, 0.05f, 0f);
    [SerializeField, Min(0.01f)] private float _pulseDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float _wobbleDuration = 1.6f;
    [SerializeField, Min(0f)] private float _wobbleStrength = 0.08f;
    [SerializeField, Min(0.01f)] private float _retreatDuration = 0.6f;
    [SerializeField, Min(0f)] private float _retreatStagger = 0.04f;

    private readonly List<Vector3> _restScales = new List<Vector3>();
    private readonly List<Vector3> _restPositions = new List<Vector3>();
    private readonly List<Tween> _tweens = new List<Tween>();

    public bool IsShown => gameObject.activeSelf;

    private void Awake()
    {
        if (_blobs.Count == 0 || _blobs.Contains(null))
            throw new InvalidOperationException(nameof(_blobs));

        foreach (Transform blob in _blobs)
        {
            _restScales.Add(blob.localScale);
            _restPositions.Add(blob.localPosition);
        }
    }

    private void OnDestroy() => KillTweens();

    public void ShowInstant()
    {
        KillTweens();
        gameObject.SetActive(true);

        for (int index = 0; index < _blobs.Count; index++)
        {
            _blobs[index].localScale = _restScales[index];
            _blobs[index].localPosition = _restPositions[index];
            Vector3 squashedScale = Vector3.Scale(_restScales[index], new Vector3(1f + _wobbleStrength, 1f - _wobbleStrength, 1f + _wobbleStrength));
            _tweens.Add(_blobs[index].DOScale(squashedScale, _wobbleDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(index * 0.13f % _wobbleDuration));
        }
    }

    public void PlayPulse(Vector3 worldPosition)
    {
        if (!gameObject.activeSelf)
            return;

        Transform nearest = FindNearest(worldPosition);
        nearest.DOPunchPosition(_pulse, _pulseDuration, 4, 0.6f).SetLink(nearest.gameObject, LinkBehaviour.KillOnDisable);
    }

    public void PlayRetreat(float delay)
    {
        if (!gameObject.activeSelf)
            return;

        KillTweens();

        for (int index = 0; index < _blobs.Count; index++)
        {
            Transform blob = _blobs[index];
            float blobDelay = delay + index * _retreatStagger;
            _tweens.Add(blob.DOLocalMove(_restPositions[index] + _retreatOffset, _retreatDuration).SetDelay(blobDelay).SetEase(Ease.InBack));
            _tweens.Add(blob.DOScale(Vector3.zero, _retreatDuration).SetDelay(blobDelay).SetEase(Ease.InBack));
        }

        _tweens.Add(DOVirtual.DelayedCall(delay + _blobs.Count * _retreatStagger + _retreatDuration, () => gameObject.SetActive(false)));
    }

    public void Hide()
    {
        KillTweens();
        gameObject.SetActive(false);
    }

    private Transform FindNearest(Vector3 worldPosition)
    {
        Transform nearest = _blobs[0];

        foreach (Transform blob in _blobs)
        {
            if ((blob.position - worldPosition).sqrMagnitude < (nearest.position - worldPosition).sqrMagnitude)
                nearest = blob;
        }

        return nearest;
    }

    private void KillTweens()
    {
        foreach (Tween tween in _tweens)
            tween?.Kill();

        _tweens.Clear();
    }
}
