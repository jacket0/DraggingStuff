using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class BoardStateFingerprint
{
    public static string CreateKey(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        List<string> shelves = new List<string>();

        for (int shelfIndex = 0; shelfIndex < board.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = board.Shelves[shelfIndex];
            List<string> columns = Enumerable.Range(0, shelf.Capacity).Select(columnIndex => CreateColumnKey(board, shelfIndex, columnIndex)).ToList();

            if (!board.HasSlime)
                columns.Sort(StringComparer.Ordinal);

            string marker = !shelf.IsOpen ? "C" : shelf.IsConveyor ? "B" : shelf.IsFiltered ? $"{CreateFilterMarker(shelf)}|" : "";
            string cycleMarker = shelf.IsCyclic ? $"{CreateCycleMarker(shelf)}|" : "";
            shelves.Add($"[{marker}{cycleMarker}{string.Join("", columns)}]");
        }

        if (!HasPhysicalShelfReferences(board))
            shelves.Sort(StringComparer.Ordinal);

        string slimeMarker = board.HasSlime ? $"G{board.Slime.MovesToGrowth}|" : "";
        return slimeMarker + string.Join("", shelves);
    }

    public static string CreateHash(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        StringBuilder layout = new StringBuilder();

        for (int shelfIndex = 0; shelfIndex < board.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = board.Shelves[shelfIndex];

            if (!shelf.IsOpen)
                layout.Append('C');

            if (shelf.IsConveyor)
                layout.Append('B');

            if (shelf.IsFiltered)
                layout.Append(CreateFilterMarker(shelf));

            if (shelf.IsCyclic)
                layout.Append(CreateCycleMarker(shelf));

            layout.Append('[');

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
                layout.Append(CreateColumnKey(board, shelfIndex, columnIndex));

            layout.Append(']');
        }

        ulong hash = 14695981039346656037UL;

        for (int index = 0; index < layout.Length; index++)
        {
            char character = layout[index];
            hash ^= (byte)character;
            hash *= 1099511628211UL;
            hash ^= (byte)(character >> 8);
            hash *= 1099511628211UL;
        }

        return hash.ToString("X16");
    }

    private static bool HasPhysicalShelfReferences(BoardStateSnapshot board)
    {
        return board.HasKeys || board.HasCycles || board.HasSlime || board.Shelves.Any(shelf => !shelf.IsOpen && shelf.Columns.Any(column => column.Count > 0));
    }

    private static string CreateFilterMarker(ShelfStateSnapshot shelf)
    {
        return "F" + string.Join(".", shelf.AcceptedTypes.Select(type => (int)type));
    }

    private static string CreateCycleMarker(ShelfStateSnapshot shelf)
    {
        return $"Y{shelf.Cycle.OpenMoves}.{shelf.Cycle.ClosedMoves}.{shelf.Cycle.MovesToSwitch}";
    }

    private static string CreateColumnKey(BoardStateSnapshot board, int shelfIndex, int columnIndex)
    {
        ColumnStateSnapshot column = board.Shelves[shelfIndex].Columns[columnIndex];
        StringBuilder key = new StringBuilder(board.IsSticky(new ColumnPosition(shelfIndex, columnIndex)) ? "s(" : "(");

        foreach (ItemType type in column.Items)
            key.Append((int)type).Append(',');

        foreach (ColumnKeySnapshot columnKey in column.Keys)
            key.Append('k').Append(columnKey.ItemsInFront).Append('.').Append(columnKey.LockedShelfIndex).Append(';');

        return key.Append(')').ToString();
    }
}
