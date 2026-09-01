using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GenerationBatch
{
    private readonly List<BoardLayerSnapshot>[] _layersByShelf;
    private readonly List<int> _groupSizes = new List<int>();

    public int ShelfCount => _layersByShelf.Length;
    public int ItemCount => _layersByShelf.Sum(layers => layers.Sum(CountItems));
    public int GroupCount => _groupSizes.Count;
    public IReadOnlyList<int> GroupSizes => _groupSizes;

    public GenerationBatch(int shelfCount)
    {
        if (shelfCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(shelfCount));

        _layersByShelf = new List<BoardLayerSnapshot>[shelfCount];

        for (int shelfIndex = 0; shelfIndex < shelfCount; shelfIndex++)
            _layersByShelf[shelfIndex] = new List<BoardLayerSnapshot>();
    }

    public IReadOnlyList<BoardLayerSnapshot> GetLayers(int shelfIndex)
    {
        if (shelfIndex < 0 || shelfIndex >= ShelfCount)
            throw new ArgumentOutOfRangeException(nameof(shelfIndex));

        return _layersByShelf[shelfIndex];
    }

    public void AddLayer(int shelfIndex, BoardLayerSnapshot layer)
    {
        if (shelfIndex < 0 || shelfIndex >= ShelfCount)
            throw new ArgumentOutOfRangeException(nameof(shelfIndex));

        _layersByShelf[shelfIndex].Add(layer ?? throw new ArgumentNullException(nameof(layer)));
    }

    public void RegisterGroup(int size)
    {
        if (size < Shelf.MinimumMatchCapacity || size > Shelf.MaximumCapacity)
            throw new ArgumentOutOfRangeException(nameof(size));

        _groupSizes.Add(size);
    }

    private static int CountItems(BoardLayerSnapshot layer)
    {
        int count = 0;

        foreach (ItemType? item in layer.Items)
        {
            if (item.HasValue)
                count++;
        }

        return count;
    }
}
