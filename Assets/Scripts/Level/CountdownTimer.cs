using System;
using UnityEngine;

public sealed class CountdownTimer : MonoBehaviour
{
    private float _totalTime;
    private float _remainingTime;
    private float _countdownRate = 1f;
    private bool _hasStarted;
    private bool _isRunning;
    private bool _hasExpired;

    public float TotalTime => _totalTime;
    public float RemainingTime => _remainingTime;
    public float ActiveTime => _totalTime - _remainingTime;
    public bool HasStarted => _hasStarted;
    public bool IsRunning => _isRunning;
    public CountdownTimerState State => CreateState();

    public event Action<CountdownTimerState> StateChanged;
    public event Action Expired;

    private void Update()
    {
        if (!_isRunning || _hasExpired)
            return;

        float elapsedTime = Time.deltaTime * _countdownRate;

        if (elapsedTime <= 0f)
            return;

        _remainingTime = Mathf.Max(0f, _remainingTime - elapsedTime);
        PublishState();

        if (_remainingTime > 0f)
            return;

        _hasExpired = true;
        _isRunning = false;
        Expired?.Invoke();
    }

    public void Configure(float totalTime)
    {
        Configure(totalTime, totalTime);
    }

    public void Configure(float totalTime, float initialTime)
    {
        if (totalTime <= 0f || float.IsNaN(totalTime) || float.IsInfinity(totalTime))
            throw new ArgumentOutOfRangeException(nameof(totalTime));

        if (initialTime <= 0f || initialTime > totalTime || float.IsNaN(initialTime) || float.IsInfinity(initialTime))
            throw new ArgumentOutOfRangeException(nameof(initialTime));

        _totalTime = totalTime;
        _remainingTime = initialTime;
        _countdownRate = 1f;
        _hasStarted = false;
        _isRunning = false;
        _hasExpired = false;
        PublishState();
    }

    public void StartTimer()
    {
        if (_totalTime <= 0f)
            throw new InvalidOperationException("The timer is not configured.");

        if (_hasStarted)
            return;

        _hasStarted = true;
        _isRunning = true;
        PublishState();
    }

    public void Pause()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        PublishState();
    }

    public void Resume()
    {
        if (!_hasStarted || _hasExpired || _isRunning)
            return;

        _isRunning = true;
        PublishState();
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        PublishState();
    }

    public void AddTime(float seconds)
    {
        if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));

        _remainingTime = Mathf.Min(_totalTime, _remainingTime + seconds);
        PublishState();
    }

    public void SetCountdownRate(float countdownRate)
    {
        if (countdownRate < 0f || float.IsNaN(countdownRate) || float.IsInfinity(countdownRate))
            throw new ArgumentOutOfRangeException(nameof(countdownRate));

        _countdownRate = countdownRate;
    }

    private void PublishState()
    {
        StateChanged?.Invoke(CreateState());
    }

    private CountdownTimerState CreateState()
    {
        return new CountdownTimerState(_remainingTime, _totalTime, _hasStarted, _isRunning);
    }
}
