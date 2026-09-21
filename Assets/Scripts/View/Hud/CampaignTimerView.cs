using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignTimerView : MonoBehaviour
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private GameObject _startHint;
    [SerializeField] private StarRatingView _availableStars;
    [SerializeField] private Image _frameImage;
    [SerializeField] private Image _clockImage;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _criticalColor = new Color(1f, 0.35f, 0.05f);
    [SerializeField, Range(0f, 1f)] private float _criticalTimeRatio = 0.2f;
    [SerializeField, Min(0.01f)] private float _pulseDuration = 0.55f;
    [SerializeField, Min(1f)] private float _pulseScale = 1.06f;

    private Tween _pulseTween;
    private TimedLevelDefinition _definition;
    private bool _isCritical;

    private void Awake()
    {
        if (_levelSession == null || _timeText == null || _startHint == null || _availableStars == null || _frameImage == null || _clockImage == null)
            throw new System.InvalidOperationException(nameof(CampaignTimerView));
    }

    private void OnEnable()
    {
        _levelSession.SessionStarted += HandleSessionStarted;

        if (_levelSession.CurrentLevel != null)
            HandleSessionStarted();
    }

    private void OnDisable()
    {
        if (_levelSession != null)
        {
            _levelSession.SessionStarted -= HandleSessionStarted;
            _levelSession.Timer.StateChanged -= ApplyState;
        }

        StopPulse();
    }

    private void HandleSessionStarted()
    {
        _levelSession.Timer.StateChanged -= ApplyState;
        _definition = _levelSession.CurrentLevel.Definition;
        _levelSession.Timer.StateChanged += ApplyState;
        ApplyState(_levelSession.Timer.State);
    }

    private void ApplyState(CountdownTimerState state)
    {
        _timeText.SetText(TimeTextFormatter.FormatSeconds(state.RemainingTime));
        _startHint.SetActive(!state.HasStarted);

        long activeTimeMilliseconds = (long)Mathf.Round(state.ActiveTime * 1000f);
        int availableRating = LevelStarCalculator.Calculate(true, activeTimeMilliseconds, _definition);
        _availableStars.SetRating(availableRating);

        bool isCritical = state.HasStarted && state.NormalizedTime <= _criticalTimeRatio && state.RemainingTime > 0f;
        SetCriticalState(isCritical);
    }

    private void SetCriticalState(bool isCritical)
    {
        if (_isCritical == isCritical)
            return;

        _isCritical = isCritical;
        Color color = isCritical ? _criticalColor : _normalColor;
        _frameImage.color = color;
        _clockImage.color = color;
        _timeText.color = color;

        StopPulse();

        if (!isCritical)
            return;

        _pulseTween = transform
            .DOScale(Vector3.one * _pulseScale, _pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void StopPulse()
    {
        _pulseTween?.Kill();
        _pulseTween = null;
        transform.localScale = Vector3.one;
    }
}
