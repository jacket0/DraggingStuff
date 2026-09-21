using System;
using System.Linq;
using UnityEngine;

public sealed class EndlessBoardRefiller : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private EndlessGenerationConfig _generationConfig;
    [SerializeField] private ShelfItemCatalog _itemCatalog;
    [SerializeField] private ShelfItemPool _itemPool;

    private System.Random _random;
    private EndlessLayoutGenerator _generator;
    private bool _isInitialized;

    public int HiddenItemCount => _shelfBoard.Shelves.Sum(shelf => shelf.Columns.Sum(column => Math.Max(0, column.Count - 1)));
    public System.Random Random => _random ?? throw new InvalidOperationException();
    public double LastGenerationMilliseconds { get; private set; }

    public void Initialize(int seed)
    {
        if (_isInitialized || _shelfBoard == null || _generationConfig == null || _itemCatalog == null || _itemPool == null)
            throw new InvalidOperationException("Invalid refiller initialization.");

        _shelfBoard.Initialize();
        _random = new System.Random(seed);
        _generator = new EndlessLayoutGenerator(_random, _generationConfig, _itemCatalog);
        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
        Materialize(_generator.GenerateInitial(CreateSnapshot()));
        Materialize(_generator.GenerateRefill(CreateSnapshot(), 0));
        timer.Stop();
        LastGenerationMilliseconds = timer.Elapsed.TotalMilliseconds;
        _shelfBoard.InitializeViews();
        _isInitialized = true;
    }

    public void RefillIfNeeded(int matchCount)
    {
        if (!_isInitialized || HiddenItemCount >= _generationConfig.RefillThresholdItemCount)
            return;

        if (_shelfBoard.HasLockedShelves || _shelfBoard.ItemAnimations.HasAnimations)
            throw new InvalidOperationException("The board must settle before refill.");

        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
        GenerationBatch batch = _generator.GenerateRefill(CreateSnapshot(), matchCount);
        timer.Stop();
        LastGenerationMilliseconds = timer.Elapsed.TotalMilliseconds;
        Materialize(batch);
        _shelfBoard.InitializeViews();
    }

    private BoardSnapshot CreateSnapshot()
    {
        return new BoardSnapshot(_shelfBoard.Shelves.Select(shelf => new BoardShelfSnapshot(
            shelf.Columns.Select(column => new BoardColumnSnapshot(column.Items.Select(item => item.Type).ToArray())).ToArray())).ToArray());
    }

    private void Materialize(GenerationBatch batch)
    {
        for (int shelfIndex = 0; shelfIndex < batch.ShelfCount; shelfIndex++)
        {
            Shelf shelf = _shelfBoard.Shelves[shelfIndex];

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                foreach (ItemType type in batch.GetItems(shelfIndex, columnIndex))
                    shelf.Columns[columnIndex].Append(_itemPool.Get(type));
            }
        }
    }
}
