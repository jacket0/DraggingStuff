using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class EndlessBoardRefiller : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private EndlessGenerationConfig _generationConfig;
    [SerializeField] private EndlessItemCatalog _itemCatalog;
    [SerializeField] private ShelfLayerPool _layerPool;
    [SerializeField] private ShelfItemPool _itemPool;

    private System.Random _random;
    private EndlessLayoutGenerator _generator;
    private GenerationBatch _preparedBatch;
    private bool _isInitialized;

    public int HiddenItemCount => CountHiddenItems();
    public System.Random Random => _random ?? throw new InvalidOperationException();
    public double LastGenerationMilliseconds { get; private set; }

    private void OnDestroy()
    {
        if (_shelfBoard == null)
            return;

        foreach (Shelf shelf in _shelfBoard.Shelves)
            shelf.LayerRemoved -= HandleLayerRemoved;
    }

    public void Initialize(int seed)
    {
        ValidateDependencies();

        if (_isInitialized)
            throw new InvalidOperationException();

        _random = new System.Random(seed);
        _generator = new EndlessLayoutGenerator(_random, _generationConfig, _itemCatalog, new EndlessBoardSolver());

        MoveConfiguredLayersToPool();

        foreach (Shelf shelf in _shelfBoard.Shelves)
            shelf.LayerRemoved += HandleLayerRemoved;

        System.Diagnostics.Stopwatch generationTimer = System.Diagnostics.Stopwatch.StartNew();
        GenerationBatch initialBatch = _generator.GenerateInitial(CreateSnapshot());
        Materialize(initialBatch);

        int hiddenGroupCount = _random.Next(
            _generationConfig.MinimumBatchGroupCount,
            _generationConfig.MaximumBatchGroupCount + 1);

        GenerationBatch hiddenBatch = _generator.GenerateRefill(CreateSnapshot(), hiddenGroupCount, 0);
        generationTimer.Stop();
        LastGenerationMilliseconds = generationTimer.Elapsed.TotalMilliseconds;
        Materialize(hiddenBatch);

        _shelfBoard.InitializeViews();
        _isInitialized = true;
    }

    public void PrepareRefill(int matchCount)
    {
        if (!_isInitialized || _preparedBatch != null)
            return;

        if (HiddenItemCount >= _generationConfig.RefillThresholdItemCount)
            return;

        int groupCount = _random.Next(
            _generationConfig.MinimumBatchGroupCount,
            _generationConfig.MaximumBatchGroupCount + 1);
        System.Diagnostics.Stopwatch generationTimer = System.Diagnostics.Stopwatch.StartNew();
        _preparedBatch = _generator.GenerateRefill(CreateSnapshot(), groupCount, matchCount);
        generationTimer.Stop();
        LastGenerationMilliseconds = generationTimer.Elapsed.TotalMilliseconds;
    }

    public void ApplyPreparedRefill(int matchCount)
    {
        PrepareRefill(matchCount);

        if (_preparedBatch == null)
            return;

        GenerationBatch batch = _preparedBatch;
        _preparedBatch = null;
        Materialize(batch);
    }

    public BoardSnapshot CreateSnapshot()
    {
        List<BoardShelfSnapshot> shelves = new List<BoardShelfSnapshot>(_shelfBoard.Shelves.Count);

        foreach (Shelf shelf in _shelfBoard.Shelves)
        {
            List<BoardLayerSnapshot> layers = new List<BoardLayerSnapshot>(shelf.Layers.Count);

            foreach (ShelfLayer layer in shelf.Layers)
            {
                ItemType?[] items = new ItemType?[shelf.Capacity];

                for (int slotIndex = 0; slotIndex < layer.Slots.Count; slotIndex++)
                {
                    ShelfSlot slot = layer.Slots[slotIndex];
                    items[slotIndex] = slot.IsEmpty ? null : slot.Item.Type;
                }

                layers.Add(new BoardLayerSnapshot(items));
            }

            shelves.Add(new BoardShelfSnapshot(shelf.Capacity, layers));
        }

        return new BoardSnapshot(shelves);
    }

    private void Materialize(GenerationBatch batch)
    {
        for (int shelfIndex = 0; shelfIndex < batch.ShelfCount; shelfIndex++)
        {
            Shelf shelf = _shelfBoard.Shelves[shelfIndex];

            foreach (BoardLayerSnapshot layerSnapshot in batch.GetLayers(shelfIndex))
            {
                ShelfLayer layer = _layerPool.Get(shelf.Capacity);

                for (int slotIndex = 0; slotIndex < layerSnapshot.Items.Count; slotIndex++)
                {
                    ItemType? itemType = layerSnapshot.Items[slotIndex];

                    if (!itemType.HasValue)
                        continue;

                    ShelfItem item = _itemPool.Get(itemType.Value);
                    layer.Slots[slotIndex].PlaceItem(item);
                }

                shelf.AppendLayer(layer);
            }
        }
    }

    private int CountHiddenItems()
    {
        int count = 0;

        foreach (Shelf shelf in _shelfBoard.Shelves)
        {
            for (int layerIndex = 1; layerIndex < shelf.Layers.Count; layerIndex++)
            {
                foreach (ShelfSlot slot in shelf.Layers[layerIndex].Slots)
                {
                    if (!slot.IsEmpty)
                        count++;
                }
            }
        }

        return count;
    }

    private void HandleLayerRemoved(ShelfLayer layer)
    {
        _layerPool.Release(layer);
    }

    private void MoveConfiguredLayersToPool()
    {
        foreach (Shelf shelf in _shelfBoard.Shelves)
        {
            IReadOnlyList<ShelfLayer> layers = shelf.DetachLayers();

            foreach (ShelfLayer layer in layers)
                _layerPool.Release(layer);
        }
    }

    private void ValidateDependencies()
    {
        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_generationConfig == null)
            throw new InvalidOperationException(nameof(_generationConfig));

        if (_itemCatalog == null)
            throw new InvalidOperationException(nameof(_itemCatalog));

        if (_layerPool == null)
            throw new InvalidOperationException(nameof(_layerPool));

        if (_itemPool == null)
            throw new InvalidOperationException(nameof(_itemPool));

        if (_shelfBoard.Shelves.Count < 2)
            throw new InvalidOperationException(nameof(_shelfBoard.Shelves));

        HashSet<Shelf> shelves = new HashSet<Shelf>();

        foreach (Shelf shelf in _shelfBoard.Shelves)
        {
            if (shelf == null || !shelves.Add(shelf) || !Shelf.IsValidCapacity(shelf.Capacity))
                throw new InvalidOperationException(nameof(_shelfBoard.Shelves));

            shelf.ValidateLayers();

            if (shelf.Layers.Any(layer => !layer.IsEmpty))
                throw new InvalidOperationException(nameof(shelf.Layers));
        }

        if (!_shelfBoard.Shelves.Any(shelf => shelf.Capacity == Shelf.MinimumMatchCapacity))
            throw new InvalidOperationException("Для бесконечного режима нужна хотя бы одна полка вместимостью 3.");
    }
}
