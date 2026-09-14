using System;
using System.Collections.Generic;

public sealed class ColumnStateSnapshot
{
    private static readonly HashSet<ItemType> ValidTypes = new HashSet<ItemType>((ItemType[])Enum.GetValues(typeof(ItemType)));
    public IReadOnlyList<ItemType> Items { get; }
    public int Count => Items.Count;
    public bool IsEmpty => Count == 0;
    public ItemType? FrontItem => IsEmpty ? (ItemType?)null : Items[0];

    public ColumnStateSnapshot(IReadOnlyList<ItemType> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        ItemType[] copy = new ItemType[items.Count];

        for (int index = 0; index < items.Count; index++)
        {
            if (!ValidTypes.Contains(items[index]))
                throw new ArgumentException("A column contains an unknown item type.", nameof(items));

            copy[index] = items[index];
        }

        Items = Array.AsReadOnly(copy);
    }
}
