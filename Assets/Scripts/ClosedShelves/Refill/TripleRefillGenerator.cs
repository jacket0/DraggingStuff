using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TripleRefillGenerator
{
    private const int TripleSize = 3;

    private static readonly int[][] Permutations3 =
    {
        new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
        new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
    };

    private readonly System.Random _random;
    private readonly ShelfRefillSettings _settings;
    private readonly ItemType[] _levelTypes;

    public TripleRefillGenerator(System.Random random, ShelfRefillSettings settings, IReadOnlyList<ItemType> levelTypes)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        if (levelTypes == null || levelTypes.Count == 0)
            throw new ArgumentException("At least one item type is required.", nameof(levelTypes));

        _settings.Validate();
        _levelTypes = levelTypes.Distinct().OrderBy(type => (int)type).ToArray();
    }

    public bool ShouldRefill(BoardStateSnapshot board, bool isRefillActive)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        return isRefillActive && ComputeHiddenItemCount(board) < _settings.HiddenItemThreshold;
    }

    public GenerationBatch GenerateRefill(BoardStateSnapshot board, IReadOnlyDictionary<ItemType, int> remainingRequiredCounts, int emptyColumnReserve)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (remainingRequiredCounts == null)
            throw new ArgumentNullException(nameof(remainingRequiredCounts));

        if (emptyColumnReserve < 0)
            throw new ArgumentOutOfRangeException(nameof(emptyColumnReserve));

        List<ItemType>[][] mirror = new List<ItemType>[board.Shelves.Count][];
        bool[] shelfOpen = new bool[board.Shelves.Count];
        List<ColumnPosition> allPositions = new List<ColumnPosition>();
        Dictionary<ItemType, int> onBoard = new Dictionary<ItemType, int>();

        foreach (ItemType type in _levelTypes)
            onBoard[type] = 0;

        for (int shelfIndex = 0; shelfIndex < mirror.Length; shelfIndex++)
        {
            ShelfStateSnapshot shelf = board.Shelves[shelfIndex];
            shelfOpen[shelfIndex] = shelf.IsOpen;
            mirror[shelfIndex] = new List<ItemType>[shelf.Capacity];

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                List<ItemType> items = new List<ItemType>(shelf.Columns[columnIndex].Items);
                mirror[shelfIndex][columnIndex] = items;
                allPositions.Add(new ColumnPosition(shelfIndex, columnIndex));

                foreach (ItemType type in items)
                    onBoard[type] = onBoard.TryGetValue(type, out int existing) ? existing + 1 : 1;
            }
        }

        int emptyOpenColumns = allPositions.Count(position => shelfOpen[position.ShelfIndex] && mirror[position.ShelfIndex][position.ColumnIndex].Count == 0);
        int fallbackBudget = Math.Max(0, emptyOpenColumns - emptyColumnReserve);

        GenerationBatch batch = new GenerationBatch(board);

        for (int slot = 0; slot < _settings.GroupsPerRefill; slot++)
        {
            ItemType type = ChooseTripleType(onBoard, remainingRequiredCounts);
            PlaceTriple(type, mirror, shelfOpen, allPositions, batch, ref fallbackBudget);
            onBoard[type] += TripleSize;
        }

        return batch;
    }

    public IReadOnlyList<IReadOnlyList<ItemType>> CreateRevealedShelf(int groupCount, IReadOnlyDictionary<ItemType, int> onBoardCounts)
    {
        if (groupCount <= 0 || groupCount % TripleSize != 0)
            throw new ArgumentException("groupCount must be a positive multiple of three.", nameof(groupCount));

        if (onBoardCounts == null)
            throw new ArgumentNullException(nameof(onBoardCounts));

        if (_levelTypes.Length < TripleSize)
            throw new InvalidOperationException("At least three item types are required to build a revealed shelf.");

        List<ItemType>[] columns = { new List<ItemType>(groupCount), new List<ItemType>(groupCount), new List<ItemType>(groupCount) };
        Dictionary<ItemType, int> runningCount = _levelTypes.ToDictionary(type => type, type => onBoardCounts.TryGetValue(type, out int value) ? value : 0);
        ItemType[] previousPermutation = null;
        int blocks = groupCount / TripleSize;

        for (int block = 0; block < blocks; block++)
        {
            ItemType[] chosen = ChooseDistinctTypesWithoutReplacement(runningCount, TripleSize);
            ItemType[] permutation = ChooseBlockPermutation(chosen, previousPermutation);

            for (int row = 0; row < TripleSize; row++)
            {
                for (int column = 0; column < TripleSize; column++)
                    columns[column].Add(permutation[(row + column) % TripleSize]);
            }

            foreach (ItemType type in chosen)
                runningCount[type] += TripleSize;

            previousPermutation = permutation;
        }

        return columns;
    }

    private static int ComputeHiddenItemCount(BoardStateSnapshot board)
    {
        int total = 0;

        foreach (ShelfStateSnapshot shelf in board.Shelves)
        {
            if (!shelf.IsOpen)
                continue;

            foreach (ColumnStateSnapshot column in shelf.Columns)
                total += Math.Max(0, column.Count - 1);
        }

        return total;
    }

    private ItemType ChooseTripleType(IReadOnlyDictionary<ItemType, int> onBoard, IReadOnlyDictionary<ItemType, int> remainingRequiredCounts)
    {
        foreach (ItemType type in _levelTypes)
        {
            if (remainingRequiredCounts.TryGetValue(type, out int needed) && needed > 0 && onBoard[type] < needed)
                return type;
        }

        double[] weights = new double[_levelTypes.Length];
        double total = 0;

        for (int index = 0; index < _levelTypes.Length; index++)
        {
            ItemType type = _levelTypes[index];
            double weight = 1d / (1d + onBoard[type]);

            if (remainingRequiredCounts.TryGetValue(type, out int needed) && needed > 0)
                weight *= _settings.ConditionTypeWeight;

            weights[index] = weight;
            total += weight;
        }

        int selected = RouletteSelect(weights, total);
        return _levelTypes[selected];
    }

    private void PlaceTriple(ItemType type, List<ItemType>[][] mirror, bool[] shelfOpen, List<ColumnPosition> allPositions, GenerationBatch batch, ref int fallbackBudget)
    {
        HashSet<ColumnPosition> usedThisTriple = new HashSet<ColumnPosition>();
        Dictionary<int, int> shelfUsageThisTriple = new Dictionary<int, int>();

        for (int placementIndex = 0; placementIndex < TripleSize; placementIndex++)
        {
            ColumnPosition? position = FindNonEmptyCandidate(type, mirror, shelfOpen, allPositions, usedThisTriple, shelfUsageThisTriple);

            if (!position.HasValue && fallbackBudget > 0)
            {
                position = FindEmptyFallbackCandidate(type, mirror, shelfOpen, allPositions, usedThisTriple, shelfUsageThisTriple);

                if (position.HasValue)
                    fallbackBudget--;
            }

            if (!position.HasValue)
                throw new InvalidOperationException($"No eligible column for a triple of type {type}.");

            mirror[position.Value.ShelfIndex][position.Value.ColumnIndex].Add(type);
            batch.Append(position.Value.ShelfIndex, position.Value.ColumnIndex, type);
            usedThisTriple.Add(position.Value);
            shelfUsageThisTriple[position.Value.ShelfIndex] = shelfUsageThisTriple.TryGetValue(position.Value.ShelfIndex, out int used) ? used + 1 : 1;
        }
    }

    private ColumnPosition? FindNonEmptyCandidate(ItemType type, List<ItemType>[][] mirror, bool[] shelfOpen, List<ColumnPosition> allPositions,
        HashSet<ColumnPosition> usedThisTriple, Dictionary<int, int> shelfUsageThisTriple)
    {
        List<ColumnPosition> candidates = new List<ColumnPosition>();
        int minimumDepth = int.MaxValue;

        foreach (ColumnPosition position in allPositions)
        {
            if (!shelfOpen[position.ShelfIndex] || usedThisTriple.Contains(position))
                continue;

            if (shelfUsageThisTriple.TryGetValue(position.ShelfIndex, out int used) && used >= _settings.MaximumTriplePerShelf)
                continue;

            List<ItemType> column = mirror[position.ShelfIndex][position.ColumnIndex];

            if (column.Count == 0 || column.Count >= TimedLevelLayoutRules.MaximumColumnDepth)
                continue;

            if (column[column.Count - 1] == type)
                continue;

            if (column.Count < minimumDepth)
            {
                minimumDepth = column.Count;
                candidates.Clear();
                candidates.Add(position);
            }
            else if (column.Count == minimumDepth)
            {
                candidates.Add(position);
            }
        }

        return candidates.Count == 0 ? (ColumnPosition?)null : candidates[_random.Next(candidates.Count)];
    }

    private ColumnPosition? FindEmptyFallbackCandidate(ItemType type, List<ItemType>[][] mirror, bool[] shelfOpen, List<ColumnPosition> allPositions,
        HashSet<ColumnPosition> usedThisTriple, Dictionary<int, int> shelfUsageThisTriple)
    {
        List<ColumnPosition> candidates = new List<ColumnPosition>();

        foreach (ColumnPosition position in allPositions)
        {
            if (!shelfOpen[position.ShelfIndex] || usedThisTriple.Contains(position))
                continue;

            if (shelfUsageThisTriple.TryGetValue(position.ShelfIndex, out int used) && used >= _settings.MaximumTriplePerShelf)
                continue;

            if (mirror[position.ShelfIndex][position.ColumnIndex].Count != 0)
                continue;

            if (WouldCreateInstantMatch(mirror[position.ShelfIndex], position.ColumnIndex, type))
                continue;

            candidates.Add(position);
        }

        return candidates.Count == 0 ? (ColumnPosition?)null : candidates[_random.Next(candidates.Count)];
    }

    private static bool WouldCreateInstantMatch(IReadOnlyList<List<ItemType>> shelfColumns, int columnIndex, ItemType type)
    {
        if (shelfColumns.Count < Shelf.MinimumMatchCapacity)
            return false;

        for (int index = 0; index < shelfColumns.Count; index++)
        {
            if (index == columnIndex)
                continue;

            if (shelfColumns[index].Count == 0 || shelfColumns[index][0] != type)
                return false;
        }

        return true;
    }

    private ItemType[] ChooseDistinctTypesWithoutReplacement(Dictionary<ItemType, int> runningCount, int count)
    {
        List<ItemType> pool = new List<ItemType>(_levelTypes);
        ItemType[] chosen = new ItemType[count];

        for (int index = 0; index < count; index++)
        {
            double[] weights = new double[pool.Count];
            double total = 0;

            for (int poolIndex = 0; poolIndex < pool.Count; poolIndex++)
            {
                double weight = 1d / (1d + runningCount[pool[poolIndex]]);
                weights[poolIndex] = weight;
                total += weight;
            }

            int selected = RouletteSelect(weights, total);
            chosen[index] = pool[selected];
            pool.RemoveAt(selected);
        }

        return chosen;
    }

    private ItemType[] ChooseBlockPermutation(ItemType[] chosen, ItemType[] previousPermutation)
    {
        List<ItemType[]> valid = new List<ItemType[]>();

        foreach (int[] permutation in Permutations3)
        {
            ItemType[] candidate = { chosen[permutation[0]], chosen[permutation[1]], chosen[permutation[2]] };

            if (previousPermutation == null
                || (candidate[0] != previousPermutation[2]
                    && candidate[1] != previousPermutation[0]
                    && candidate[2] != previousPermutation[1]))
            {
                valid.Add(candidate);
            }
        }

        if (valid.Count == 0)
            throw new InvalidOperationException("No valid block permutation found for the revealed shelf.");

        return valid[_random.Next(valid.Count)];
    }

    private int RouletteSelect(IReadOnlyList<double> weights, double total)
    {
        if (total <= 0)
            throw new InvalidOperationException("No item type can be generated safely.");

        double roll = _random.NextDouble() * total;
        int selected = -1;

        for (int index = 0; index < weights.Count; index++)
        {
            if (weights[index] <= 0)
                continue;

            selected = index;
            roll -= weights[index];

            if (roll < 0)
                return selected;
        }

        return selected;
    }
}
