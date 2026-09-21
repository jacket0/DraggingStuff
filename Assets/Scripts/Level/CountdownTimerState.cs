public readonly struct CountdownTimerState
{
    public float RemainingTime { get; }
    public float TotalTime { get; }
    public float ActiveTime => TotalTime - RemainingTime;
    public float NormalizedTime => TotalTime > 0f ? RemainingTime / TotalTime : 0f;
    public bool HasStarted { get; }
    public bool IsRunning { get; }

    public CountdownTimerState(float remainingTime, float totalTime, bool hasStarted, bool isRunning)
    {
        RemainingTime = remainingTime;
        TotalTime = totalTime;
        HasStarted = hasStarted;
        IsRunning = isRunning;
    }
}
