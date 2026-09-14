using System;
using System.Collections.Generic;

public sealed class GenerationBatch
{
    private readonly List<ItemType>[][] _items;
    public int ShelfCount => _items.Length;

    public GenerationBatch(BoardSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        _items = new List<ItemType>[snapshot.Shelves.Count][];

        for (int shelfIndex = 0; shelfIndex < _items.Length; shelfIndex++)
        {
            _items[shelfIndex] = new List<ItemType>[snapshot.Shelves[shelfIndex].Capacity];

            for (int columnIndex = 0; columnIndex < _items[shelfIndex].Length; columnIndex++)
                _items[shelfIndex][columnIndex] = new List<ItemType>();
        }
    }

    public IReadOnlyList<ItemType> GetItems(int shelfIndex, int columnIndex) => _items[shelfIndex][columnIndex].AsReadOnly();
    public void Append(int shelfIndex, int columnIndex, ItemType type) => _items[shelfIndex][columnIndex].Add(type);
}
