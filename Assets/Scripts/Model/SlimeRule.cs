using System;
using System.Collections.Generic;
using System.Linq;

public static class SlimeRule
{
    public static ColumnPosition? FindGrowthTarget(BoardStateSnapshot board)
    {
        IReadOnlyList<ColumnPosition> targets = FindGrowthTargets(board, 1);
        return targets.Count > 0 ? targets[0] : (ColumnPosition?)null;
    }

    public static IReadOnlyList<ColumnPosition> FindGrowthTargets(BoardStateSnapshot board, int count)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (!board.HasSlime)
            return Array.Empty<ColumnPosition>();

        return board.Slime.PerimeterColumns.Where(column => !board.Slime.IsSticky(column)).Take(count).ToArray();
    }

    public static BoardStateSnapshot Clean(BoardStateSnapshot board, IEnumerable<int> matchedShelfIndexes, out IReadOnlyList<ColumnPosition> cleanedColumns)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (matchedShelfIndexes == null)
            throw new ArgumentNullException(nameof(matchedShelfIndexes));

        cleanedColumns = Array.Empty<ColumnPosition>();

        if (!board.HasSlime)
            return board;

        HashSet<int> matchedShelves = new HashSet<int>(matchedShelfIndexes);
        ColumnPosition[] cleaned = board.Slime.StickyColumns
            .Where(column => matchedShelves.Any(shelfIndex => IsCleanedBy(board.Slime, shelfIndex, column)))
            .ToArray();

        if (cleaned.Length == 0)
            return board;

        cleanedColumns = Array.AsReadOnly(cleaned);
        return board.WithSlime(board.Slime.WithStickyColumns(board.Slime.StickyColumns.Except(cleaned), board.Slime.MovesToGrowth));
    }

    public static BoardStateSnapshot AdvanceGrowth(BoardStateSnapshot board, out IReadOnlyList<ColumnPosition> grownColumns)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        grownColumns = Array.Empty<ColumnPosition>();

        if (!board.HasSlime)
            return board;

        SlimeSnapshot slime = board.Slime;

        if (slime.MovesToGrowth > 1)
            return board.WithSlime(slime.WithStickyColumns(slime.StickyColumns, slime.MovesToGrowth - 1));

        grownColumns = FindGrowthTargets(board, slime.Rules.ColumnsPerGrowth);
        return board.WithSlime(slime.WithStickyColumns(slime.StickyColumns.Concat(grownColumns), slime.GrowthPeriod));
    }

    public static bool IsStuck(BoardStateSnapshot board, BoardMoveSimulator simulator)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (simulator == null)
            throw new ArgumentNullException(nameof(simulator));

        if (!board.HasSlime || !board.Slime.HasStickyColumns || board.IsCleared)
            return false;

        ColumnPosition[] columns = board.Shelves
            .SelectMany((shelf, shelfIndex) => Enumerable.Range(0, shelf.Capacity).Select(columnIndex => new ColumnPosition(shelfIndex, columnIndex)))
            .ToArray();

        foreach (ColumnPosition source in columns)
        {
            foreach (ColumnPosition target in columns)
            {
                if (simulator.TrySimulate(board, source, target, out BoardMoveSimulation simulation) && simulation.IsAllowed)
                    return false;
            }
        }

        return true;
    }

    public static bool HasHopelessShelf(BoardStateSnapshot board) => FindHopelessShelf(board) >= 0;

    public static int FindHopelessShelf(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (!board.HasSlime || board.Slime.Rules.CleansNeighborShelves)
            return -1;

        for (int shelfIndex = 0; shelfIndex < board.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = board.Shelves[shelfIndex];
            ItemType[] stickyFronts = Enumerable.Range(0, shelf.Capacity)
                .Where(columnIndex => board.Slime.IsSticky(new ColumnPosition(shelfIndex, columnIndex)) && shelf.Columns[columnIndex].FrontItem.HasValue)
                .Select(columnIndex => shelf.Columns[columnIndex].FrontItem.Value)
                .ToArray();

            if (stickyFronts.Distinct().Count() > 1)
                return shelfIndex;
        }

        return -1;
    }

    private static bool IsCleanedBy(SlimeSnapshot slime, int matchedShelfIndex, ColumnPosition column)
    {
        return column.ShelfIndex == matchedShelfIndex || slime.Rules.CleansNeighborShelves && slime.AreNeighborShelves(matchedShelfIndex, column.ShelfIndex);
    }
}
