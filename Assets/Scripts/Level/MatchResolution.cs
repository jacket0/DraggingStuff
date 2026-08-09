using System;
using System.Collections.Generic;

public class MatchResolution
{
    private readonly IReadOnlyList<ShelfItem> _items;

    public IReadOnlyList<ShelfItem> Items => _items;

    public MatchResolution(IEnumerable<ShelfItem> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        List<ShelfItem> itemList = new List<ShelfItem>(items);

        if (itemList.Count != ShelfLayer.SlotCount)
            throw new ArgumentException(nameof(items));

        if (itemList.Exists(item => item == null))
            throw new ArgumentException(nameof(itemList));

        _items = itemList.AsReadOnly();
    }
}