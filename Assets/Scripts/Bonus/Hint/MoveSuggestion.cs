using System;
using System.Collections.Generic;

public class MoveSuggestion
{
    private readonly IReadOnlyList<ShelfItem> _targetMatchingItems;

    public ShelfSlot SourceSlot { get; }
    public ShelfSlot TargetSlot { get; }
    public IReadOnlyList<ShelfItem> TargetMatchingItems => _targetMatchingItems;

    public MoveSuggestion(ShelfSlot sourceSlot, ShelfSlot targetSlot, IReadOnlyList<ShelfItem> targetMatchingItems)
    {
        SourceSlot = sourceSlot ?? throw new ArgumentNullException(nameof(sourceSlot));
        TargetSlot = targetSlot ?? throw new ArgumentNullException(nameof(targetSlot));

        if (targetMatchingItems == null)
            throw new ArgumentNullException(nameof(targetMatchingItems));

        List<ShelfItem> matchingItems = new List<ShelfItem>(targetMatchingItems);

        if (matchingItems.Exists(item => item == null))
            throw new ArgumentException(nameof(targetMatchingItems));

        _targetMatchingItems = matchingItems.AsReadOnly();
    }
}
