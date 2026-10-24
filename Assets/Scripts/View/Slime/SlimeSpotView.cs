using System;
using DG.Tweening;
using UnityEngine;

public sealed class SlimeSpotView : MonoBehaviour
{
    [SerializeField] private Transform _blob;
    [SerializeField, Min(0.01f)] private float _appearDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float _vanishDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float _wobbleDuration = 1.2f;
    [SerializeField, Min(0f)] private float _wobbleStrength = 0.06f;

    private Vector3 _restScale;
    private Tween _transition;
    private Tween _wobble;

    public bool IsVisible => gameObject.activeSelf && _transition == null;
    public bool IsAnimating => _transition != null;

    private void Awake()
    {
        if (_blob == null)
            throw new InvalidOperationException(nameof(_blob));

        _restScale = _blob.localScale;
    }

    private void OnDestroy() => Cancel();

    public void ShowInstant()
    {
        Cancel();
        gameObject.SetActive(true);
        _blob.localScale = _restScale;
        StartWobble();
    }

    public void PlayAppear(Action completed)
    {
        Cancel();
        gameObject.SetActive(true);
        _blob.localScale = Vector3.zero;
        _transition = _blob.DOScale(_restScale, _appearDuration).SetEase(Ease.OutBack).OnComplete(() =>
        {
            _transition = null;
            StartWobble();
            completed?.Invoke();
        });
    }

    public void PlayVanish()
    {
        if (!gameObject.activeSelf)
            return;

        Cancel();
        _transition = _blob.DOScale(Vector3.zero, _vanishDuration).SetEase(Ease.InBack).OnComplete(() =>
        {
            _transition = null;
            gameObject.SetActive(false);
        });
    }

    public void Hide()
    {
        Cancel();
        gameObject.SetActive(false);
    }

    private void StartWobble()
    {
        Vector3 squashedScale = Vector3.Scale(_restScale, new Vector3(1f + _wobbleStrength, 1f - _wobbleStrength, 1f + _wobbleStrength));
        _wobble = _blob.DOScale(squashedScale, _wobbleDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    private void Cancel()
    {
        _transition?.Kill();
        _transition = null;
        _wobble?.Kill();
        _wobble = null;
    }
}
