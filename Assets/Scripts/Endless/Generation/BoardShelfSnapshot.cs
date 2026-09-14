using System;
using System.Collections.Generic;

public sealed class BoardShelfSnapshot
{
    public int Capacity => Columns.Count;
    public IReadOnlyList<BoardColumnSnapshot> Columns { get; }

    public BoardShelfSnapshot(IReadOnlyList<BoardColumnSnapshot> columns)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        if (!Shelf.IsValidCapacity(columns.Count))
            throw new ArgumentException("A shelf must contain between one and five columns.", nameof(columns));

        BoardColumnSnapshot[] copy = new BoardColumnSnapshot[columns.Count];

        for (int index = 0; index < columns.Count; index++)
            copy[index] = columns[index] ?? throw new ArgumentException("A column is missing.", nameof(columns));

        Columns = Array.AsReadOnly(copy);
    }
}
