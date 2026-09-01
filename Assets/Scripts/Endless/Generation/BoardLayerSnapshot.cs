using System;
using System.Collections.Generic;

public sealed class BoardLayerSnapshot
{
    private readonly ItemType?[] _items;

    public IReadOnlyList<ItemType?> Items => _items;

    public BoardLayerSnapshot(IReadOnlyList<ItemType?> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        if (!Shelf.IsValidCapacity(items.Count))
            throw new ArgumentException(nameof(items));

        _items = new ItemType?[items.Count];

        for (int index = 0; index < items.Count; index++)
            _items[index] = items[index];
    }
}
