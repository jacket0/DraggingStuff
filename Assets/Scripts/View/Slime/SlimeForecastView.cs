using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class SlimeForecastView : MonoBehaviour
{
    [SerializeField] private Transform _drop;
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField, Min(0.01f)] private float _pulseDuration = 0.5f;
    [SerializeField, Min(0f)] private float _pulseStrength = 0.25f;

    private Vector3 _restScale;
    private Tween _pulse;

    public int MovesToGrowth { get; private set; }

    private void Awake()
    {
        if (_drop == null)
            throw new InvalidOperationException(nameof(_drop));

        if (_countdownText == null)
            throw new InvalidOperationException(nameof(_countdownText));

        _restScale = _drop.localScale;
    }

    private void OnDestroy() => _pulse?.Kill();

    public void Show(Vector3 position, int movesToGrowth)
    {
        if (movesToGrowth < 1)
            throw new ArgumentOutOfRangeException(nameof(movesToGrowth));

        transform.position = position;
        gameObject.SetActive(true);
        MovesToGrowth = movesToGrowth;
        _countdownText.text = movesToGrowth.ToString();
        StopPulse();

        if (movesToGrowth == 1)
            _pulse = _drop.DOScale(_restScale * (1f + _pulseStrength), _pulseDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    public void Hide()
    {
        StopPulse();
        MovesToGrowth = 0;
        gameObject.SetActive(false);
    }

    private void StopPulse()
    {
        _pulse?.Kill();
        _pulse = null;
        _drop.localScale = _restScale;
    }
}
