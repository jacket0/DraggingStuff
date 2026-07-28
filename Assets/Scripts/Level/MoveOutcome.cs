public readonly struct MoveOutcome
{
    public bool IsSuccessful { get; }
    public bool HasMatch { get; }
    public bool IsLevelCompleted { get; }

    private MoveOutcome(bool isSuccessful, bool hasMatch, bool isLevelCompleted)
    {
        IsSuccessful = isSuccessful;
        HasMatch = hasMatch;
        IsLevelCompleted = isLevelCompleted;
    }

    public static MoveOutcome Rejected()
    {
        return new MoveOutcome(false, false, false);
    }

    public static MoveOutcome Successful(bool hasMatch, bool isLevelCompleted)
    {
        return new MoveOutcome(true, hasMatch, isLevelCompleted);
    }
}