using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TimedLevelLayoutGenerator
{
    private const int MaximumAttemptCount = 8;
    private const int MaximumConstructionNodeCount = 10000;

    public BoardStateSnapshot Generate(TimedLevelDefinition definition, BoardStateSnapshot boardShape, int seed)
    {
        return GenerateWithSolution(definition, boardShape, seed).State;
    }

    public TimedLevelGenerationResult GenerateWithSolution(TimedLevelDefinition definition, BoardStateSnapshot boardShape, int seed)
    {
        TimedLevelValidator.ValidateForGeneration(definition, boardShape);
        TimedLevelGenerationInput input = new TimedLevelGenerationInput(
            definition.name,
            definition.EmptyColumnCount,
            definition.ItemGroups.Select(group => new TimedLevelGroupCount(group.Type, group.GroupCount)).ToArray());
        return GenerateWithSolution(input, boardShape, seed);
    }

    public TimedLevelGenerationResult GenerateWithSolution(TimedLevelGenerationInput input, BoardStateSnapshot boardShape, int seed)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (boardShape == null || boardShape.Shelves.Count == 0)
            throw new ArgumentNullException(nameof(boardShape));

        if (TryGenerateWithSolution(input, boardShape, seed, MaximumAttemptCount, MaximumConstructionNodeCount, out TimedLevelGenerationResult result))
            return result;

        throw new InvalidOperationException($"{input.Name}: seed {seed} could not produce a valid layout after {MaximumAttemptCount} attempts.");
    }

    public bool TryGenerateWithSolution(
        TimedLevelGenerationInput input,
        BoardStateSnapshot boardShape,
        int seed,
        int maximumAttemptCount,
        int maximumConstructionNodeCount,
        out TimedLevelGenerationResult result)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (boardShape == null || boardShape.Shelves.Count == 0)
            throw new ArgumentNullException(nameof(boardShape));

        if (maximumAttemptCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumAttemptCount));

        if (maximumConstructionNodeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumConstructionNodeCount));

        System.Random random = new System.Random(seed);
        int matchSize = boardShape.Shelves[0].Capacity;

        for (int attempt = 0; attempt < maximumAttemptCount; attempt++)
        {
            List<ItemType> groups = CreateGroupOrder(input, random);

            if (TryBuild(boardShape, input.EmptyColumnCount, groups, matchSize, random, maximumConstructionNodeCount, out result))
                return true;
        }

        result = null;
        return false;
    }

    private static List<ItemType> CreateGroupOrder(TimedLevelGenerationInput input, System.Random random)
    {
        Dictionary<ItemType, int> remainingCounts = input.Groups.ToDictionary(group => group.Type, group => group.Count);
        List<ItemType> groups = new List<ItemType>();
        ItemType? previousType = null;

        while (remainingCounts.Values.Any(count => count > 0))
        {
            List<ItemType> candidates = remainingCounts
                .Where(pair => pair.Value > 0 && (!previousType.HasValue || pair.Key != previousType.Value))
                .Select(pair => pair.Key)
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = remainingCounts
                    .Where(pair => pair.Value > 0)
                    .Select(pair => pair.Key)
                    .ToList();
            }

            ItemType selectedType = candidates[random.Next(candidates.Count)];
            groups.Add(selectedType);
            remainingCounts[selectedType]--;
            previousType = selectedType;
        }

        return groups;
    }

    private static bool TryBuild(
        BoardStateSnapshot boardShape,
        int requiredEmptyColumnCount,
        IReadOnlyList<ItemType> groups,
        int matchSize,
        System.Random random,
        int maximumConstructionNodeCount,
        out TimedLevelGenerationResult result)
    {
        List<ItemType>[][] columns = CreateEmptyColumns(boardShape);
        List<ReverseMove> reverseMoves = new List<ReverseMove>();
        int visitedNodeCount = 0;

        if (!TryAddGroups(columns, requiredEmptyColumnCount, groups, 0, random, reverseMoves, maximumConstructionNodeCount, ref visitedNodeCount))
        {
            result = null;
            return false;
        }

        BoardStateSnapshot layout = CreateSnapshot(columns);

        if (layout.EmptyColumnCount != requiredEmptyColumnCount)
        {
            result = null;
            return false;
        }

        List<TimedLevelMove> solutionMoves = CreateSolutionMoves(reverseMoves);

        if (!CanReplaySolution(layout, solutionMoves))
        {
            result = null;
            return false;
        }

        result = new TimedLevelGenerationResult(layout, solutionMoves);
        return true;
    }

    private static bool TryAddGroups(
        List<ItemType>[][] columns,
        int requiredEmptyColumnCount,
        IReadOnlyList<ItemType> groups,
        int groupIndex,
        System.Random random,
        List<ReverseMove> reverseMoves,
        int maximumConstructionNodeCount,
        ref int visitedNodeCount)
    {
        if (groupIndex >= groups.Count)
            return CountEmptyColumns(columns) == requiredEmptyColumnCount;

        if (visitedNodeCount >= maximumConstructionNodeCount)
            return false;

        visitedNodeCount++;
        ItemType type = groups[groupIndex];
        List<ReverseMove> moves = FindReverseMoves(columns, requiredEmptyColumnCount);
        moves.RemoveAll(move => WouldCreateAutomaticMatch(columns, move, type));
        moves.RemoveAll(move => WouldCreateAdjacentDuplicate(columns, move, type));
        Shuffle(moves, random);
        moves.Sort((first, second) => CompareReverseMoves(first, second, columns));

        foreach (ReverseMove move in moves)
        {
            PrependGroup(columns, move, type);
            reverseMoves.Add(move);

            if (TryAddGroups(columns, requiredEmptyColumnCount, groups, groupIndex + 1, random, reverseMoves, maximumConstructionNodeCount, ref visitedNodeCount))
                return true;

            reverseMoves.RemoveAt(reverseMoves.Count - 1);
            RemoveGroup(columns, move);
        }

        return false;
    }

    private static List<ItemType>[][] CreateEmptyColumns(BoardStateSnapshot boardShape)
    {
        List<ItemType>[][] columns = new List<ItemType>[boardShape.Shelves.Count][];

        for (int shelfIndex = 0; shelfIndex < columns.Length; shelfIndex++)
        {
            columns[shelfIndex] = new List<ItemType>[boardShape.Shelves[shelfIndex].Capacity];

            for (int columnIndex = 0; columnIndex < columns[shelfIndex].Length; columnIndex++)
                columns[shelfIndex][columnIndex] = new List<ItemType>();
        }

        return columns;
    }

    private static List<ReverseMove> FindReverseMoves(List<ItemType>[][] columns, int requiredEmptyColumnCount)
    {
        List<ReverseMove> moves = new List<ReverseMove>();
        int emptyColumnCount = CountEmptyColumns(columns);

        for (int targetShelfIndex = 0; targetShelfIndex < columns.Length; targetShelfIndex++)
        {
            List<ItemType>[] targetShelf = columns[targetShelfIndex];

            for (int targetColumnIndex = 0; targetColumnIndex < targetShelf.Length; targetColumnIndex++)
            {
                if (targetShelf[targetColumnIndex].Count != 0)
                    continue;

                for (int sourceShelfIndex = 0; sourceShelfIndex < columns.Length; sourceShelfIndex++)
                {
                    if (sourceShelfIndex == targetShelfIndex)
                        continue;

                    for (int sourceColumnIndex = 0; sourceColumnIndex < columns[sourceShelfIndex].Length; sourceColumnIndex++)
                    {
                        if (!CanPrepend(targetShelf, targetColumnIndex, columns[sourceShelfIndex][sourceColumnIndex]))
                            continue;

                        int filledEmptyColumnCount = CountFilledEmptyColumns(targetShelf, targetColumnIndex, columns[sourceShelfIndex][sourceColumnIndex]);

                        if (emptyColumnCount - filledEmptyColumnCount < requiredEmptyColumnCount)
                            continue;

                        moves.Add(new ReverseMove(targetShelfIndex, targetColumnIndex, sourceShelfIndex, sourceColumnIndex, filledEmptyColumnCount));
                    }
                }
            }
        }

        return moves;
    }

    private static bool CanPrepend(IReadOnlyList<List<ItemType>> targetShelf, int targetColumnIndex, IReadOnlyCollection<ItemType> sourceColumn)
    {
        if (sourceColumn.Count >= TimedLevelLayoutRules.MaximumColumnDepth)
            return false;

        for (int columnIndex = 0; columnIndex < targetShelf.Count; columnIndex++)
        {
            if (columnIndex != targetColumnIndex && targetShelf[columnIndex].Count >= TimedLevelLayoutRules.MaximumColumnDepth)
                return false;
        }

        return true;
    }

    private static bool WouldCreateAutomaticMatch(List<ItemType>[][] columns, ReverseMove move, ItemType type)
    {
        List<ItemType>[] sourceShelf = columns[move.SourceShelfIndex];

        for (int columnIndex = 0; columnIndex < sourceShelf.Length; columnIndex++)
        {
            if (columnIndex == move.SourceColumnIndex)
                continue;

            if (sourceShelf[columnIndex].Count == 0 || sourceShelf[columnIndex][0] != type)
                return false;
        }

        return true;
    }

    private static bool WouldCreateAdjacentDuplicate(List<ItemType>[][] columns, ReverseMove move, ItemType type)
    {
        List<ItemType>[] targetShelf = columns[move.TargetShelfIndex];

        for (int columnIndex = 0; columnIndex < targetShelf.Length; columnIndex++)
        {
            if (columnIndex == move.TargetColumnIndex)
                continue;

            List<ItemType> column = targetShelf[columnIndex];

            if (column.Count > 0 && column[0] == type)
                return true;
        }

        List<ItemType> sourceColumn = columns[move.SourceShelfIndex][move.SourceColumnIndex];
        return sourceColumn.Count > 0 && sourceColumn[0] == type;
    }

    private static int CountFilledEmptyColumns(IReadOnlyList<List<ItemType>> targetShelf, int targetColumnIndex, IReadOnlyCollection<ItemType> sourceColumn)
    {
        int count = sourceColumn.Count == 0 ? 1 : 0;

        for (int columnIndex = 0; columnIndex < targetShelf.Count; columnIndex++)
        {
            if (columnIndex != targetColumnIndex && targetShelf[columnIndex].Count == 0)
                count++;
        }

        return count;
    }

    private static int CompareReverseMoves(ReverseMove first, ReverseMove second, List<ItemType>[][] columns)
    {
        int emptyComparison = second.FilledEmptyColumnCount.CompareTo(first.FilledEmptyColumnCount);

        if (emptyComparison != 0)
            return emptyComparison;

        return GetResultingDepth(first, columns).CompareTo(GetResultingDepth(second, columns));
    }

    private static int GetResultingDepth(ReverseMove move, List<ItemType>[][] columns)
    {
        int depth = columns[move.SourceShelfIndex][move.SourceColumnIndex].Count + 1;

        for (int columnIndex = 0; columnIndex < columns[move.TargetShelfIndex].Length; columnIndex++)
        {
            if (columnIndex != move.TargetColumnIndex)
                depth = Math.Max(depth, columns[move.TargetShelfIndex][columnIndex].Count + 1);
        }

        return depth;
    }

    private static void PrependGroup(List<ItemType>[][] columns, ReverseMove move, ItemType type)
    {
        List<ItemType>[] targetShelf = columns[move.TargetShelfIndex];

        for (int columnIndex = 0; columnIndex < targetShelf.Length; columnIndex++)
        {
            if (columnIndex != move.TargetColumnIndex)
                targetShelf[columnIndex].Insert(0, type);
        }

        columns[move.SourceShelfIndex][move.SourceColumnIndex].Insert(0, type);
    }

    private static void RemoveGroup(List<ItemType>[][] columns, ReverseMove move)
    {
        List<ItemType>[] targetShelf = columns[move.TargetShelfIndex];

        for (int columnIndex = 0; columnIndex < targetShelf.Length; columnIndex++)
        {
            if (columnIndex != move.TargetColumnIndex)
                targetShelf[columnIndex].RemoveAt(0);
        }

        columns[move.SourceShelfIndex][move.SourceColumnIndex].RemoveAt(0);
    }

    private static List<TimedLevelMove> CreateSolutionMoves(IReadOnlyList<ReverseMove> reverseMoves)
    {
        List<TimedLevelMove> moves = new List<TimedLevelMove>(reverseMoves.Count);

        for (int index = reverseMoves.Count - 1; index >= 0; index--)
        {
            ReverseMove move = reverseMoves[index];
            moves.Add(new TimedLevelMove(
                new ColumnPosition(move.SourceShelfIndex, move.SourceColumnIndex),
                new ColumnPosition(move.TargetShelfIndex, move.TargetColumnIndex)));
        }

        return moves;
    }

    private static bool CanReplaySolution(BoardStateSnapshot layout, IReadOnlyList<TimedLevelMove> moves)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = layout;

        foreach (TimedLevelMove move in moves)
        {
            if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                || !simulation.IsAllowed
                || simulation.MatchCount != 1)
            {
                return false;
            }

            state = simulation.State;
        }

        return state.IsCleared;
    }

    private static int CountEmptyColumns(IEnumerable<List<ItemType>[]> shelves)
    {
        return shelves.Sum(shelf => shelf.Count(column => column.Count == 0));
    }

    private static BoardStateSnapshot CreateSnapshot(IReadOnlyList<List<ItemType>[]> columns)
    {
        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[columns.Count];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            shelves[shelfIndex] = new ShelfStateSnapshot(columns[shelfIndex]
                .Select(column => new ColumnStateSnapshot(column.ToArray()))
                .ToArray());
        }

        return new BoardStateSnapshot(shelves);
    }

    private static void Shuffle<T>(IList<T> values, System.Random random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int otherIndex = random.Next(index + 1);
            T value = values[index];
            values[index] = values[otherIndex];
            values[otherIndex] = value;
        }
    }

    private readonly struct ReverseMove
    {
        public int TargetShelfIndex { get; }
        public int TargetColumnIndex { get; }
        public int SourceShelfIndex { get; }
        public int SourceColumnIndex { get; }
        public int FilledEmptyColumnCount { get; }

        public ReverseMove(int targetShelfIndex, int targetColumnIndex, int sourceShelfIndex, int sourceColumnIndex, int filledEmptyColumnCount)
        {
            TargetShelfIndex = targetShelfIndex;
            TargetColumnIndex = targetColumnIndex;
            SourceShelfIndex = sourceShelfIndex;
            SourceColumnIndex = sourceColumnIndex;
            FilledEmptyColumnCount = filledEmptyColumnCount;
        }
    }
}
