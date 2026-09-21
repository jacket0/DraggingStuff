using System;
using UnityEngine;

[RequireComponent(typeof(CountdownTimer))]
public sealed class EndlessTimer : TimerSpeedModifierTarget
{
    [SerializeField, Min(1f)] private float _startingTime = 35f;
    [SerializeField, Min(1f)] private float _maximumTime = 45f;
    [SerializeField, Min(0f)] private float _threeItemTimeReward = 2.5f;
    [SerializeField, Min(0f)] private float _fourItemTimeReward = 3.5f;
    [SerializeField, Min(0f)] private float _fiveItemTimeReward = 5f;
    [SerializeField, Min(0.01f)] private float _drainSmoothingDuration = 2f;
    [SerializeField] private AnimationCurve _drainMultiplierByMatchCount;
    [SerializeField] private CountdownTimer _countdownTimer;

    private float _drainMultiplier = 1f;
    private float _targetDrainMultiplier = 1f;
    private float _drainMultiplierVelocity;

    public float RemainingTime => _countdownTimer.RemainingTime;
    public float MaximumTime => _maximumTime;
    public float DrainMultiplier => _drainMultiplier;
    public float NormalizedTime => _countdownTimer.State.NormalizedTime;

    public event Action<EndlessTimerState> StateChanged;
    public event Action<float> TimeAdded;
    public event Action Expired;

    private void Awake()
    {
        if (_countdownTimer == null)
            _countdownTimer = GetComponent<CountdownTimer>();

        if (_countdownTimer == null)
            throw new InvalidOperationException(nameof(_countdownTimer));
    }

    private void OnEnable()
    {
        _countdownTimer.StateChanged += HandleTimerStateChanged;
        _countdownTimer.Expired += HandleTimerExpired;
    }

    private void OnDisable()
    {
        if (_countdownTimer == null)
            return;

        _countdownTimer.StateChanged -= HandleTimerStateChanged;
        _countdownTimer.Expired -= HandleTimerExpired;
    }

    private void Update()
    {
        _drainMultiplier = Mathf.SmoothDamp(
            _drainMultiplier,
            _targetDrainMultiplier,
            ref _drainMultiplierVelocity,
            _drainSmoothingDuration,
            Mathf.Infinity,
            Time.deltaTime);

        _countdownTimer.SetCountdownRate(TimerSpeedMultiplier * _drainMultiplier);
    }

    private void OnValidate()
    {
        _maximumTime = Mathf.Max(1f, _maximumTime);
        _startingTime = Mathf.Clamp(_startingTime, 1f, _maximumTime);
        _threeItemTimeReward = Mathf.Max(0f, _threeItemTimeReward);
        _fourItemTimeReward = Mathf.Max(0f, _fourItemTimeReward);
        _fiveItemTimeReward = Mathf.Max(0f, _fiveItemTimeReward);
        _drainSmoothingDuration = Mathf.Max(0.01f, _drainSmoothingDuration);
    }

    public void StartTimer()
    {
        _drainMultiplier = EvaluateDrainMultiplier(0);
        _targetDrainMultiplier = _drainMultiplier;
        _drainMultiplierVelocity = 0f;
        _countdownTimer.Configure(_maximumTime, _startingTime);
        _countdownTimer.SetCountdownRate(TimerSpeedMultiplier * _drainMultiplier);
        _countdownTimer.StartTimer();
    }

    public void StopTimer()
    {
        _countdownTimer.Stop();
    }

    public void RegisterMatch(int matchCount, int itemCount)
    {
        if (matchCount < 0)
            throw new ArgumentOutOfRangeException(nameof(matchCount));

        float timeReward = itemCount switch
        {
            3 => _threeItemTimeReward,
            4 => _fourItemTimeReward,
            5 => _fiveItemTimeReward,
            _ => throw new ArgumentOutOfRangeException(nameof(itemCount))
        };

        _targetDrainMultiplier = EvaluateDrainMultiplier(matchCount);
        AddTime(timeReward);
    }

    public void AddTime(float seconds)
    {
        if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));

        float previousTime = _countdownTimer.RemainingTime;
        _countdownTimer.AddTime(seconds);
        float addedTime = _countdownTimer.RemainingTime - previousTime;

        if (addedTime > 0f)
            TimeAdded?.Invoke(addedTime);
    }

    private float EvaluateDrainMultiplier(int matchCount)
    {
        if (_drainMultiplierByMatchCount == null || _drainMultiplierByMatchCount.length == 0)
            return 1f;

        return Mathf.Max(0f, _drainMultiplierByMatchCount.Evaluate(matchCount));
    }

    private void HandleTimerStateChanged(CountdownTimerState state)
    {
        StateChanged?.Invoke(new EndlessTimerState(state.RemainingTime, _maximumTime, _drainMultiplier));
    }

    private void HandleTimerExpired()
    {
        Expired?.Invoke();
    }
}
