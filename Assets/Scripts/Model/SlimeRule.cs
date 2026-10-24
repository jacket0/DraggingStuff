using System;
using System.Collections.Generic;
using System.Linq;

public static class SlimeRule
{
    public static ColumnPosition? FindGrowthTarget(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (!board.HasSlime)
            return null;

        foreach (ColumnPosition stickyColumn in board.Slime.StickyColumns)
        {
            foreach (ColumnPosition neighbor in GetNeighborColumns(board, stickyColumn))
            {
                if (!board.Slime.IsSticky(neighbor))
                    return neighbor;
            }
        }

        return null;
    }

    public static IReadOnlyList<ColumnPosition> GetNeighborColumns(BoardStateSnapshot board, ColumnPosition position)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (!board.Contains(position))
            throw new ArgumentOutOfRangeException(nameof(position));

        List<ColumnPosition> neighbors = new List<ColumnPosition>();

        if (position.ColumnIndex > 0)
            neighbors.Add(new ColumnPosition(position.ShelfIndex, position.ColumnIndex - 1));

        if (position.ColumnIndex < board.Shelves[position.ShelfIndex].Capacity - 1)
            neighbors.Add(new ColumnPosition(position.ShelfIndex, position.ColumnIndex + 1));

        if (!board.HasSlime)
            return neighbors;

        foreach (SlimeColumnLink link in board.Slime.Links)
        {
            if (link.TryGetOtherEnd(position, out ColumnPosition otherEnd))
                neighbors.Add(otherEnd);
        }

        return neighbors;
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

    public static BoardStateSnapshot AdvanceGrowth(BoardStateSnapshot board, bool hasCleaned, out ColumnPosition? grownColumn)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        grownColumn = null;

        if (!board.HasSlime || !board.Slime.HasStickyColumns)
            return board;

        SlimeSnapshot slime = board.Slime;

        if (hasCleaned)
            return board.WithSlime(slime.WithStickyColumns(slime.StickyColumns, slime.GrowthPeriod));

        if (slime.MovesToGrowth > 1)
            return board.WithSlime(slime.WithStickyColumns(slime.StickyColumns, slime.MovesToGrowth - 1));

        grownColumn = FindGrowthTarget(board);
        IEnumerable<ColumnPosition> stickyColumns = grownColumn.HasValue ? slime.StickyColumns.Append(grownColumn.Value) : slime.StickyColumns;
        return board.WithSlime(slime.WithStickyColumns(stickyColumns, slime.GrowthPeriod));
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

    public static BoardStateSnapshot ApplyRescue(BoardStateSnapshot board, out ColumnPosition cleanedColumn)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (!board.HasSlime || !board.Slime.HasStickyColumns)
            throw new InvalidOperationException("Only a board with sticky columns can be rescued.");

        SlimeSnapshot slime = board.Slime;
        ColumnPosition[] filledColumns = slime.StickyColumns.Where(column => GetColumn(board, column).FrontItem != null).ToArray();
        ColumnPosition rescuedColumn = filledColumns.Length > 0 ? filledColumns[0] : slime.StickyColumns[0];
        cleanedColumn = rescuedColumn;
        return board.WithSlime(slime.WithStickyColumns(slime.StickyColumns.Where(column => !column.Equals(rescuedColumn)), slime.GrowthPeriod));
    }

    private static bool IsCleanedBy(SlimeSnapshot slime, int matchedShelfIndex, ColumnPosition column)
    {
        return column.ShelfIndex == matchedShelfIndex || slime.AreNeighborShelves(matchedShelfIndex, column.ShelfIndex);
    }

    private static ColumnStateSnapshot GetColumn(BoardStateSnapshot board, ColumnPosition position)
    {
        return board.Shelves[position.ShelfIndex].Columns[position.ColumnIndex];
    }
}
