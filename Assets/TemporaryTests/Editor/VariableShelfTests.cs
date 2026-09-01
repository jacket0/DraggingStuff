using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class VariableShelfTests
{
    private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int index = _objects.Count - 1; index >= 0; index--)
        {
            if (_objects[index] != null)
                UnityEngine.Object.DestroyImmediate(_objects[index]);
        }

        _objects.Clear();
    }

    [TestCase(1)]
    [TestCase(2)]
    public void SmallLayerNeverMatches(int capacity)
    {
        ShelfLayer layer = CreateLayer(Enumerable.Repeat<ItemType?>(ItemType.Ball, capacity).ToArray());

        Assert.That(layer.HasMatch(), Is.False);
    }

    [Test]
    public void SmallShelfAcceptsMoveWithoutMatch()
    {
        Shelf sourceShelf = CreateShelf(1, CreateLayer(ItemType.Ball));
        Shelf targetShelf = CreateShelf(2, CreateLayer(null, null));
        ShelfBoard board = CreateBoard(sourceShelf, targetShelf);

        MoveOutcome outcome = board.TryMove(sourceShelf.ActiveLayer.Slots[0], targetShelf.ActiveLayer.Slots[1]);

        Assert.That(outcome.IsSuccessful, Is.True);
        Assert.That(outcome.HasMatch, Is.False);
        Assert.That(sourceShelf.ActiveLayer.Slots[0].IsEmpty, Is.True);
        Assert.That(targetShelf.ActiveLayer.Slots[1].Item.Type, Is.EqualTo(ItemType.Ball));
    }

    [Test]
    public void DetachLayersReturnsConfiguredLayersAndClearsShelf()
    {
        ShelfLayer activeLayer = CreateLayer(null, null, null);
        ShelfLayer previewLayer = CreateLayer(null, null, null);
        Shelf shelf = CreateShelf(3, activeLayer, previewLayer);

        IReadOnlyList<ShelfLayer> detachedLayers = shelf.DetachLayers();

        Assert.That(detachedLayers, Is.EqualTo(new[] { activeLayer, previewLayer }));
        Assert.That(shelf.Layers, Is.Empty);
        Assert.That(shelf.HasActiveLayer, Is.False);
    }

    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void FullMatchingLayerCreatesMatch(int capacity)
    {
        ShelfLayer layer = CreateLayer(Enumerable.Repeat<ItemType?>(ItemType.Bear, capacity).ToArray());

        MatchResolution match = layer.TakeMatch();

        Assert.That(match.Items.Count, Is.EqualTo(capacity));
        Assert.That(layer.IsEmpty, Is.True);
    }

    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void PartialLayerDoesNotMatch(int capacity)
    {
        ItemType?[] items = Enumerable.Repeat<ItemType?>(ItemType.Plant, capacity).ToArray();
        items[capacity - 1] = null;

        Assert.That(CreateLayer(items).HasMatch(), Is.False);
    }

    [Test]
    public void MixedFullLayerDoesNotMatch()
    {
        ShelfLayer layer = CreateLayer(ItemType.Lamp, ItemType.Lamp, ItemType.Ball, ItemType.Lamp);

        Assert.That(layer.HasMatch(), Is.False);
    }

    [TestCase(3, 100)]
    [TestCase(4, 200)]
    [TestCase(5, 300)]
    public void ScoreUsesMatchSize(int matchSize, long expectedScore)
    {
        ScoreSystem scoreSystem = CreateComponent<ScoreSystem>("ScoreSystem");
        MatchResolution match = new MatchResolution(CreateItems(matchSize, ItemType.Ball));

        long earnedScore = scoreSystem.RegisterMatch(match, 1);

        Assert.That(earnedScore, Is.EqualTo(expectedScore));
        Assert.That(scoreSystem.CurrentScore, Is.EqualTo(expectedScore));
    }

    [Test]
    public void MatchResolutionRejectsMixedItems()
    {
        ShelfItem[] items =
        {
            CreateItem(ItemType.Ball),
            CreateItem(ItemType.Ball),
            CreateItem(ItemType.Bear)
        };

        Assert.Throws<ArgumentException>(() => new MatchResolution(items));
    }

    [TestCase(2)]
    [TestCase(6)]
    public void MatchResolutionRejectsUnsupportedSize(int itemCount)
    {
        Assert.Throws<ArgumentException>(() => new MatchResolution(CreateItems(itemCount, ItemType.Ball)));
    }

    [Test]
    public void MoveThatConsumesLastEmptySlotIsRejectedAfterLayerReveal()
    {
        Shelf sourceShelf = CreateShelf(1,
            CreateLayer(ItemType.Ball),
            CreateLayer(ItemType.Bear));
        Shelf targetShelf = CreateShelf(1, CreateLayer((ItemType?)null));
        ShelfBoard board = CreateBoard(sourceShelf, targetShelf);

        bool canMove = board.CanMove(sourceShelf.ActiveLayer.Slots[0], targetShelf.ActiveLayer.Slots[0]);

        Assert.That(canMove, Is.False);
    }

    [Test]
    public void MatchMoveIsAllowedWhenItConsumesLastEmptySlot()
    {
        Shelf sourceShelf = CreateShelf(1,
            CreateLayer(ItemType.Bear),
            CreateLayer(ItemType.Lamp));
        Shelf targetShelf = CreateShelf(3, CreateLayer(ItemType.Bear, ItemType.Bear, null));
        ShelfBoard board = CreateBoard(sourceShelf, targetShelf);

        bool canMove = board.CanMove(sourceShelf.ActiveLayer.Slots[0], targetShelf.ActiveLayer.Slots[2]);

        Assert.That(canMove, Is.True);
    }

    [Test]
    public void SolverSupportsVariableLayerSizes()
    {
        BoardSnapshot snapshot = new BoardSnapshot(new[]
        {
            new BoardShelfSnapshot(1, new[]
            {
                new BoardLayerSnapshot(new ItemType?[] { ItemType.Bear })
            }),
            new BoardShelfSnapshot(4, new[]
            {
                new BoardLayerSnapshot(new ItemType?[] { ItemType.Bear, ItemType.Bear, ItemType.Bear, null })
            })
        });
        EndlessBoardSolver solver = new EndlessBoardSolver();

        Assert.That(solver.CountImmediateMatchMoves(snapshot), Is.EqualTo(1));
        Assert.That(solver.CanReachMatches(snapshot, 1, 1, 20, 10, 100), Is.True);
    }

    [Test]
    public void SolverUsesSmallShelfForPreparatoryMove()
    {
        BoardSnapshot snapshot = new BoardSnapshot(new[]
        {
            new BoardShelfSnapshot(3, new[]
            {
                new BoardLayerSnapshot(new ItemType?[] { ItemType.Ball, ItemType.Ball, ItemType.Bear })
            }),
            new BoardShelfSnapshot(2, new[]
            {
                new BoardLayerSnapshot(new ItemType?[] { ItemType.Ball, ItemType.Lamp })
            }),
            new BoardShelfSnapshot(1, new[]
            {
                new BoardLayerSnapshot(new ItemType?[] { null })
            })
        });
        EndlessBoardSolver solver = new EndlessBoardSolver();

        Assert.That(solver.CanReachMatches(snapshot, 1, 2, 30, 20, 100), Is.True);
    }

    [Test]
    public void LayerPoolKeepsCapacitiesSeparate()
    {
        ShelfLayer oneSlotPrefab = CreateLayer((ItemType?)null);
        ShelfLayer twoSlotPrefab = CreateLayer(null, null);
        oneSlotPrefab.gameObject.AddComponent<ShelfLayerView>();
        twoSlotPrefab.gameObject.AddComponent<ShelfLayerView>();

        ShelfLayerPool pool = CreateComponent<ShelfLayerPool>("LayerPool");
        SetField(pool, "_oneSlotLayerPrefab", oneSlotPrefab);
        SetField(pool, "_twoSlotLayerPrefab", twoSlotPrefab);
        SetField(pool, "_poolRoot", pool.transform);

        ShelfLayer oneSlotLayer = pool.Get(1);
        pool.Release(oneSlotLayer);
        ShelfLayer twoSlotLayer = pool.Get(2);

        Assert.That(twoSlotLayer, Is.Not.SameAs(oneSlotLayer));
        Assert.That(twoSlotLayer.Capacity, Is.EqualTo(2));

        pool.Release(twoSlotLayer);

        Assert.That(pool.Get(1), Is.SameAs(oneSlotLayer));
    }

    [Test]
    public void SameSeedCreatesSameBatch()
    {
        BoardSnapshot snapshot = CreateEmptySnapshot(3, 3, 3, 3, 4, 4, 5, 5, 1, 2, 3, 4, 5, 3);
        EndlessGenerationConfig config = CreateConfig();
        EndlessItemCatalog catalog = CreateCatalog();
        GenerationBatch first = new EndlessLayoutGenerator(new System.Random(12345), config, catalog, new EndlessBoardSolver())
            .GenerateRefill(snapshot, 10, 0);
        GenerationBatch second = new EndlessLayoutGenerator(new System.Random(12345), config, catalog, new EndlessBoardSolver())
            .GenerateRefill(snapshot, 10, 0);

        Assert.That(Serialize(first), Is.EqualTo(Serialize(second)));
    }

    [Test]
    public void GeneratedBatchHasNoReadyOrLargeNearCompleteMatch()
    {
        BoardSnapshot snapshot = CreateEmptySnapshot(3, 3, 3, 3, 4, 4, 5, 5, 1, 2, 3, 4, 5, 3);
        GenerationBatch batch = new EndlessLayoutGenerator(
                new System.Random(9182),
                CreateConfig(),
                CreateCatalog(),
                new EndlessBoardSolver())
            .GenerateRefill(snapshot, 12, 20);

        for (int shelfIndex = 0; shelfIndex < batch.ShelfCount; shelfIndex++)
        {
            foreach (BoardLayerSnapshot layer in batch.GetLayers(shelfIndex))
            {
                ItemType?[] items = layer.Items.ToArray();
                int itemCount = items.Count(item => item.HasValue);
                int largestGroup = items
                    .Where(item => item.HasValue)
                    .GroupBy(item => item.Value)
                    .Select(group => group.Count())
                    .DefaultIfEmpty(0)
                    .Max();

                Assert.That(itemCount == items.Length && largestGroup == items.Length, Is.False);

                if (items.Length >= 4)
                    Assert.That(itemCount == items.Length - 1 && largestGroup == items.Length - 1, Is.False);
            }
        }
    }

    [Test, Explicit]
    public void GroupSizeDistributionStaysNearConfiguredWeights()
    {
        BoardSnapshot snapshot = CreateEmptySnapshot(3, 3, 3, 4, 4, 5, 5, 3, 4, 5, 3, 4, 5, 3);
        EndlessGenerationConfig config = CreateConfig();
        EndlessItemCatalog catalog = CreateCatalog();
        Dictionary<int, int> counts = new Dictionary<int, int> { [3] = 0, [4] = 0, [5] = 0 };

        for (int seed = 0; seed < 200; seed++)
        {
            GenerationBatch batch = new EndlessLayoutGenerator(new System.Random(seed), config, catalog, new EndlessBoardSolver())
                .GenerateRefill(snapshot, 10, seed / 2);

            foreach (int size in batch.GroupSizes)
                counts[size]++;
        }

        int total = counts.Values.Sum();
        Assert.That((float)counts[3] / total, Is.InRange(0.5f, 0.7f));
        Assert.That((float)counts[4] / total, Is.InRange(0.2f, 0.4f));
        Assert.That((float)counts[5] / total, Is.InRange(0.05f, 0.15f));
    }

    private ShelfBoard CreateBoard(params Shelf[] shelves)
    {
        ShelfBoard board = CreateComponent<ShelfBoard>("ShelfBoard");
        SetField(board, "_shelves", shelves.ToList());
        return board;
    }

    private Shelf CreateShelf(int capacity, params ShelfLayer[] layers)
    {
        Shelf shelf = CreateComponent<Shelf>($"Shelf{capacity}");

        foreach (ShelfLayer layer in layers)
            layer.transform.SetParent(shelf.transform, false);

        SetField(shelf, "_capacity", capacity);
        SetField(shelf, "_shelfLayers", layers.ToList());
        shelf.ValidateLayers();
        return shelf;
    }

    private ShelfLayer CreateLayer(params ItemType?[] types)
    {
        ShelfLayer layer = CreateComponent<ShelfLayer>($"Layer{types.Length}");
        List<ShelfSlot> slots = new List<ShelfSlot>(types.Length);

        for (int index = 0; index < types.Length; index++)
        {
            ShelfSlot slot = CreateChildComponent<ShelfSlot>(layer.transform, $"Slot{index}");
            Transform anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(slot.transform, false);
            SetField(slot, "_itemAnchor", anchor);

            if (types[index].HasValue)
                slot.PlaceItem(CreateItem(types[index].Value));

            slots.Add(slot);
        }

        SetField(layer, "_slots", slots);
        return layer;
    }

    private ShelfItem[] CreateItems(int count, ItemType type)
    {
        ShelfItem[] items = new ShelfItem[count];

        for (int index = 0; index < count; index++)
            items[index] = CreateItem(type);

        return items;
    }

    private ShelfItem CreateItem(ItemType type)
    {
        ShelfItem item = CreateComponent<ShelfItem>($"Item{type}");
        SetField(item, "_type", type);
        return item;
    }

    private BoardSnapshot CreateEmptySnapshot(params int[] capacities)
    {
        return new BoardSnapshot(capacities
            .Select(capacity => new BoardShelfSnapshot(capacity, Array.Empty<BoardLayerSnapshot>()))
            .ToArray());
    }

    private EndlessGenerationConfig CreateConfig()
    {
        EndlessGenerationConfig config = ScriptableObject.CreateInstance<EndlessGenerationConfig>();
        _objects.Add(config);
        SetField(config, "_generationTimeLimitMilliseconds", 1000);
        SetField(config, "_solverTimeLimitMilliseconds", 100);
        return config;
    }

    private EndlessItemCatalog CreateCatalog()
    {
        EndlessItemCatalog catalog = ScriptableObject.CreateInstance<EndlessItemCatalog>();
        _objects.Add(catalog);
        List<EndlessItemCatalog.Entry> entries = new List<EndlessItemCatalog.Entry>();

        foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
        {
            EndlessItemCatalog.Entry entry = new EndlessItemCatalog.Entry();
            SetField(entry, "_type", type);
            SetField(entry, "_prefab", CreateItem(type));
            SetField(entry, "_weight", 1f);
            entries.Add(entry);
        }

        SetField(catalog, "_entries", entries);
        return catalog;
    }

    private static string Serialize(GenerationBatch batch)
    {
        List<string> shelves = new List<string>();

        for (int shelfIndex = 0; shelfIndex < batch.ShelfCount; shelfIndex++)
        {
            string layers = string.Join("/", batch.GetLayers(shelfIndex)
                .Select(layer => string.Join(",", layer.Items.Select(item => item.HasValue ? ((int)item.Value + 1).ToString() : "0"))));
            shelves.Add(layers);
        }

        return string.Join("|", shelves) + ":" + string.Join(",", batch.GroupSizes);
    }

    private T CreateComponent<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        _objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private static T CreateChildComponent<T>(Transform parent, string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.AddComponent<T>();
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);

        field.SetValue(target, value);
    }
}
