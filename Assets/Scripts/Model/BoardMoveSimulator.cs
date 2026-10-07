using System;
using System.Collections.Generic;

public sealed class BoardMoveSimulator
{
    public bool TrySimulate(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target, out BoardMoveSimulation simulation)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        simulation = null;

        if (!board.Contains(source) || !board.Contains(target) || source.Equals(target))
            return false;

        ShelfStateSnapshot sourceShelf = board.Shelves[source.ShelfIndex];
        ShelfStateSnapshot targetShelf = board.Shelves[target.ShelfIndex];

        if (!sourceShelf.IsOpen || !targetShelf.IsOpen)
            return false;

        ColumnStateSnapshot sourceColumn = sourceShelf.Columns[source.ColumnIndex];
        ColumnStateSnapshot targetColumn = targetShelf.Columns[target.ColumnIndex];

        if (sourceColumn.IsEmpty)
            return false;

        bool isSwap = !targetColumn.IsEmpty;

        if (isSwap && sourceColumn.Items[0] == targetColumn.Items[0])
            return false;

        if (!targetShelf.Accepts(sourceColumn.Items[0]))
            return false;

        if (isSwap && !sourceShelf.Accepts(targetColumn.Items[0]))
            return false;

        ShelfStateSnapshot[] shelves = CopyShelves(board);

        if (isSwap)
        {
            ReplaceColumn(shelves, source, ReplaceFront(sourceColumn, targetColumn.Items[0]));
            ReplaceColumn(shelves, target, ReplaceFront(targetColumn, sourceColumn.Items[0]));
        }
        else
        {
            ReplaceColumn(shelves, source, RemoveFront(sourceColumn));
            ReplaceColumn(shelves, target, new ColumnStateSnapshot(new[] { sourceColumn.Items[0] }));
        }

        bool[] affectedShelves = new bool[shelves.Length];
        affectedShelves[source.ShelfIndex] = true;
        affectedShelves[target.ShelfIndex] = true;
        List<MatchInfo> matches = ResolveCascades(shelves, affectedShelves, board.FilteredTypes);
        bool conveyorsShifted = matches.Count > 0 && board.HasConveyors;
        int matchCountAfterShift = 0;

        if (conveyorsShifted)
        {
            ShiftConveyors(shelves, affectedShelves);
            List<MatchInfo> matchesAfterShift = ResolveCascades(shelves, affectedShelves, board.FilteredTypes);
            matchCountAfterShift = matchesAfterShift.Count;
            matches.AddRange(matchesAfterShift);
        }

        simulation = CreateSimulation(shelves, board.FilteredTypes, matches, affectedShelves, isSwap, conveyorsShifted, matchCountAfterShift);
        return true;
    }

    public ShelfStateSnapshot ResolveShelfMatches(BoardStateSnapshot board, int shelfIndex)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        ShelfStateSnapshot shelf = board.Shelves[shelfIndex];

        while (shelf.CanMatch(board.FilteredTypes))
            shelf = RemoveFronts(shelf);

        return shelf;
    }

    public BoardStateSnapshot ShiftConveyors(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        ShelfStateSnapshot[] shelves = CopyShelves(board);
        ShiftConveyors(shelves, new bool[shelves.Length]);
        return new BoardStateSnapshot(shelves, board.FilteredTypes);
    }

    private static ShelfStateSnapshot[] CopyShelves(BoardStateSnapshot board)
    {
        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[board.Shelves.Count];

        for (int index = 0; index < shelves.Length; index++)
            shelves[index] = board.Shelves[index];

        return shelves;
    }

    private static void ShiftConveyors(ShelfStateSnapshot[] shelves, bool[] affectedShelves)
    {
        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            if (!shelves[shelfIndex].IsConveyor)
                continue;

            shelves[shelfIndex] = shelves[shelfIndex].ShiftConveyor();
            affectedShelves[shelfIndex] = true;
        }
    }

    private static List<MatchInfo> ResolveCascades(ShelfStateSnapshot[] shelves, bool[] affectedShelves, IReadOnlyCollection<ItemType> filteredTypes)
    {
        List<MatchInfo> matches = new List<MatchInfo>();
        bool hasMatches;

        do
        {
            hasMatches = false;

            for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
            {
                ShelfStateSnapshot shelf = shelves[shelfIndex];

                if (!shelf.CanMatch(filteredTypes))
                    continue;

                matches.Add(new MatchInfo(shelfIndex, shelf.Columns[0].Items[0], shelf.Capacity));
                shelves[shelfIndex] = RemoveFronts(shelf);

                affectedShelves[shelfIndex] = true;
                hasMatches = true;
            }
        }
        while (hasMatches);

        return matches;
    }

    private static ShelfStateSnapshot RemoveFronts(ShelfStateSnapshot shelf)
    {
        ColumnStateSnapshot[] columns = new ColumnStateSnapshot[shelf.Capacity];

        for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            columns[columnIndex] = RemoveFront(shelf.Columns[columnIndex]);

        return shelf.WithColumns(columns);
    }

    private static BoardMoveSimulation CreateSimulation(
        ShelfStateSnapshot[] shelves,
        IReadOnlyCollection<ItemType> filteredTypes,
        List<MatchInfo> matches,
        bool[] affectedShelves,
        bool isSwap,
        bool conveyorsShifted,
        int matchCountAfterShift)
    {
        List<int> affectedShelfIndexes = new List<int>();

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            if (affectedShelves[shelfIndex])
                affectedShelfIndexes.Add(shelfIndex);
        }

        return new BoardMoveSimulation(new BoardStateSnapshot(shelves, filteredTypes), matches, affectedShelfIndexes, isSwap, conveyorsShifted, matchCountAfterShift);
    }

    private static void ReplaceColumn(ShelfStateSnapshot[] shelves, ColumnPosition position, ColumnStateSnapshot column)
    {
        ShelfStateSnapshot shelf = shelves[position.ShelfIndex];
        ColumnStateSnapshot[] columns = new ColumnStateSnapshot[shelf.Capacity];

        for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            columns[columnIndex] = columnIndex == position.ColumnIndex ? column : shelf.Columns[columnIndex];

        shelves[position.ShelfIndex] = shelf.WithColumns(columns);
    }

    private static ColumnStateSnapshot RemoveFront(ColumnStateSnapshot column)
    {
        ItemType[] items = new ItemType[column.Count - 1];

        for (int index = 0; index < items.Length; index++)
            items[index] = column.Items[index + 1];

        return new ColumnStateSnapshot(items);
    }

    private static ColumnStateSnapshot ReplaceFront(ColumnStateSnapshot column, ItemType item)
    {
        ItemType[] items = new ItemType[column.Count];
        items[0] = item;

        for (int index = 1; index < items.Length; index++)
            items[index] = column.Items[index];

        return new ColumnStateSnapshot(items);
    }
}
