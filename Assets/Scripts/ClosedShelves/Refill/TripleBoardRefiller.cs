using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TripleBoardRefiller : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private ShelfItemPool _itemPool;

    private TripleRefillGenerator _generator;
    private bool _isInitialized;

    public void Initialize(int seed, ShelfRefillSettings settings, IReadOnlyList<ItemType> levelTypes)
    {
        if (_isInitialized || _shelfBoard == null || _itemPool == null)
            throw new InvalidOperationException("Invalid refiller initialization.");

        _generator = new TripleRefillGenerator(new System.Random(seed), settings, levelTypes);
        _isInitialized = true;
    }

    public IReadOnlyList<Shelf> RefillIfNeeded(bool isRefillActive, IReadOnlyDictionary<ItemType, int> remainingRequiredCounts, int emptyColumnReserve)
    {
        BoardStateSnapshot snapshot = _shelfBoard.CreateSnapshot();

        if (!_generator.ShouldRefill(snapshot, isRefillActive))
            return Array.Empty<Shelf>();

        GenerationBatch batch = _generator.GenerateRefill(snapshot, remainingRequiredCounts, emptyColumnReserve);
        return GenerationBatchMaterializer.Append(_shelfBoard, batch, _itemPool);
    }

    public void RevealShelf(Shelf shelf, int revealedGroupCount)
    {
        Dictionary<ItemType, int> onBoardCounts = new Dictionary<ItemType, int>();

        foreach (ShelfStateSnapshot shelfSnapshot in _shelfBoard.CreateSnapshot().Shelves)
        {
            foreach (ColumnStateSnapshot column in shelfSnapshot.Columns)
            {
                foreach (ItemType type in column.Items)
                    onBoardCounts[type] = onBoardCounts.TryGetValue(type, out int existing) ? existing + 1 : 1;
            }
        }

        IReadOnlyList<IReadOnlyList<ItemType>> columns = _generator.CreateRevealedShelf(revealedGroupCount, onBoardCounts);

        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            foreach (ItemType type in columns[columnIndex])
                shelf.Columns[columnIndex].Append(_itemPool.Get(type));
        }
    }
}
