using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class FreezeBonusTimerView : MonoBehaviour
{
    [SerializeField] private GameSession _session;
    [SerializeField] private TimerFreezeBonusEffect _freezeEffect;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private Color _criticalColor = new Color(0.886f, 0.271f, 0.227f);
    [SerializeField, Min(0.1f)] private float _criticalTime = 1.5f;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float _pulseDuration = 0.3f;
    [SerializeField, Min(1f)] private float _pulseScale = 1.1f;

    private Color _normalColor;
    private Tween _fadeTween;
    private Tween _pulseTween;
    private bool _isVisible;
    private bool _isPulsing;

    private void Awake()
    {
        if (_session == null || _freezeEffect == null || _canvasGroup == null || _timeText == null)
            throw new InvalidOperationException(nameof(FreezeBonusTimerView));

        _normalColor = _timeText.color;
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void OnDisable()
    {
        _fadeTween?.Kill();
        _fadeTween = null;
        _isVisible = false;
        _canvasGroup.alpha = 0f;
        StopPulse();
    }

    private void Update()
    {
        float remainingTime = _freezeEffect.RemainingFreezeTime;
        bool isActive = remainingTime > 0f && _session.State != LevelState.Won && _session.State != LevelState.Lost;
        SetVisible(isActive);

        if (!isActive)
        {
            StopPulse();
            return;
        }

        _timeText.SetText("{0:1}", remainingTime);
        float criticalProgress = 1f - Mathf.Clamp01(remainingTime / _criticalTime);
        _timeText.color = Color.Lerp(_normalColor, _criticalColor, criticalProgress);

        if (criticalProgress > 0f)
            StartPulse();
    }

    private void SetVisible(bool isVisible)
    {
        if (_isVisible == isVisible)
            return;

        _isVisible = isVisible;
        _fadeTween?.Kill();
        _fadeTween = _canvasGroup.DOFade(isVisible ? 1f : 0f, _fadeDuration).SetLink(gameObject);
    }

    private void StartPulse()
    {
        if (_isPulsing)
            return;

        _isPulsing = true;
        _pulseTween = _timeText.transform
            .DOScale(Vector3.one * _pulseScale, _pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void StopPulse()
    {
        if (!_isPulsing)
            return;

        _isPulsing = false;
        _pulseTween?.Kill();
        _pulseTween = null;
        _timeText.transform.localScale = Vector3.one;
    }
}
