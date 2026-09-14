using System;
using System.Collections.Generic;

public readonly struct MoveOutcome
{
    public bool IsSuccessful => Item != null;
    public ShelfItem Item { get; }
    public ShelfColumn Source { get; }
    public ShelfColumn Target { get; }
    public IReadOnlyList<Shelf> AffectedShelves { get; }

    private MoveOutcome(ShelfItem item, ShelfColumn source, ShelfColumn target, Shelf[] affectedShelves)
    {
        Item = item;
        Source = source;
        Target = target;
        AffectedShelves = Array.AsReadOnly(affectedShelves);
    }

    public static MoveOutcome Rejected() => default;
    public static MoveOutcome Successful(ShelfItem item, ShelfColumn source, ShelfColumn target, Shelf[] affectedShelves)
        => new MoveOutcome(item, source, target, affectedShelves);
}
