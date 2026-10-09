using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ShelfColumn
{
    private readonly List<ShelfItem> _items = new List<ShelfItem>();
    private readonly List<QueuedKey> _queuedKeys = new List<QueuedKey>();

    public IReadOnlyList<ShelfItem> Items { get; }
    public IReadOnlyList<ShelfKey> Keys => _queuedKeys.Select(queuedKey => queuedKey.Key).ToArray();
    public ShelfKey FrontKey => _queuedKeys.Count > 0 && _queuedKeys[0].ItemsInFront == 0 ? _queuedKeys[0].Key : null;
    public ShelfItem FrontItem => FrontKey != null || _items.Count == 0 ? null : _items[0];
    public int Count => _items.Count;
    public bool HasKeys => _queuedKeys.Count > 0;
    public bool IsEmpty => Count == 0 && !HasKeys;

    public ShelfColumn()
    {
        Items = _items.AsReadOnly();
    }

    public ShelfItem TakeFront()
    {
        ShelfItem item = FrontItem ?? throw new InvalidOperationException("The column has no front item to take.");
        _items.RemoveAt(0);
        item.Column = null;

        foreach (QueuedKey queuedKey in _queuedKeys)
            queuedKey.ItemsInFront--;

        return item;
    }

    public void PlaceFront(ShelfItem item)
    {
        if (!IsEmpty)
            throw new InvalidOperationException("The target column must be empty.");

        Append(item);
    }

    public void SwapFrontWith(ShelfColumn other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        if (ReferenceEquals(this, other))
            throw new InvalidOperationException("A column cannot swap with itself.");

        ShelfItem item = FrontItem;
        ShelfItem otherItem = other.FrontItem;

        if (item == null || otherItem == null)
            throw new InvalidOperationException("Both columns must contain a front item.");

        _items[0] = otherItem;
        other._items[0] = item;
        otherItem.Column = this;
        item.Column = other;
    }

    public bool MoveFrontToBack()
    {
        if (HasKeys)
            throw new InvalidOperationException("A column with keys cannot move its front item to the back.");

        if (Count < 2)
            return false;

        ShelfItem item = _items[0];
        _items.RemoveAt(0);
        _items.Add(item);
        return true;
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

    public void AddKey(ShelfKey key, int itemsInFront)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        if (itemsInFront < 0 || itemsInFront > Count)
            throw new ArgumentOutOfRangeException(nameof(itemsInFront));

        if (key.Column != null)
            throw new InvalidOperationException("The key already belongs to a column.");

        if (_queuedKeys.Any(queuedKey => queuedKey.ItemsInFront == itemsInFront))
            throw new InvalidOperationException("Two keys cannot share a place in a column.");

        int index = _queuedKeys.Count(queuedKey => queuedKey.ItemsInFront < itemsInFront);
        _queuedKeys.Insert(index, new QueuedKey(key, itemsInFront));
        key.Column = this;
    }

    public ShelfKey TakeFrontKey()
    {
        ShelfKey key = FrontKey ?? throw new InvalidOperationException("The column has no front key to take.");
        _queuedKeys.RemoveAt(0);
        key.Column = null;
        return key;
    }

    public int GetQueueSlot(ShelfItem item)
    {
        int itemIndex = _items.IndexOf(item);

        if (itemIndex < 0)
            throw new ArgumentException("The item does not belong to this column.", nameof(item));

        return itemIndex + _queuedKeys.Count(queuedKey => queuedKey.ItemsInFront <= itemIndex);
    }

    public int GetQueueSlot(ShelfKey key)
    {
        int keyIndex = _queuedKeys.FindIndex(queuedKey => queuedKey.Key == key);

        if (keyIndex < 0)
            throw new ArgumentException("The key does not belong to this column.", nameof(key));

        return _queuedKeys[keyIndex].ItemsInFront + keyIndex;
    }

    public ColumnStateSnapshot CreateSnapshot(Func<Shelf, int> getLockedShelfIndex)
    {
        if (HasKeys && getLockedShelfIndex == null)
            throw new InvalidOperationException("A column with keys needs the board to resolve its locked shelves.");

        return new ColumnStateSnapshot(
            _items.Select(item => item.Type).ToArray(),
            _queuedKeys.Select(queuedKey => new ColumnKeySnapshot(queuedKey.ItemsInFront, getLockedShelfIndex(queuedKey.Key.LockedShelf))).ToArray());
    }

    private sealed class QueuedKey
    {
        public ShelfKey Key { get; }
        public int ItemsInFront { get; set; }

        public QueuedKey(ShelfKey key, int itemsInFront)
        {
            Key = key;
            ItemsInFront = itemsInFront;
        }
    }
}
