public readonly struct ComboState
{
    public int Count { get; }
    public float RemainingTime { get; }
    public float NormalizedTime { get; }

    public bool IsActive => Count > 0;

    public ComboState(int count, float remainingTime, float normalizedTime)
    {
        Count = count;
        RemainingTime = remainingTime;
        NormalizedTime = normalizedTime;
    }
}

