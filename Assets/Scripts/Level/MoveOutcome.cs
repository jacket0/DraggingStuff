using System;
using System.Collections.Generic;

public readonly struct MoveOutcome
{
    public bool IsSuccessful => Item != null;
    public ShelfItem Item { get; }
    public ShelfItem DisplacedItem { get; }
    public ShelfColumn Source { get; }
    public ShelfColumn Target { get; }
    public IReadOnlyList<Shelf> AffectedShelves { get; }
    public bool IsSwap => DisplacedItem != null;

    private MoveOutcome(ShelfItem item, ShelfItem displacedItem, ShelfColumn source, ShelfColumn target, Shelf[] affectedShelves)
    {
        Item = item;
        DisplacedItem = displacedItem;
        Source = source;
        Target = target;
        AffectedShelves = Array.AsReadOnly(affectedShelves);
    }

    public static MoveOutcome Rejected() => default;
    public static MoveOutcome Successful(ShelfItem item, ShelfColumn source, ShelfColumn target, Shelf[] affectedShelves)
        => new MoveOutcome(item, null, source, target, affectedShelves);
    public static MoveOutcome SuccessfulSwap(ShelfItem item, ShelfItem displacedItem, ShelfColumn source, ShelfColumn target, Shelf[] affectedShelves)
        => new MoveOutcome(item, displacedItem, source, target, affectedShelves);
}
