using System;
using System.Collections.Generic;

public sealed class BoardStateSnapshot
{
    public IReadOnlyList<ShelfStateSnapshot> Shelves { get; }
    public int EmptyColumnCount { get; }
    public int ItemCount { get; }
    public bool IsCleared => ItemCount == 0;

    public BoardStateSnapshot(IReadOnlyList<ShelfStateSnapshot> shelves)
    {
        if (shelves == null)
            throw new ArgumentNullException(nameof(shelves));

        ShelfStateSnapshot[] copy = new ShelfStateSnapshot[shelves.Count];

        for (int index = 0; index < shelves.Count; index++)
        {
            ShelfStateSnapshot shelf = shelves[index] ?? throw new ArgumentException("The board contains a null shelf.", nameof(shelves));
            copy[index] = shelf;

            foreach (ColumnStateSnapshot column in shelf.Columns)
            {
                ItemCount += column.Count;

                if (column.IsEmpty)
                    EmptyColumnCount++;
            }
        }

        Shelves = Array.AsReadOnly(copy);
    }

    public bool Contains(ColumnPosition position)
    {
        return position.ShelfIndex >= 0 && position.ShelfIndex < Shelves.Count
            && position.ColumnIndex >= 0 && position.ColumnIndex < Shelves[position.ShelfIndex].Capacity;
    }
}
