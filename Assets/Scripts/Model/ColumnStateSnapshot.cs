using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ColumnStateSnapshot
{
    private static readonly HashSet<ItemType> ValidTypes = new HashSet<ItemType>((ItemType[])Enum.GetValues(typeof(ItemType)));
    private static readonly IReadOnlyList<ColumnKeySnapshot> NoKeys = Array.AsReadOnly(Array.Empty<ColumnKeySnapshot>());

    public IReadOnlyList<ItemType> Items { get; }
    public IReadOnlyList<ColumnKeySnapshot> Keys { get; }
    public int Count => Items.Count;
    public bool HasKeys => Keys.Count > 0;
    public bool HasFrontKey => HasKeys && Keys[0].ItemsInFront == 0;
    public bool IsEmpty => Count == 0 && !HasKeys;
    public ItemType? FrontItem => HasFrontKey || Count == 0 ? (ItemType?)null : Items[0];

    public ColumnStateSnapshot(IReadOnlyList<ItemType> items) : this(items, NoKeys)
    {
    }

    public ColumnStateSnapshot(IReadOnlyList<ItemType> items, IReadOnlyList<ColumnKeySnapshot> keys)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        ItemType[] copy = new ItemType[items.Count];

        for (int index = 0; index < items.Count; index++)
        {
            if (!ValidTypes.Contains(items[index]))
                throw new ArgumentException("A column contains an unknown item type.", nameof(items));

            copy[index] = items[index];
        }

        Items = Array.AsReadOnly(copy);
        Keys = NormalizeKeys(keys, copy.Length);
    }

    public ColumnStateSnapshot RemoveFrontItem()
    {
        if (FrontItem == null)
            throw new InvalidOperationException("The column has no front item to remove.");

        ItemType[] items = new ItemType[Count - 1];

        for (int index = 0; index < items.Length; index++)
            items[index] = Items[index + 1];

        return new ColumnStateSnapshot(items, HasKeys ? Keys.Select(key => key.WithItemsInFront(key.ItemsInFront - 1)).ToArray() : NoKeys);
    }

    public ColumnStateSnapshot RemoveFrontKey(out ColumnKeySnapshot key)
    {
        if (!HasFrontKey)
            throw new InvalidOperationException("The column has no front key to remove.");

        key = Keys[0];
        return new ColumnStateSnapshot(Items, Keys.Skip(1).ToArray());
    }

    public ColumnStateSnapshot ReplaceFrontItem(ItemType type)
    {
        if (FrontItem == null)
            throw new InvalidOperationException("The column has no front item to replace.");

        ItemType[] items = new ItemType[Count];
        items[0] = type;

        for (int index = 1; index < items.Length; index++)
            items[index] = Items[index];

        return new ColumnStateSnapshot(items, Keys);
    }

    public ColumnStateSnapshot MoveFrontToBack()
    {
        if (HasKeys)
            throw new InvalidOperationException("A column with keys cannot move its front item to the back.");

        if (Count < 2)
            return this;

        ItemType[] items = new ItemType[Count];

        for (int index = 1; index < Count; index++)
            items[index - 1] = Items[index];

        items[Count - 1] = Items[0];
        return new ColumnStateSnapshot(items);
    }

    private static IReadOnlyList<ColumnKeySnapshot> NormalizeKeys(IReadOnlyList<ColumnKeySnapshot> keys, int itemCount)
    {
        if (keys.Count == 0)
            return NoKeys;

        if (keys.Any(key => key == null))
            throw new ArgumentException("A column contains a null key.", nameof(keys));

        ColumnKeySnapshot[] sortedKeys = keys.OrderBy(key => key.ItemsInFront).ToArray();

        if (sortedKeys[sortedKeys.Length - 1].ItemsInFront > itemCount)
            throw new ArgumentException("A key cannot stand beyond the end of its column.", nameof(keys));

        for (int index = 1; index < sortedKeys.Length; index++)
        {
            if (sortedKeys[index].ItemsInFront == sortedKeys[index - 1].ItemsInFront)
                throw new ArgumentException("Two keys cannot share a place in a column.", nameof(keys));
        }

        return Array.AsReadOnly(sortedKeys);
    }
}
