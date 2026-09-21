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

        ColumnStateSnapshot sourceColumn = board.Shelves[source.ShelfIndex].Columns[source.ColumnIndex];
        ColumnStateSnapshot targetColumn = board.Shelves[target.ShelfIndex].Columns[target.ColumnIndex];

        if (sourceColumn.IsEmpty)
            return false;

        bool isSwap = !targetColumn.IsEmpty;

        if (isSwap && sourceColumn.Items[0] == targetColumn.Items[0])
            return false;

        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[board.Shelves.Count];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
            shelves[shelfIndex] = board.Shelves[shelfIndex];

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
        simulation = Resolve(shelves, affectedShelves, isSwap);
        return true;
    }

    public BoardMoveSimulation ResolveMatches(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[board.Shelves.Count];

        for (int index = 0; index < shelves.Length; index++)
            shelves[index] = board.Shelves[index];

        return Resolve(shelves, new bool[shelves.Length], false);
    }

    private static BoardMoveSimulation Resolve(ShelfStateSnapshot[] shelves, bool[] affectedShelves, bool isSwap)
    {
        int matchCount = 0;
        bool hasMatches;

        do
        {
            hasMatches = false;

            for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
            {
                ShelfStateSnapshot shelf = shelves[shelfIndex];

                if (!shelf.HasMatch())
                    continue;

                ColumnStateSnapshot[] columns = new ColumnStateSnapshot[shelf.Capacity];

                for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
                    columns[columnIndex] = RemoveFront(shelf.Columns[columnIndex]);

                shelves[shelfIndex] = new ShelfStateSnapshot(columns);

                affectedShelves[shelfIndex] = true;
                matchCount++;
                hasMatches = true;
            }
        }
        while (hasMatches);

        List<int> affectedShelfIndexes = new List<int>();

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            if (affectedShelves[shelfIndex])
                affectedShelfIndexes.Add(shelfIndex);
        }

        return new BoardMoveSimulation(new BoardStateSnapshot(shelves), matchCount, affectedShelfIndexes, isSwap);
    }

    private static void ReplaceColumn(ShelfStateSnapshot[] shelves, ColumnPosition position, ColumnStateSnapshot column)
    {
        ShelfStateSnapshot shelf = shelves[position.ShelfIndex];
        ColumnStateSnapshot[] columns = new ColumnStateSnapshot[shelf.Capacity];

        for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            columns[columnIndex] = columnIndex == position.ColumnIndex ? column : shelf.Columns[columnIndex];

        shelves[position.ShelfIndex] = new ShelfStateSnapshot(columns);
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
