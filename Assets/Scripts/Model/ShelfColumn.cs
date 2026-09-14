using System;
using System.Collections.Generic;

public sealed class ShelfColumn
{
    private readonly List<ShelfItem> _items = new List<ShelfItem>();

    public IReadOnlyList<ShelfItem> Items { get; }
    public ShelfItem FrontItem => IsEmpty ? null : _items[0];
    public int Count => _items.Count;
    public bool IsEmpty => Count == 0;

    public ShelfColumn()
    {
        Items = _items.AsReadOnly();
    }

    public ShelfItem TakeFront()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Cannot take an item from an empty column.");

        ShelfItem item = _items[0];
        _items.RemoveAt(0);
        item.Column = null;
        return item;
    }

    public void PlaceFront(ShelfItem item)
    {
        if (!IsEmpty)
            throw new InvalidOperationException("The target column must be empty.");

        Append(item);
    }

    public void Append(ShelfItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (item.Column != null)
            throw new InvalidOperationException("The item already belongs to a column.");

        _items.Add(item);
        item.Column = this;
    }
}
