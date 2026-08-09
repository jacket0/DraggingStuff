using System;
using System.Collections.Generic;

public readonly struct MoveOutcome
{
    private readonly IReadOnlyList<Shelf> _shelvesToAdvance;

    public bool IsSuccessful { get; }
    public MatchResolution Match { get; }
    public bool HasMatch => Match != null;
    public bool IsLevelCompleted { get; }
    public IReadOnlyList<Shelf> ShelvesToAdvance => _shelvesToAdvance ?? Array.Empty<Shelf>();
    public bool HasLayerTransition => ShelvesToAdvance.Count > 0;

    private MoveOutcome(bool isSuccessful, MatchResolution match, bool isLevelCompleted, IReadOnlyList<Shelf> shelvesToAdvance)
    {
        IsSuccessful = isSuccessful;
        Match = match;
        IsLevelCompleted = isLevelCompleted;
        _shelvesToAdvance = shelvesToAdvance;
    }

    public static MoveOutcome Rejected()
    {
        return new MoveOutcome(false, null, false, Array.Empty<Shelf>());
    }

    public static MoveOutcome Successful(MatchResolution match, bool isLevelCompleted, IReadOnlyList<Shelf> shelvesToAdvance)
    {
        return new MoveOutcome(true, match, isLevelCompleted, shelvesToAdvance);
    }
}