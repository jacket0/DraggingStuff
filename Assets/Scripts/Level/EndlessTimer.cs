using System;
using UnityEngine;

public sealed class EndlessTimer : TimerSpeedModifierTarget
{
    [SerializeField, Min(1f)] private float _startingTime = 35f;
    [SerializeField, Min(1f)] private float _maximumTime = 45f;
    [SerializeField, Min(0f)] private float _threeItemTimeReward = 2.5f;
    [SerializeField, Min(0f)] private float _fourItemTimeReward = 3.5f;
    [SerializeField, Min(0f)] private float _fiveItemTimeReward = 5f;
    [SerializeField, Min(0.01f)] private float _drainSmoothingDuration = 2f;
    [SerializeField] private AnimationCurve _drainMultiplierByMatchCount;

    private float _remainingTime;
    private float _drainMultiplier = 1f;
    private float _targetDrainMultiplier = 1f;
    private float _drainMultiplierVelocity;
    private bool _isRunning;
    private bool _hasExpired;

    public float RemainingTime => _remainingTime;
    public float MaximumTime => _maximumTime;
    public float DrainMultiplier => _drainMultiplier;
    public float NormalizedTime => _maximumTime > 0f ? Mathf.Clamp01(_remainingTime / _maximumTime) : 0f;

    public event Action<EndlessTimerState> StateChanged;
    public event Action<float> TimeAdded;
    public event Action Expired;

    private void Update()
    {
        if (!_isRunning || _hasExpired)
            return;

        _drainMultiplier = Mathf.SmoothDamp(
            _drainMultiplier,
            _targetDrainMultiplier,
            ref _drainMultiplierVelocity,
            _drainSmoothingDuration,
            Mathf.Infinity,
            Time.deltaTime);

        float elapsedTime = Time.deltaTime * TimerSpeedMultiplier * _drainMultiplier;

        if (elapsedTime <= 0f)
        {
            PublishState();
            return;
        }

        _remainingTime = Mathf.Max(0f, _remainingTime - elapsedTime);
        PublishState();

        if (_remainingTime > 0f)
            return;

        _hasExpired = true;
        _isRunning = false;
        Expired?.Invoke();
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
        _remainingTime = Mathf.Min(_startingTime, _maximumTime);
        _drainMultiplier = EvaluateDrainMultiplier(0);
        _targetDrainMultiplier = _drainMultiplier;
        _drainMultiplierVelocity = 0f;
        _hasExpired = false;
        _isRunning = true;
        PublishState();
    }

    public void StopTimer()
    {
        _isRunning = false;
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

        float previousTime = _remainingTime;
        _remainingTime = Mathf.Min(_maximumTime, _remainingTime + seconds);
        PublishState();

        float addedTime = _remainingTime - previousTime;

        if (addedTime > 0f)
            TimeAdded?.Invoke(addedTime);
    }

    private float EvaluateDrainMultiplier(int matchCount)
    {
        if (_drainMultiplierByMatchCount == null || _drainMultiplierByMatchCount.length == 0)
            return 1f;

        return Mathf.Max(0f, _drainMultiplierByMatchCount.Evaluate(matchCount));
    }

    private void PublishState()
    {
        StateChanged?.Invoke(new EndlessTimerState(_remainingTime, _maximumTime, _drainMultiplier));
    }
}
