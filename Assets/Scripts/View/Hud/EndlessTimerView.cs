using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EndlessTimerView : MonoBehaviour
{
    [SerializeField] private EndlessTimer _timer;
    [SerializeField] private TMP_Text _secondsText;
    [SerializeField] private TMP_Text _addedTimeText;
    [SerializeField] private Image _progressImage;
    [SerializeField] private Image _frameImage;
    [SerializeField] private Image _vignetteImage;
    [SerializeField] private Color _normalColor = new Color(1f, 0.72f, 0.1f);
    [SerializeField] private Color _warningColor = new Color(1f, 0.35f, 0.05f);
    [SerializeField] private Color _criticalColor = new Color(0.8f, 0.05f, 0.02f);
    [SerializeField, Min(0f)] private float _minimumPulseSpeed = 1f;
    [SerializeField, Min(0f)] private float _maximumPulseSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float _vignetteStartNormalizedTime = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _vignetteFullNormalizedTime = 0.05f;
    [SerializeField, Range(0f, 1f)] private float _maximumVignetteAlpha = 0.12f;
    [SerializeField, Min(0.01f)] private float _pressureSmoothingDuration = 0.6f;
    [SerializeField, Min(0.01f)] private float _addedTimeAnimationDuration = 0.8f;
    [SerializeField, Min(0f)] private float _addedTimeRiseDistance = 24f;

    private Sequence _addedTimeSequence;
    private Vector2 _addedTimePosition;
    private float _targetPressure;
    private float _visualPressure;
    private float _pressureVelocity;

    private void OnEnable()
    {
        _timer.StateChanged += ApplyState;
        _timer.TimeAdded += HandleTimeAdded;

        if (_addedTimeText != null)
        {
            _addedTimePosition = _addedTimeText.rectTransform.anchoredPosition;
            _addedTimeText.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (_timer != null)
        {
            _timer.StateChanged -= ApplyState;
            _timer.TimeAdded -= HandleTimeAdded;
        }

        _addedTimeSequence?.Kill();
        _addedTimeSequence = null;
    }

    private void Update()
    {
        _visualPressure = Mathf.SmoothDamp(
            _visualPressure,
            _targetPressure,
            ref _pressureVelocity,
            _pressureSmoothingDuration,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        Color timerColor = _visualPressure < 0.5f
            ? Color.Lerp(_normalColor, _warningColor, _visualPressure * 2f)
            : Color.Lerp(_warningColor, _criticalColor, (_visualPressure - 0.5f) * 2f);

        _progressImage.color = timerColor;
        _frameImage.color = timerColor;

        Color vignetteColor = _criticalColor;
        vignetteColor.a = _maximumVignetteAlpha * _visualPressure;
        _vignetteImage.color = vignetteColor;

        float pulseSpeed = Mathf.Lerp(_minimumPulseSpeed, _maximumPulseSpeed, _visualPressure);
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.05f * _visualPressure;
        _frameImage.transform.localScale = Vector3.one * pulse;
    }

    private void ApplyState(EndlessTimerState state)
    {
        _targetPressure = GetPressure(state);
        _secondsText.SetText(state.RemainingTime.ToString("0.0", CultureInfo.InvariantCulture));
        _progressImage.fillAmount = state.NormalizedTime;
    }

    private float GetPressure(EndlessTimerState state)
    {
        float timePressure = Mathf.InverseLerp(
            _vignetteStartNormalizedTime,
            _vignetteFullNormalizedTime,
            state.NormalizedTime);
        float drainPressure = Mathf.InverseLerp(1f, 4f, state.DrainMultiplier);
        return Mathf.Clamp01(timePressure * Mathf.Lerp(0.75f, 1f, drainPressure));
    }

    private void HandleTimeAdded(float addedTime)
    {
        if (_addedTimeText == null)
            return;

        _addedTimeSequence?.Kill();

        RectTransform textTransform = _addedTimeText.rectTransform;
        Color textColor = _normalColor;
        textColor.a = 1f;

        _addedTimeText.SetText($"+{addedTime.ToString("0.0", CultureInfo.InvariantCulture)}");
        _addedTimeText.color = textColor;
        _addedTimeText.gameObject.SetActive(true);
        textTransform.anchoredPosition = _addedTimePosition;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(textTransform.DOAnchorPosY(_addedTimePosition.y + _addedTimeRiseDistance, _addedTimeAnimationDuration));
        sequence.Join(_addedTimeText.DOFade(0f, _addedTimeAnimationDuration).SetDelay(_addedTimeAnimationDuration * 0.25f));
        sequence.OnComplete(() =>
        {
            if (_addedTimeSequence == sequence)
                _addedTimeSequence = null;

            _addedTimeText.gameObject.SetActive(false);
        });

        _addedTimeSequence = sequence;
        sequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }
}
