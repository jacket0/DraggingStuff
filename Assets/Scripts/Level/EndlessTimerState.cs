public readonly struct EndlessTimerState
{
    public float RemainingTime { get; }
    public float MaximumTime { get; }
    public float DrainMultiplier { get; }
    public float NormalizedTime { get; }

    public EndlessTimerState(float remainingTime, float maximumTime, float drainMultiplier)
    {
        RemainingTime = remainingTime;
        MaximumTime = maximumTime;
        DrainMultiplier = drainMultiplier;
        NormalizedTime = maximumTime > 0f ? remainingTime / maximumTime : 0f;
    }
}
