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

        foreach (ShelfStateSnapshot shelf in board.Shelves)
        {
            List<string> columns = shelf.Columns.Select(CreateColumnKey).ToList();
            columns.Sort(StringComparer.Ordinal);
            shelves.Add($"[{string.Join("", columns)}]");
        }

        shelves.Sort(StringComparer.Ordinal);
        return string.Join("", shelves);
    }

    public static string CreateHash(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        StringBuilder layout = new StringBuilder();

        foreach (ShelfStateSnapshot shelf in board.Shelves)
        {
            layout.Append('[');

            foreach (ColumnStateSnapshot column in shelf.Columns)
                layout.Append(CreateColumnKey(column));

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

    private static string CreateColumnKey(ColumnStateSnapshot column)
    {
        StringBuilder key = new StringBuilder("(");

        foreach (ItemType type in column.Items)
            key.Append((int)type).Append(',');

        return key.Append(')').ToString();
    }
}
