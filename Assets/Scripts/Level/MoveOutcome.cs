using System;
using System.Collections.Generic;

public readonly struct MoveOutcome
{
    private readonly IReadOnlyList<Shelf> _emptiedShelves;

    public bool IsSuccessful { get; }
    public MatchResolution Match { get; }
    public bool HasMatch => Match != null;
    public IReadOnlyList<Shelf> EmptiedShelves => _emptiedShelves ?? Array.Empty<Shelf>();

    private MoveOutcome(bool isSuccessful, MatchResolution match, IReadOnlyList<Shelf> emptiedShelves)
    {
        IsSuccessful = isSuccessful;
        Match = match;
        _emptiedShelves = emptiedShelves;
    }

    public static MoveOutcome Rejected()
    {
        return new MoveOutcome(false, null, Array.Empty<Shelf>());
    }

    public static MoveOutcome Successful(MatchResolution match, IReadOnlyList<Shelf> emptiedShelves)
    {
        return new MoveOutcome(true, match, emptiedShelves);
    }
}
