using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TimedLevelVariantRecolorer
{
    public TimedLevelVariant Recolor(TimedLevelDefinition definition, TimedLevelVariant variant)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (variant == null)
            throw new ArgumentNullException(nameof(variant));

        BoardStateSnapshot previousLayout = variant.CreateLayout();
        IReadOnlyList<TimedLevelMove> solution = variant.CreateSolution();
        ItemType[] itemTypes = definition.ItemGroups.Select(group => group.Type).ToArray();
        int[] remainingCounts = definition.ItemGroups.Select(group => group.GroupCount).ToArray();

        if (remainingCounts.Sum() != solution.Count)
            throw new InvalidOperationException($"{definition.name}: group count does not match the stored solution.");

        List<int[]> visibleGroupConstraints = new List<int[]>();
        List<int>[][] columns = BuildGroupLayout(previousLayout, solution, visibleGroupConstraints);
        ValidateShape(previousLayout, columns);
        bool[,] adjacency = BuildAdjacency(columns, solution.Count);
        AddVisibleGroupConstraints(columns, visibleGroupConstraints);
        int[] assignments = Enumerable.Repeat(-1, solution.Count).ToArray();

        if (!TryAssignTypes(assignments, remainingCounts, adjacency, visibleGroupConstraints, variant.Seed))
            throw new InvalidOperationException($"{definition.name}: seed {variant.Seed} could not satisfy item adjacency rules.");

        BoardStateSnapshot layout = CreateLayout(columns, assignments, itemTypes);
        ValidateSolution(definition.name, variant.Seed, layout, solution);
        return new TimedLevelVariant(
            variant.Seed,
            TimedLevelLayoutRules.GeneratorVersion,
            solution.Count,
            BoardStateFingerprint.CreateHash(layout),
            layout,
            solution);
    }

    private static List<int>[][] BuildGroupLayout(
        BoardStateSnapshot shape,
        IReadOnlyList<TimedLevelMove> solution,
        ICollection<int[]> visibleGroupConstraints)
    {
        List<int>[][] columns = new List<int>[shape.Shelves.Count][];

        for (int shelfIndex = 0; shelfIndex < columns.Length; shelfIndex++)
        {
            columns[shelfIndex] = new List<int>[shape.Shelves[shelfIndex].Capacity];

            for (int columnIndex = 0; columnIndex < columns[shelfIndex].Length; columnIndex++)
                columns[shelfIndex][columnIndex] = new List<int>();
        }

        for (int groupIndex = 0; groupIndex < solution.Count; groupIndex++)
        {
            TimedLevelMove move = solution[solution.Count - 1 - groupIndex];
            ValidatePosition(columns, move.Source);
            ValidatePosition(columns, move.Target);

            if (move.Source.ShelfIndex == move.Target.ShelfIndex)
                throw new InvalidOperationException("Stored solution contains a move within one shelf.");

            AddSourceShelfConstraint(columns[move.Source.ShelfIndex], move.Source.ColumnIndex, groupIndex, visibleGroupConstraints);
            List<int>[] targetShelf = columns[move.Target.ShelfIndex];

            for (int columnIndex = 0; columnIndex < targetShelf.Length; columnIndex++)
            {
                if (columnIndex != move.Target.ColumnIndex)
                    targetShelf[columnIndex].Insert(0, groupIndex);
            }

            columns[move.Source.ShelfIndex][move.Source.ColumnIndex].Insert(0, groupIndex);
        }

        return columns;
    }

    private static void AddSourceShelfConstraint(
        IReadOnlyList<List<int>> sourceShelf,
        int sourceColumnIndex,
        int groupIndex,
        ICollection<int[]> constraints)
    {
        List<int> visibleGroups = new List<int> { groupIndex };

        for (int columnIndex = 0; columnIndex < sourceShelf.Count; columnIndex++)
        {
            if (columnIndex == sourceColumnIndex)
                continue;

            if (sourceShelf[columnIndex].Count == 0)
                return;

            visibleGroups.Add(sourceShelf[columnIndex][0]);
        }

        AddVisibleGroupConstraint(visibleGroups, constraints);
    }

    private static void ValidatePosition(IReadOnlyList<List<int>[]> columns, ColumnPosition position)
    {
        if (position.ShelfIndex < 0
            || position.ShelfIndex >= columns.Count
            || position.ColumnIndex < 0
            || position.ColumnIndex >= columns[position.ShelfIndex].Length)
        {
            throw new InvalidOperationException("Stored solution contains an invalid column position.");
        }
    }

    private static void ValidateShape(BoardStateSnapshot previousLayout, IReadOnlyList<List<int>[]> columns)
    {
        for (int shelfIndex = 0; shelfIndex < columns.Count; shelfIndex++)
        {
            for (int columnIndex = 0; columnIndex < columns[shelfIndex].Length; columnIndex++)
            {
                if (columns[shelfIndex][columnIndex].Count != previousLayout.Shelves[shelfIndex].Columns[columnIndex].Items.Count)
                    throw new InvalidOperationException("Stored solution does not reproduce the stored layout shape.");

                if (columns[shelfIndex][columnIndex].Count > TimedLevelLayoutRules.MaximumColumnDepth)
                    throw new InvalidOperationException("Stored solution exceeds maximum column depth.");
            }
        }
    }

    private static bool[,] BuildAdjacency(IReadOnlyList<List<int>[]> columns, int groupCount)
    {
        bool[,] adjacency = new bool[groupCount, groupCount];

        foreach (List<int>[] shelf in columns)
        {
            foreach (List<int> column in shelf)
            {
                for (int itemIndex = 1; itemIndex < column.Count; itemIndex++)
                {
                    int first = column[itemIndex - 1];
                    int second = column[itemIndex];

                    if (first == second)
                        throw new InvalidOperationException("One generated group occupies adjacent positions in a column.");

                    adjacency[first, second] = true;
                    adjacency[second, first] = true;
                }
            }
        }

        return adjacency;
    }

    private static void AddVisibleGroupConstraints(IReadOnlyList<List<int>[]> columns, ICollection<int[]> constraints)
    {
        foreach (List<int>[] shelf in columns)
        {
            if (shelf.Any(column => column.Count == 0))
                continue;

            AddVisibleGroupConstraint(shelf.Select(column => column[0]), constraints);
        }
    }

    private static void AddVisibleGroupConstraint(IEnumerable<int> groups, ICollection<int[]> constraints)
    {
        int[] visibleGroups = groups.Distinct().ToArray();

        if (visibleGroups.Length < 2)
            throw new InvalidOperationException("Stored solution forces an automatic match.");

        constraints.Add(visibleGroups);
    }

    private static bool TryAssignTypes(
        int[] assignments,
        int[] remainingCounts,
        bool[,] adjacency,
        IReadOnlyList<int[]> visibleGroups,
        int seed)
    {
        int groupIndex = SelectGroup(assignments, adjacency, seed);

        if (groupIndex < 0)
            return remainingCounts.All(count => count == 0);

        foreach (int typeIndex in OrderTypes(groupIndex, remainingCounts, seed))
        {
            if (!CanAssign(groupIndex, typeIndex, assignments, adjacency))
                continue;

            assignments[groupIndex] = typeIndex;
            remainingCounts[typeIndex]--;

            if (AreVisibleGroupsValid(assignments, visibleGroups)
                && TryAssignTypes(assignments, remainingCounts, adjacency, visibleGroups, seed))
            {
                return true;
            }

            remainingCounts[typeIndex]++;
            assignments[groupIndex] = -1;
        }

        return false;
    }

    private static int SelectGroup(IReadOnlyList<int> assignments, bool[,] adjacency, int seed)
    {
        int selectedGroup = -1;
        int selectedSaturation = -1;
        int selectedDegree = -1;
        int selectedTieBreaker = -1;

        for (int groupIndex = 0; groupIndex < assignments.Count; groupIndex++)
        {
            if (assignments[groupIndex] >= 0)
                continue;

            HashSet<int> neighborTypes = new HashSet<int>();
            int degree = 0;

            for (int otherGroup = 0; otherGroup < assignments.Count; otherGroup++)
            {
                if (!adjacency[groupIndex, otherGroup])
                    continue;

                degree++;

                if (assignments[otherGroup] >= 0)
                    neighborTypes.Add(assignments[otherGroup]);
            }

            int tieBreaker = CreateOrderKey(seed, groupIndex, 0);

            if (neighborTypes.Count > selectedSaturation
                || neighborTypes.Count == selectedSaturation && degree > selectedDegree
                || neighborTypes.Count == selectedSaturation && degree == selectedDegree && tieBreaker > selectedTieBreaker)
            {
                selectedGroup = groupIndex;
                selectedSaturation = neighborTypes.Count;
                selectedDegree = degree;
                selectedTieBreaker = tieBreaker;
            }
        }

        return selectedGroup;
    }

    private static IEnumerable<int> OrderTypes(int groupIndex, IReadOnlyList<int> remainingCounts, int seed)
    {
        return Enumerable.Range(0, remainingCounts.Count)
            .Where(typeIndex => remainingCounts[typeIndex] > 0)
            .OrderByDescending(typeIndex => remainingCounts[typeIndex])
            .ThenBy(typeIndex => CreateOrderKey(seed, groupIndex, typeIndex));
    }

    private static bool CanAssign(int groupIndex, int typeIndex, IReadOnlyList<int> assignments, bool[,] adjacency)
    {
        for (int otherGroup = 0; otherGroup < assignments.Count; otherGroup++)
        {
            if (adjacency[groupIndex, otherGroup] && assignments[otherGroup] == typeIndex)
                return false;
        }

        return true;
    }

    private static bool AreVisibleGroupsValid(IReadOnlyList<int> assignments, IReadOnlyList<int[]> visibleGroups)
    {
        foreach (int[] groups in visibleGroups)
        {
            int firstType = assignments[groups[0]];

            if (firstType < 0)
                continue;

            bool allAssigned = true;
            bool allSame = true;

            for (int index = 1; index < groups.Length; index++)
            {
                int type = assignments[groups[index]];

                if (type < 0)
                {
                    allAssigned = false;
                    continue;
                }

                if (type != firstType)
                    allSame = false;
            }

            if (allAssigned && allSame)
                return false;
        }

        return true;
    }

    private static int CreateOrderKey(int seed, int first, int second)
    {
        unchecked
        {
            int value = seed;
            value = value * 397 ^ first;
            value = value * 397 ^ second;
            value ^= value >> 16;
            value *= 0x45d9f3b;
            value ^= value >> 16;
            return value & int.MaxValue;
        }
    }

    private static BoardStateSnapshot CreateLayout(
        IReadOnlyList<List<int>[]> columns,
        IReadOnlyList<int> assignments,
        IReadOnlyList<ItemType> itemTypes)
    {
        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[columns.Count];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            shelves[shelfIndex] = new ShelfStateSnapshot(columns[shelfIndex]
                .Select(column => new ColumnStateSnapshot(
                    column.Select(groupIndex => itemTypes[assignments[groupIndex]]).ToArray()))
                .ToArray());
        }

        return new BoardStateSnapshot(shelves);
    }

    private static void ValidateSolution(
        string definitionName,
        int seed,
        BoardStateSnapshot layout,
        IReadOnlyList<TimedLevelMove> solution)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = layout;

        foreach (TimedLevelMove move in solution)
        {
            if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                || !simulation.IsAllowed
                || simulation.MatchCount != 1)
            {
                throw new InvalidOperationException($"{definitionName}: seed {seed} no longer matches its stored solution.");
            }

            state = simulation.State;
        }

        if (!state.IsCleared)
            throw new InvalidOperationException($"{definitionName}: seed {seed} does not clear the board.");
    }
}
