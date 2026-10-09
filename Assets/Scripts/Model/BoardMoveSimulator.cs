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
        ItemType? sourceItem = sourceColumn.FrontItem;
        ItemType? targetItem = targetColumn.FrontItem;

        if (sourceItem == null || !targetColumn.IsEmpty && targetItem == null)
            return false;

        bool isSwap = !targetColumn.IsEmpty;

        if (isSwap && sourceItem == targetItem)
            return false;

        if (!targetShelf.Accepts(sourceItem.Value))
            return false;

        if (isSwap && !sourceShelf.Accepts(targetItem.Value))
            return false;

        ShelfStateSnapshot[] shelves = CopyShelves(board);

        if (isSwap)
        {
            ReplaceColumn(shelves, source, sourceColumn.ReplaceFrontItem(targetItem.Value));
            ReplaceColumn(shelves, target, targetColumn.ReplaceFrontItem(sourceItem.Value));
        }
        else
        {
            ReplaceColumn(shelves, source, sourceColumn.RemoveFrontItem());
            ReplaceColumn(shelves, target, new ColumnStateSnapshot(new[] { sourceItem.Value }));
        }

        bool[] affectedShelves = new bool[shelves.Length];
        affectedShelves[source.ShelfIndex] = true;
        affectedShelves[target.ShelfIndex] = true;
        List<int> openedShelfIndexes = new List<int>();
        List<MatchInfo> matches = ResolveBoard(shelves, affectedShelves, openedShelfIndexes, board.FilteredTypes);
        bool conveyorsShifted = matches.Count > 0 && board.HasConveyors;
        int matchCountAfterShift = 0;

        if (conveyorsShifted)
        {
            ShiftConveyors(shelves, affectedShelves);
            List<MatchInfo> matchesAfterShift = ResolveBoard(shelves, affectedShelves, openedShelfIndexes, board.FilteredTypes);
            matchCountAfterShift = matchesAfterShift.Count;
            matches.AddRange(matchesAfterShift);
        }

        simulation = CreateSimulation(shelves, board.FilteredTypes, matches, affectedShelves, openedShelfIndexes, isSwap, conveyorsShifted, matchCountAfterShift);
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

    public BoardStateSnapshot CollectFrontKeys(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        ShelfStateSnapshot[] shelves = CopyShelves(board);
        CollectFrontKeys(shelves, new bool[shelves.Length], new List<int>());
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

    private static List<MatchInfo> ResolveBoard(
        ShelfStateSnapshot[] shelves,
        bool[] affectedShelves,
        List<int> openedShelfIndexes,
        IReadOnlyCollection<ItemType> filteredTypes)
    {
        List<MatchInfo> matches = new List<MatchInfo>();
        bool hasChanges;

        do
        {
            int collectedKeyCount = CollectFrontKeys(shelves, affectedShelves, openedShelfIndexes);
            List<MatchInfo> cascadeMatches = ResolveCascades(shelves, affectedShelves, filteredTypes);
            matches.AddRange(cascadeMatches);
            hasChanges = collectedKeyCount > 0 || cascadeMatches.Count > 0;
        }
        while (hasChanges);

        return matches;
    }

    private static int CollectFrontKeys(ShelfStateSnapshot[] shelves, bool[] affectedShelves, List<int> openedShelfIndexes)
    {
        int collectedKeyCount = 0;

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            for (int columnIndex = 0; columnIndex < shelves[shelfIndex].Capacity; columnIndex++)
            {
                ColumnStateSnapshot column = shelves[shelfIndex].Columns[columnIndex];

                if (!column.HasFrontKey)
                    continue;

                ReplaceColumn(shelves, new ColumnPosition(shelfIndex, columnIndex), column.RemoveFrontKey(out ColumnKeySnapshot key));
                OpenLockedShelf(shelves, key.LockedShelfIndex);
                affectedShelves[shelfIndex] = true;
                affectedShelves[key.LockedShelfIndex] = true;
                openedShelfIndexes.Add(key.LockedShelfIndex);
                collectedKeyCount++;
            }
        }

        return collectedKeyCount;
    }

    private static void OpenLockedShelf(ShelfStateSnapshot[] shelves, int shelfIndex)
    {
        if (shelfIndex >= shelves.Length || shelves[shelfIndex].IsOpen)
            throw new InvalidOperationException("A key must open a closed shelf on the board.");

        shelves[shelfIndex] = shelves[shelfIndex].WithOpenState(true);
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

                matches.Add(new MatchInfo(shelfIndex, shelf.Columns[0].FrontItem.Value, shelf.Capacity));
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
            columns[columnIndex] = shelf.Columns[columnIndex].RemoveFrontItem();

        return shelf.WithColumns(columns);
    }

    private static BoardMoveSimulation CreateSimulation(
        ShelfStateSnapshot[] shelves,
        IReadOnlyCollection<ItemType> filteredTypes,
        List<MatchInfo> matches,
        bool[] affectedShelves,
        List<int> openedShelfIndexes,
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

        return new BoardMoveSimulation(
            new BoardStateSnapshot(shelves, filteredTypes),
            matches,
            affectedShelfIndexes,
            openedShelfIndexes,
            isSwap,
            conveyorsShifted,
            matchCountAfterShift);
    }

    private static void ReplaceColumn(ShelfStateSnapshot[] shelves, ColumnPosition position, ColumnStateSnapshot column)
    {
        ShelfStateSnapshot shelf = shelves[position.ShelfIndex];
        ColumnStateSnapshot[] columns = new ColumnStateSnapshot[shelf.Capacity];

        for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            columns[columnIndex] = columnIndex == position.ColumnIndex ? column : shelf.Columns[columnIndex];

        shelves[position.ShelfIndex] = shelf.WithColumns(columns);
    }
}
