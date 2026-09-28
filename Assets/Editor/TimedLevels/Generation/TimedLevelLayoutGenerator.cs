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
            definition.ItemGroups.Select(group => new TimedLevelGroupCount(group.Type, group.GroupCount)).ToArray(),
            definition.ClosedShelves.Select(closedShelf => closedShelf.ShelfIndex).ToArray(),
            definition.ConveyorShelfIndices.ToArray());
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
        LayoutConstraints constraints = new LayoutConstraints(input, maximumConstructionNodeCount);

        for (int attempt = 0; attempt < maximumAttemptCount; attempt++)
        {
            List<ItemType> groups = CreateGroupOrder(input, random);

            if (TryBuild(boardShape, groups, matchSize, random, constraints, out result))
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
        IReadOnlyList<ItemType> groups,
        int matchSize,
        System.Random random,
        LayoutConstraints constraints,
        out TimedLevelGenerationResult result)
    {
        List<ItemType>[][] columns = CreateEmptyColumns(boardShape);
        List<ReverseMove> reverseMoves = new List<ReverseMove>();
        int visitedNodeCount = 0;

        if (!TryAddGroups(columns, groups, 0, random, reverseMoves, constraints, ref visitedNodeCount))
        {
            result = null;
            return false;
        }

        BoardStateSnapshot layout = CreateSnapshot(columns, constraints);

        if (layout.EmptyColumnCount != constraints.RequiredEmptyColumnCount)
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
        IReadOnlyList<ItemType> groups,
        int groupIndex,
        System.Random random,
        List<ReverseMove> reverseMoves,
        LayoutConstraints constraints,
        ref int visitedNodeCount)
    {
        if (groupIndex >= groups.Count)
            return CountEmptyColumns(columns, constraints) == constraints.RequiredEmptyColumnCount && AreConveyorsStartFilled(columns, constraints);

        if (visitedNodeCount >= constraints.MaximumConstructionNodeCount)
            return false;

        visitedNodeCount++;

        if (constraints.HasConveyors)
        {
            UndoConveyorShift(columns, constraints);

            if (HasAnyMatch(columns))
            {
                RedoConveyorShift(columns, constraints);
                return false;
            }
        }

        ItemType type = groups[groupIndex];
        List<ReverseMove> moves = FindReverseMoves(columns, constraints);
        moves.RemoveAll(move => WouldCreateAutomaticMatch(columns, move, type));
        moves.RemoveAll(move => WouldCreateAdjacentDuplicate(columns, move, type, constraints));
        Shuffle(moves, random);
        moves.Sort((first, second) => CompareReverseMoves(first, second, columns, constraints));

        foreach (ReverseMove move in moves)
        {
            PrependGroup(columns, move, type);
            reverseMoves.Add(move);

            if (TryAddGroups(columns, groups, groupIndex + 1, random, reverseMoves, constraints, ref visitedNodeCount))
                return true;

            reverseMoves.RemoveAt(reverseMoves.Count - 1);
            RemoveGroup(columns, move);
        }

        if (constraints.HasConveyors)
            RedoConveyorShift(columns, constraints);

        return false;
    }

    private static void UndoConveyorShift(List<ItemType>[][] columns, LayoutConstraints constraints)
    {
        foreach (int shelfIndex in constraints.ConveyorShelfIndices)
        {
            foreach (List<ItemType> column in columns[shelfIndex])
            {
                if (column.Count < 2)
                    continue;

                ItemType back = column[column.Count - 1];
                column.RemoveAt(column.Count - 1);
                column.Insert(0, back);
            }
        }
    }

    private static void RedoConveyorShift(List<ItemType>[][] columns, LayoutConstraints constraints)
    {
        foreach (int shelfIndex in constraints.ConveyorShelfIndices)
        {
            foreach (List<ItemType> column in columns[shelfIndex])
            {
                if (column.Count < 2)
                    continue;

                ItemType front = column[0];
                column.RemoveAt(0);
                column.Add(front);
            }
        }
    }

    private static bool HasAnyMatch(List<ItemType>[][] columns)
    {
        foreach (List<ItemType>[] shelf in columns)
        {
            if (shelf.All(column => column.Count > 0 && column[0] == shelf[0][0]))
                return true;
        }

        return false;
    }

    private static bool AreConveyorsStartFilled(List<ItemType>[][] columns, LayoutConstraints constraints)
    {
        return constraints.ConveyorShelfIndices.All(shelfIndex => TimedLevelLayoutRules.IsConveyorStartFilled(CreateShelfSnapshot(columns, shelfIndex, constraints)));
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

    private static List<ReverseMove> FindReverseMoves(List<ItemType>[][] columns, LayoutConstraints constraints)
    {
        List<ReverseMove> moves = new List<ReverseMove>();
        int emptyColumnCount = CountEmptyColumns(columns, constraints);

        for (int targetShelfIndex = 0; targetShelfIndex < columns.Length; targetShelfIndex++)
        {
            if (constraints.IsClosed(targetShelfIndex))
                continue;

            List<ItemType>[] targetShelf = columns[targetShelfIndex];

            for (int targetColumnIndex = 0; targetColumnIndex < targetShelf.Length; targetColumnIndex++)
            {
                if (targetShelf[targetColumnIndex].Count != 0)
                    continue;

                for (int sourceShelfIndex = 0; sourceShelfIndex < columns.Length; sourceShelfIndex++)
                {
                    if (sourceShelfIndex == targetShelfIndex || constraints.IsClosed(sourceShelfIndex))
                        continue;

                    for (int sourceColumnIndex = 0; sourceColumnIndex < columns[sourceShelfIndex].Length; sourceColumnIndex++)
                    {
                        if (!CanPrepend(columns, targetShelfIndex, targetColumnIndex, sourceShelfIndex, sourceColumnIndex, constraints))
                            continue;

                        int filledEmptyColumnCount = CountFilledEmptyColumns(targetShelf, targetColumnIndex, columns[sourceShelfIndex][sourceColumnIndex]);

                        if (emptyColumnCount - filledEmptyColumnCount < constraints.RequiredEmptyColumnCount)
                            continue;

                        moves.Add(new ReverseMove(targetShelfIndex, targetColumnIndex, sourceShelfIndex, sourceColumnIndex, filledEmptyColumnCount));
                    }
                }
            }
        }

        return moves;
    }

    private static bool CanPrepend(
        List<ItemType>[][] columns,
        int targetShelfIndex,
        int targetColumnIndex,
        int sourceShelfIndex,
        int sourceColumnIndex,
        LayoutConstraints constraints)
    {
        if (columns[sourceShelfIndex][sourceColumnIndex].Count >= constraints.GetMaximumColumnDepth(sourceShelfIndex))
            return false;

        List<ItemType>[] targetShelf = columns[targetShelfIndex];
        int targetMaximumDepth = constraints.GetMaximumColumnDepth(targetShelfIndex);

        for (int columnIndex = 0; columnIndex < targetShelf.Length; columnIndex++)
        {
            if (columnIndex != targetColumnIndex && targetShelf[columnIndex].Count >= targetMaximumDepth)
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

    private static bool WouldCreateAdjacentDuplicate(List<ItemType>[][] columns, ReverseMove move, ItemType type, LayoutConstraints constraints)
    {
        List<ItemType>[] targetShelf = columns[move.TargetShelfIndex];
        bool isTargetConveyor = constraints.IsConveyor(move.TargetShelfIndex);

        for (int columnIndex = 0; columnIndex < targetShelf.Length; columnIndex++)
        {
            if (columnIndex == move.TargetColumnIndex)
                continue;

            if (WouldTouchSameType(targetShelf[columnIndex], type, isTargetConveyor))
                return true;
        }

        return WouldTouchSameType(columns[move.SourceShelfIndex][move.SourceColumnIndex], type, constraints.IsConveyor(move.SourceShelfIndex));
    }

    private static bool WouldTouchSameType(List<ItemType> column, ItemType type, bool isConveyor)
    {
        if (column.Count == 0)
            return false;

        return column[0] == type || isConveyor && column[column.Count - 1] == type;
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

    private static int CompareReverseMoves(ReverseMove first, ReverseMove second, List<ItemType>[][] columns, LayoutConstraints constraints)
    {
        if (constraints.HasConveyors)
        {
            int conveyorComparison = CountItemsAddedToConveyors(second, columns, constraints).CompareTo(CountItemsAddedToConveyors(first, columns, constraints));

            if (conveyorComparison != 0)
                return conveyorComparison;
        }

        int emptyComparison = second.FilledEmptyColumnCount.CompareTo(first.FilledEmptyColumnCount);

        if (emptyComparison != 0)
            return emptyComparison;

        return GetResultingDepth(first, columns).CompareTo(GetResultingDepth(second, columns));
    }

    private static int CountItemsAddedToConveyors(ReverseMove move, List<ItemType>[][] columns, LayoutConstraints constraints)
    {
        int count = constraints.IsConveyor(move.SourceShelfIndex) ? 1 : 0;

        if (constraints.IsConveyor(move.TargetShelfIndex))
            count += columns[move.TargetShelfIndex].Length - 1;

        return count;
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

    private static int CountEmptyColumns(List<ItemType>[][] columns, LayoutConstraints constraints)
    {
        int count = 0;

        for (int shelfIndex = 0; shelfIndex < columns.Length; shelfIndex++)
        {
            if (!constraints.IsClosed(shelfIndex))
                count += columns[shelfIndex].Count(column => column.Count == 0);
        }

        return count;
    }

    private static BoardStateSnapshot CreateSnapshot(IReadOnlyList<List<ItemType>[]> columns, LayoutConstraints constraints)
    {
        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[columns.Count];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
            shelves[shelfIndex] = CreateShelfSnapshot(columns, shelfIndex, constraints);

        return new BoardStateSnapshot(shelves);
    }

    private static ShelfStateSnapshot CreateShelfSnapshot(IReadOnlyList<List<ItemType>[]> columns, int shelfIndex, LayoutConstraints constraints)
    {
        return new ShelfStateSnapshot(
            columns[shelfIndex].Select(column => new ColumnStateSnapshot(column.ToArray())).ToArray(),
            isOpen: !constraints.IsClosed(shelfIndex),
            isConveyor: constraints.IsConveyor(shelfIndex));
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

    private sealed class LayoutConstraints
    {
        private readonly HashSet<int> _closedShelfIndices;
        private readonly HashSet<int> _conveyorShelfIndices;

        public int RequiredEmptyColumnCount { get; }
        public int MaximumConstructionNodeCount { get; }
        public IReadOnlyCollection<int> ConveyorShelfIndices => _conveyorShelfIndices;
        public bool HasConveyors => _conveyorShelfIndices.Count > 0;

        public LayoutConstraints(TimedLevelGenerationInput input, int maximumConstructionNodeCount)
        {
            _closedShelfIndices = new HashSet<int>(input.ClosedShelfIndices);
            _conveyorShelfIndices = new HashSet<int>(input.ConveyorShelfIndices);
            RequiredEmptyColumnCount = input.EmptyColumnCount;
            MaximumConstructionNodeCount = maximumConstructionNodeCount;
        }

        public bool IsClosed(int shelfIndex) => _closedShelfIndices.Contains(shelfIndex);

        public bool IsConveyor(int shelfIndex) => _conveyorShelfIndices.Contains(shelfIndex);

        public int GetMaximumColumnDepth(int shelfIndex)
        {
            return IsConveyor(shelfIndex) ? TimedLevelLayoutRules.MaximumConveyorColumnDepth : TimedLevelLayoutRules.MaximumColumnDepth;
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
