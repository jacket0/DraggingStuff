using System;
using System.Collections.Generic;
using System.Linq;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class EndlessLayoutGenerator
{
    private const int GroupPlacementAttemptCount = 80;

    private readonly System.Random _random;
    private readonly EndlessGenerationConfig _config;
    private readonly EndlessItemCatalog _catalog;
    private readonly EndlessBoardSolver _solver;

    public EndlessLayoutGenerator(System.Random random, EndlessGenerationConfig config, EndlessItemCatalog catalog, EndlessBoardSolver solver)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));

        _config.Validate();
        _catalog.Validate();
    }

    public GenerationBatch GenerateInitial(BoardSnapshot emptyBoard)
    {
        if (emptyBoard == null)
            throw new ArgumentNullException(nameof(emptyBoard));

        if (emptyBoard.Shelves.Count == 0 || emptyBoard.Shelves.Any(shelf => shelf.Layers.Count > 0))
            throw new ArgumentException(nameof(emptyBoard));

        if (!emptyBoard.Shelves.Any(shelf => shelf.Capacity == Shelf.MinimumMatchCapacity))
            throw new ArgumentException(nameof(emptyBoard));

        GenerationBatch bestBatch = FindBestBatch(emptyBoard, 0, 0, true, true);
        return bestBatch ?? CreateInitialFallback(emptyBoard);
    }

    public GenerationBatch GenerateRefill(BoardSnapshot snapshot, int groupCount, int matchCount)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        if (groupCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(groupCount));

        GenerationBatch bestBatch = FindBestBatch(snapshot, groupCount, matchCount, false, true);
        return bestBatch ?? CreateRefillFallback(snapshot, groupCount);
    }

    private GenerationBatch FindBestBatch(BoardSnapshot snapshot, int groupCount, int matchCount, bool isInitial, bool requireSolver)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<CandidateEvaluation> candidates = new List<CandidateEvaluation>();

        for (int attempt = 0; attempt < _config.CandidateCount; attempt++)
        {
            if (stopwatch.ElapsedMilliseconds >= _config.GenerationTimeLimitMilliseconds)
                break;

            GenerationBatch batch = CreateCandidate(snapshot, groupCount, matchCount, isInitial);

            if (batch == null)
                continue;

            BoardSnapshot combinedSnapshot = snapshot.Append(batch);
            int immediateMatchMoves = isInitial
                ? _solver.CountImmediateMatchMoves(combinedSnapshot, Shelf.MinimumMatchCapacity)
                : _solver.CountImmediateMatchMoves(combinedSnapshot);

            if (isInitial && immediateMatchMoves < 2)
                continue;

            float score = Score(batch, snapshot, immediateMatchMoves);
            candidates.Add(new CandidateEvaluation(batch, combinedSnapshot, score));
        }

        candidates.Sort((first, second) => second.Score.CompareTo(first.Score));

        if (!requireSolver)
            return candidates.Count > 0 ? candidates[0].Batch : null;

        int solverCandidateCount = Math.Min(_config.SolverCandidateCount, candidates.Count);

        for (int index = 0; index < solverCandidateCount; index++)
        {
            if (stopwatch.ElapsedMilliseconds >= _config.GenerationTimeLimitMilliseconds)
                break;

            CandidateEvaluation candidate = candidates[index];

            if (_solver.CanReachMatches(
                    candidate.CombinedSnapshot,
                    _config.SolverMatchGoal,
                    _config.SolverMaximumMoveCount,
                    _config.SolverNodeLimit,
                    _config.SolverMaximumMovesPerState,
                    _config.SolverTimeLimitMilliseconds))
                return candidate.Batch;
        }

        return null;
    }

    private readonly struct CandidateEvaluation
    {
        public GenerationBatch Batch { get; }
        public BoardSnapshot CombinedSnapshot { get; }
        public float Score { get; }

        public CandidateEvaluation(GenerationBatch batch, BoardSnapshot combinedSnapshot, float score)
        {
            Batch = batch;
            CombinedSnapshot = combinedSnapshot;
            Score = score;
        }
    }

    private GenerationBatch CreateCandidate(BoardSnapshot snapshot, int groupCount, int matchCount, bool isInitial)
    {
        List<ItemGroup> groups = isInitial
            ? CreateInitialGroups(snapshot)
            : CreateGroups(snapshot, groupCount);
        int itemCount = groups.Sum(group => group.Size);
        List<CandidateLayer> layers = isInitial
            ? CreateInitialLayerPlan(snapshot, itemCount)
            : CreateRefillLayerPlan(snapshot, itemCount, matchCount);

        if (layers == null)
            return null;

        foreach (ItemGroup group in groups)
        {
            if (!TryPlaceGroup(layers, group, matchCount))
                return null;
        }

        return CreateBatch(snapshot.Shelves.Count, layers, groups);
    }

    private List<CandidateLayer> CreateInitialLayerPlan(BoardSnapshot snapshot, int itemCount)
    {
        List<CandidateLayer> layers = new List<CandidateLayer>(snapshot.Shelves.Count);

        for (int shelfIndex = 0; shelfIndex < snapshot.Shelves.Count; shelfIndex++)
        {
            int capacity = snapshot.Shelves[shelfIndex].Capacity;
            layers.Add(new CandidateLayer(shelfIndex, 0, 0, capacity, capacity));
        }

        int emptySlotCount = layers.Sum(layer => layer.FillLimit) - itemCount;

        foreach (CandidateLayer layer in layers
                     .Where(layer => layer.PhysicalCapacity == 2)
                     .OrderBy(_ => _random.Next()))
        {
            if (emptySlotCount <= 0)
                break;

            if (_random.NextDouble() < _config.FullTwoSlotLayerChance)
                continue;

            layer.ReduceFillLimit();
            emptySlotCount--;
        }

        while (emptySlotCount > 0)
        {
            List<CandidateLayer> candidates = layers
                .Where(layer => layer.PhysicalCapacity >= Shelf.MinimumMatchCapacity && layer.CanReduceFillLimit)
                .ToList();

            if (candidates.Count == 0)
                candidates = layers.Where(layer => layer.CanReduceFillLimit).ToList();

            if (candidates.Count == 0)
                return null;

            CandidateLayer selectedLayer = candidates[_random.Next(candidates.Count)];
            selectedLayer.ReduceFillLimit();
            emptySlotCount--;
        }

        return layers;
    }

    private List<CandidateLayer> CreateRefillLayerPlan(BoardSnapshot snapshot, int itemCount, int matchCount)
    {
        List<CandidateLayer> layers = new List<CandidateLayer>();
        int[] generatedLayerCounts = new int[snapshot.Shelves.Count];
        int remainingItemCount = itemCount;

        while (remainingItemCount > 0)
        {
            int minimumDepth = int.MaxValue;

            for (int shelfIndex = 0; shelfIndex < snapshot.Shelves.Count; shelfIndex++)
            {
                int nextDepth = snapshot.Shelves[shelfIndex].Layers.Count + generatedLayerCounts[shelfIndex];
                minimumDepth = Math.Min(minimumDepth, nextDepth);
            }

            List<int> shelfCandidates = new List<int>();

            for (int shelfIndex = 0; shelfIndex < snapshot.Shelves.Count; shelfIndex++)
            {
                int nextDepth = snapshot.Shelves[shelfIndex].Layers.Count + generatedLayerCounts[shelfIndex];

                if (nextDepth <= minimumDepth + 1)
                    shelfCandidates.Add(shelfIndex);
            }

            int selectedShelf = shelfCandidates[_random.Next(shelfCandidates.Count)];
            int physicalCapacity = snapshot.Shelves[selectedShelf].Capacity;
            int fillLimit = ChooseLayerFillLimit(physicalCapacity, remainingItemCount, matchCount);
            int localDepth = generatedLayerCounts[selectedShelf];
            int absoluteDepth = snapshot.Shelves[selectedShelf].Layers.Count + localDepth;

            layers.Add(new CandidateLayer(selectedShelf, localDepth, absoluteDepth, physicalCapacity, fillLimit));
            generatedLayerCounts[selectedShelf]++;
            remainingItemCount -= fillLimit;
        }

        return layers;
    }

    private static int FindShallowestGeneratedShelf(BoardSnapshot snapshot, int[] generatedLayerCounts)
    {
        int selectedShelf = 0;
        int selectedDepth = int.MaxValue;

        for (int shelfIndex = 0; shelfIndex < snapshot.Shelves.Count; shelfIndex++)
        {
            int depth = snapshot.Shelves[shelfIndex].Layers.Count + generatedLayerCounts[shelfIndex];

            if (depth >= selectedDepth)
                continue;

            selectedShelf = shelfIndex;
            selectedDepth = depth;
        }

        return selectedShelf;
    }

    private int ChooseLayerFillLimit(int physicalCapacity, int remainingItemCount, int matchCount)
    {
        int maximumFill = Math.Min(physicalCapacity, remainingItemCount);

        if (physicalCapacity == 1 || maximumFill == 1)
            return 1;

        if (physicalCapacity == 2)
            return _random.NextDouble() < _config.FullTwoSlotLayerChance ? 2 : 1;

        if (_random.NextDouble() < _config.GetDenseLayerChance(matchCount))
            return maximumFill;

        return _random.Next(1, maximumFill + 1);
    }

    private List<ItemGroup> CreateInitialGroups(BoardSnapshot snapshot)
    {
        int totalSlotCount = snapshot.Shelves.Sum(shelf => shelf.Capacity);
        int emptySlotCount = Math.Max(
            _config.MinimumInitialEmptySlots,
            (int)Math.Ceiling(totalSlotCount * _config.InitialEmptySlotRatio));
        int itemBudget = totalSlotCount - emptySlotCount;
        List<int> sizes = GetAvailableGroupSizes(snapshot);

        while (itemBudget > 0 && !CanCompose(itemBudget, sizes))
            itemBudget--;

        if (itemBudget < Shelf.MinimumMatchCapacity)
            throw new InvalidOperationException(nameof(itemBudget));

        List<int> groupSizes = new List<int>();
        int remainingItemCount = itemBudget;

        while (remainingItemCount > 0)
        {
            List<int> candidates = sizes
                .Where(size => size <= remainingItemCount && CanCompose(remainingItemCount - size, sizes))
                .ToList();
            int size = ChooseWeightedGroupSize(candidates);
            groupSizes.Add(size);
            remainingItemCount -= size;
        }

        return CreateGroups(groupSizes);
    }

    private List<ItemGroup> CreateGroups(BoardSnapshot snapshot, int groupCount)
    {
        List<int> availableSizes = GetAvailableGroupSizes(snapshot);
        List<int> groupSizes = new List<int>(groupCount);

        for (int index = 0; index < groupCount; index++)
            groupSizes.Add(ChooseWeightedGroupSize(availableSizes));

        return CreateGroups(groupSizes);
    }

    private List<ItemGroup> CreateGroups(IReadOnlyList<int> groupSizes)
    {
        List<ItemType> groupTypes = CreateGroupTypes(groupSizes.Count);
        List<ItemGroup> groups = new List<ItemGroup>(groupSizes.Count);

        for (int index = 0; index < groupSizes.Count; index++)
            groups.Add(new ItemGroup(groupTypes[index], groupSizes[index]));

        return groups;
    }

    private List<int> GetAvailableGroupSizes(BoardSnapshot snapshot)
    {
        List<int> sizes = new List<int>(3);

        if (snapshot.Shelves.Any(shelf => shelf.Capacity == 3) && _config.ThreeItemGroupWeight > 0f)
            sizes.Add(3);

        if (snapshot.Shelves.Any(shelf => shelf.Capacity == 4) && _config.FourItemGroupWeight > 0f)
            sizes.Add(4);

        if (snapshot.Shelves.Any(shelf => shelf.Capacity == 5) && _config.FiveItemGroupWeight > 0f)
            sizes.Add(5);

        if (sizes.Count == 0)
            throw new InvalidOperationException(nameof(snapshot));

        return sizes;
    }

    private int ChooseWeightedGroupSize(IReadOnlyList<int> sizes)
    {
        double totalWeight = 0d;

        foreach (int size in sizes)
            totalWeight += GetGroupSizeWeight(size);

        double roll = _random.NextDouble() * totalWeight;

        foreach (int size in sizes)
        {
            roll -= GetGroupSizeWeight(size);

            if (roll <= 0d)
                return size;
        }

        return sizes[sizes.Count - 1];
    }

    private float GetGroupSizeWeight(int size)
    {
        return size switch
        {
            3 => _config.ThreeItemGroupWeight,
            4 => _config.FourItemGroupWeight,
            5 => _config.FiveItemGroupWeight,
            _ => throw new ArgumentOutOfRangeException(nameof(size))
        };
    }

    private static bool CanCompose(int itemCount, IReadOnlyList<int> sizes)
    {
        if (itemCount == 0)
            return true;

        if (itemCount < 0)
            return false;

        bool[] reachable = new bool[itemCount + 1];
        reachable[0] = true;

        for (int count = 1; count <= itemCount; count++)
        {
            foreach (int size in sizes)
            {
                if (count >= size && reachable[count - size])
                {
                    reachable[count] = true;
                    break;
                }
            }
        }

        return reachable[itemCount];
    }

    private List<ItemType> CreateGroupTypes(int groupCount)
    {
        List<ItemType> groupTypes = new List<ItemType>(groupCount);

        foreach (EndlessItemCatalog.Entry entry in _catalog.Entries)
        {
            if (groupTypes.Count < groupCount)
                groupTypes.Add(entry.Type);
        }

        while (groupTypes.Count < groupCount)
            groupTypes.Add(ChooseWeightedType());

        Shuffle(groupTypes);
        return groupTypes;
    }

    private ItemType ChooseWeightedType()
    {
        double totalWeight = 0d;

        foreach (EndlessItemCatalog.Entry entry in _catalog.Entries)
            totalWeight += entry.Weight;

        double roll = _random.NextDouble() * totalWeight;

        foreach (EndlessItemCatalog.Entry entry in _catalog.Entries)
        {
            roll -= entry.Weight;

            if (roll <= 0d)
                return entry.Type;
        }

        return _catalog.Entries[_catalog.Entries.Count - 1].Type;
    }

    private bool TryPlaceGroup(List<CandidateLayer> layers, ItemGroup group, int matchCount)
    {
        int minimumShelfCount = group.Size == 5 ? 3 : 2;
        int preferredShelfCount = minimumShelfCount;

        if (group.Size < 5 && _random.NextDouble() < _config.GetThreeShelfChance(matchCount))
            preferredShelfCount = Math.Min(3, group.Size);

        for (int attempt = 0; attempt < GroupPlacementAttemptCount; attempt++)
        {
            int requiredShelfCount = attempt < GroupPlacementAttemptCount / 2
                ? preferredShelfCount
                : minimumShelfCount;
            Dictionary<CandidateLayer, int> additions = new Dictionary<CandidateLayer, int>();
            List<CandidateLayer> availableLayers = layers
                .Where(layer => layer.CanAdd(group.Type, 1))
                .OrderBy(_ => _random.Next())
                .ToList();

            if (!TrySelectDistinctShelves(availableLayers, group.Type, requiredShelfCount, additions))
                continue;

            while (additions.Values.Sum() < group.Size)
            {
                List<CandidateLayer> candidates = availableLayers
                    .Where(layer => IsDepthClose(additions.Keys, layer))
                    .Where(layer => layer.CanAdd(group.Type, additions.TryGetValue(layer, out int count) ? count + 1 : 1))
                    .ToList();

                if (candidates.Count == 0)
                    break;

                CandidateLayer selectedLayer = candidates[_random.Next(candidates.Count)];
                additions[selectedLayer] = additions.TryGetValue(selectedLayer, out int count) ? count + 1 : 1;
            }

            if (additions.Values.Sum() != group.Size)
                continue;

            foreach (KeyValuePair<CandidateLayer, int> addition in additions)
                addition.Key.Add(group.Type, addition.Value);

            return true;
        }

        return false;
    }

    private bool TrySelectDistinctShelves(
        IReadOnlyList<CandidateLayer> availableLayers,
        ItemType type,
        int requiredShelfCount,
        IDictionary<CandidateLayer, int> additions)
    {
        HashSet<int> selectedShelves = new HashSet<int>();

        while (selectedShelves.Count < requiredShelfCount)
        {
            List<CandidateLayer> candidates = availableLayers
                .Where(layer => !selectedShelves.Contains(layer.ShelfIndex))
                .Where(layer => IsDepthClose(additions.Keys, layer))
                .Where(layer => layer.CanAdd(type, 1))
                .ToList();

            if (candidates.Count == 0)
                return false;

            CandidateLayer selectedLayer = candidates[_random.Next(candidates.Count)];
            additions.Add(selectedLayer, 1);
            selectedShelves.Add(selectedLayer.ShelfIndex);
        }

        return true;
    }

    private static bool IsDepthClose(IEnumerable<CandidateLayer> selectedLayers, CandidateLayer candidate)
    {
        foreach (CandidateLayer selectedLayer in selectedLayers)
        {
            if (!IsDepthClose(selectedLayer, candidate))
                return false;
        }

        return true;
    }

    private GenerationBatch CreateBatch(int shelfCount, List<CandidateLayer> layers, IReadOnlyList<ItemGroup> groups)
    {
        GenerationBatch batch = new GenerationBatch(shelfCount);

        foreach (CandidateLayer layer in layers.OrderBy(layer => layer.ShelfIndex).ThenBy(layer => layer.LocalDepth))
        {
            List<ItemType?> items = layer.Items.Select(item => (ItemType?)item).ToList();

            while (items.Count < layer.PhysicalCapacity)
                items.Add(null);

            Shuffle(items);
            batch.AddLayer(layer.ShelfIndex, new BoardLayerSnapshot(items));
        }

        foreach (ItemGroup group in groups)
            batch.RegisterGroup(group.Size);

        return batch;
    }

    private GenerationBatch CreateInitialFallback(BoardSnapshot snapshot)
    {
        GenerationBatch batch = new GenerationBatch(snapshot.Shelves.Count);
        List<int> matchingShelfIndexes = snapshot.Shelves
            .Select((shelf, index) => new { shelf.Capacity, Index = index })
            .Where(entry => entry.Capacity == Shelf.MinimumMatchCapacity)
            .Select(entry => entry.Index)
            .Take(2)
            .ToList();
        List<int> sourceShelfIndexes = Enumerable.Range(0, snapshot.Shelves.Count)
            .Where(index => !matchingShelfIndexes.Contains(index))
            .Take(matchingShelfIndexes.Count)
            .ToList();
        ItemType?[][] itemsByShelf = snapshot.Shelves
            .Select(shelf => new ItemType?[shelf.Capacity])
            .ToArray();
        int pathCount = Math.Min(matchingShelfIndexes.Count, sourceShelfIndexes.Count);

        for (int pathIndex = 0; pathIndex < pathCount; pathIndex++)
        {
            ItemType matchType = _catalog.Entries[pathIndex % _catalog.Entries.Count].Type;
            int matchingShelfIndex = matchingShelfIndexes[pathIndex];
            int sourceShelfIndex = sourceShelfIndexes[pathIndex];

            itemsByShelf[matchingShelfIndex][0] = matchType;
            itemsByShelf[matchingShelfIndex][1] = matchType;
            itemsByShelf[sourceShelfIndex][_random.Next(itemsByShelf[sourceShelfIndex].Length)] = matchType;
            batch.RegisterGroup(3);
        }

        for (int shelfIndex = 0; shelfIndex < snapshot.Shelves.Count; shelfIndex++)
            batch.AddLayer(shelfIndex, new BoardLayerSnapshot(itemsByShelf[shelfIndex]));

        return batch;
    }

    private GenerationBatch CreateRefillFallback(BoardSnapshot snapshot, int groupCount)
    {
        GenerationBatch batch = new GenerationBatch(snapshot.Shelves.Count);
        int[] addedLayers = new int[snapshot.Shelves.Count];
        List<ItemType> groupTypes = CreateGroupTypes(groupCount);

        foreach (ItemType type in groupTypes)
        {
            int firstShelf = FindShallowestShelf(snapshot, addedLayers, -1);
            int secondShelf = FindShallowestShelf(snapshot, addedLayers, firstShelf);

            AddFallbackLayer(snapshot, batch, addedLayers, firstShelf, type);
            AddFallbackLayer(snapshot, batch, addedLayers, secondShelf, type);
            AddFallbackLayer(snapshot, batch, addedLayers, secondShelf, type);
            batch.RegisterGroup(3);
        }

        return batch;
    }

    private int FindShallowestShelf(BoardSnapshot snapshot, int[] addedLayers, int excludedShelf)
    {
        List<int> shallowestShelves = new List<int>();
        int shallowestDepth = int.MaxValue;

        for (int shelfIndex = 0; shelfIndex < snapshot.Shelves.Count; shelfIndex++)
        {
            if (shelfIndex == excludedShelf)
                continue;

            int depth = snapshot.Shelves[shelfIndex].Layers.Count + addedLayers[shelfIndex];

            if (depth < shallowestDepth)
            {
                shallowestShelves.Clear();
                shallowestDepth = depth;
            }

            if (depth == shallowestDepth)
                shallowestShelves.Add(shelfIndex);
        }

        if (shallowestShelves.Count == 0)
            throw new InvalidOperationException(nameof(shallowestShelves));

        return shallowestShelves[_random.Next(shallowestShelves.Count)];
    }

    private void AddFallbackLayer(
        BoardSnapshot snapshot,
        GenerationBatch batch,
        int[] addedLayers,
        int shelfIndex,
        ItemType type)
    {
        ItemType?[] items = new ItemType?[snapshot.Shelves[shelfIndex].Capacity];
        items[_random.Next(items.Length)] = type;
        batch.AddLayer(shelfIndex, new BoardLayerSnapshot(items));
        addedLayers[shelfIndex]++;
    }

    private float Score(GenerationBatch batch, BoardSnapshot snapshot, int immediateMatchMoves)
    {
        float score = immediateMatchMoves * 20f;
        Dictionary<int, int> occupancyCounts = new Dictionary<int, int>();
        Dictionary<string, int> shelfShapes = new Dictionary<string, int>();

        for (int shelfIndex = 0; shelfIndex < batch.ShelfCount; shelfIndex++)
        {
            string shape = string.Empty;
            ItemType? previousDominantType = GetLastDominantType(snapshot.Shelves[shelfIndex]);

            foreach (BoardLayerSnapshot layer in batch.GetLayers(shelfIndex))
            {
                int occupancy = CountItems(layer);
                occupancyCounts[occupancy] = occupancyCounts.TryGetValue(occupancy, out int count) ? count + 1 : 1;
                shape += occupancy.ToString();

                ItemType? dominantType = GetDominantType(layer);

                if (dominantType.HasValue && dominantType == previousDominantType)
                    score -= 12f;

                if (HasPair(layer))
                    score -= 3f;

                previousDominantType = dominantType;
            }

            if (shape.Length > 0)
                shelfShapes[shape] = shelfShapes.TryGetValue(shape, out int count) ? count + 1 : 1;
        }

        score += occupancyCounts.Count * 8f;

        foreach (int repeatedShapeCount in shelfShapes.Values)
            score -= Math.Max(0, repeatedShapeCount - 1) * 2f;

        return score + _random.Next(0, 1000) / 1000f;
    }

    private static ItemType? GetLastDominantType(BoardShelfSnapshot shelf)
    {
        if (shelf.Layers.Count == 0)
            return null;

        return GetDominantType(shelf.Layers[shelf.Layers.Count - 1]);
    }

    private static ItemType? GetDominantType(BoardLayerSnapshot layer)
    {
        Dictionary<ItemType, int> counts = new Dictionary<ItemType, int>();

        foreach (ItemType? item in layer.Items)
        {
            if (!item.HasValue)
                continue;

            counts[item.Value] = counts.TryGetValue(item.Value, out int count) ? count + 1 : 1;
        }

        if (counts.Count == 0)
            return null;

        return counts.OrderByDescending(pair => pair.Value).First().Key;
    }

    private static bool HasPair(BoardLayerSnapshot layer)
    {
        return layer.Items.Where(item => item.HasValue)
            .GroupBy(item => item.Value)
            .Any(group => group.Count() == 2);
    }

    private static int CountItems(BoardLayerSnapshot layer)
    {
        return layer.Items.Count(item => item.HasValue);
    }

    private static bool IsDepthClose(CandidateLayer first, CandidateLayer second)
    {
        return Math.Abs(first.AbsoluteDepth - second.AbsoluteDepth) <= 3;
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int otherIndex = _random.Next(index + 1);
            T value = values[index];
            values[index] = values[otherIndex];
            values[otherIndex] = value;
        }
    }

    private sealed class CandidateLayer
    {
        private readonly List<ItemType> _items;

        public int ShelfIndex { get; }
        public int LocalDepth { get; }
        public int AbsoluteDepth { get; }
        public int PhysicalCapacity { get; }
        public int FillLimit { get; private set; }
        public bool CanReduceFillLimit => FillLimit > (PhysicalCapacity <= 2 ? 1 : 0);
        public IReadOnlyList<ItemType> Items => _items;

        public CandidateLayer(int shelfIndex, int localDepth, int absoluteDepth, int physicalCapacity, int fillLimit)
        {
            if (!Shelf.IsValidCapacity(physicalCapacity))
                throw new ArgumentOutOfRangeException(nameof(physicalCapacity));

            if (fillLimit < 1 || fillLimit > physicalCapacity)
                throw new ArgumentOutOfRangeException(nameof(fillLimit));

            ShelfIndex = shelfIndex;
            LocalDepth = localDepth;
            AbsoluteDepth = absoluteDepth;
            PhysicalCapacity = physicalCapacity;
            FillLimit = fillLimit;
            _items = new List<ItemType>(physicalCapacity);
        }

        public void ReduceFillLimit()
        {
            if (!CanReduceFillLimit)
                throw new InvalidOperationException();

            FillLimit--;
        }

        public bool CanAdd(ItemType type, int count)
        {
            if (count <= 0 || _items.Count + count > FillLimit)
                return false;

            int matchingCount = _items.Count(item => item == type);
            int projectedMatchingCount = matchingCount + count;
            int projectedItemCount = _items.Count + count;

            if (PhysicalCapacity >= Shelf.MinimumMatchCapacity &&
                projectedItemCount == PhysicalCapacity &&
                projectedMatchingCount == PhysicalCapacity)
                return false;

            if (PhysicalCapacity >= 4 &&
                projectedItemCount == PhysicalCapacity - 1 &&
                projectedMatchingCount == PhysicalCapacity - 1)
                return false;

            return true;
        }

        public void Add(ItemType type, int count)
        {
            if (!CanAdd(type, count))
                throw new InvalidOperationException();

            for (int index = 0; index < count; index++)
                _items.Add(type);
        }
    }

    private readonly struct ItemGroup
    {
        public ItemType Type { get; }
        public int Size { get; }

        public ItemGroup(ItemType type, int size)
        {
            if (size < Shelf.MinimumMatchCapacity || size > Shelf.MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(size));

            Type = type;
            Size = size;
        }
    }
}
