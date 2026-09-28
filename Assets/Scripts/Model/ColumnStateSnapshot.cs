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

    public ColumnStateSnapshot MoveFrontToBack()
    {
        if (Count < 2)
            return this;

        ItemType[] items = new ItemType[Count];

        for (int index = 1; index < Count; index++)
            items[index - 1] = Items[index];

        items[Count - 1] = Items[0];
        return new ColumnStateSnapshot(items);
    }
}
