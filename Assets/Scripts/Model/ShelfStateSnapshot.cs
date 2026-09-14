using System;
using System.Collections.Generic;

public sealed class ShelfStateSnapshot
{
    public const int MinimumCapacity = 1;
    public const int MaximumCapacity = 5;
    public const int MinimumMatchCapacity = 3;

    public IReadOnlyList<ColumnStateSnapshot> Columns { get; }
    public int Capacity => Columns.Count;

    public ShelfStateSnapshot(IReadOnlyList<ColumnStateSnapshot> columns)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        if (columns.Count < MinimumCapacity || columns.Count > MaximumCapacity)
            throw new ArgumentException("A shelf must contain between one and five columns.", nameof(columns));

        ColumnStateSnapshot[] copy = new ColumnStateSnapshot[columns.Count];

        for (int index = 0; index < columns.Count; index++)
            copy[index] = columns[index] ?? throw new ArgumentException("A shelf contains a null column.", nameof(columns));

        Columns = Array.AsReadOnly(copy);
    }

    public bool HasMatch()
    {
        if (Capacity < MinimumMatchCapacity || Columns[0].IsEmpty)
            return false;

        ItemType type = Columns[0].Items[0];

        foreach (ColumnStateSnapshot column in Columns)
        {
            if (column.IsEmpty || column.Items[0] != type)
                return false;
        }

        return true;
    }
}
