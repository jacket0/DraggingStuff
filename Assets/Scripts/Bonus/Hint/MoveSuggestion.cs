using System;
using System.Collections.Generic;

public class MoveSuggestion
{
    private readonly IReadOnlyList<ShelfItem> _targetMatchingItems;

    public ShelfColumnView SourceColumn { get; }
    public ShelfColumnView TargetColumn { get; }
    public IReadOnlyList<ShelfItem> TargetMatchingItems => _targetMatchingItems;

    public MoveSuggestion(ShelfColumnView sourceColumn, ShelfColumnView targetColumn, IReadOnlyList<ShelfItem> targetMatchingItems)
    {
        SourceColumn = sourceColumn ?? throw new ArgumentNullException(nameof(sourceColumn));
        TargetColumn = targetColumn ?? throw new ArgumentNullException(nameof(targetColumn));

        if (targetMatchingItems == null)
            throw new ArgumentNullException(nameof(targetMatchingItems));

        List<ShelfItem> matchingItems = new List<ShelfItem>(targetMatchingItems);

        if (matchingItems.Exists(item => item == null))
            throw new ArgumentException(nameof(targetMatchingItems));

        _targetMatchingItems = matchingItems.AsReadOnly();
    }
}
