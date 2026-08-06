public readonly struct MoveOutcome
{
    public bool IsSuccessful { get; }
    public bool HasMatch { get; }
    public bool IsLevelCompleted { get; }
    public bool HasLayerTransition { get; }

    private MoveOutcome(bool isSuccessful, bool hasMatch, bool isLevelCompleted, bool hasLayerTransition)
    {
        IsSuccessful = isSuccessful;
        HasMatch = hasMatch;
        IsLevelCompleted = isLevelCompleted;
        HasLayerTransition = hasLayerTransition;
    }

    public static MoveOutcome Rejected()
    {
        return new MoveOutcome(false, false, false, false);
    }

    public static MoveOutcome Successful(bool hasMatch, bool isLevelCompleted, bool hasLayerTransition)
    {
        return new MoveOutcome(true, hasMatch, isLevelCompleted, hasLayerTransition);
    }
}