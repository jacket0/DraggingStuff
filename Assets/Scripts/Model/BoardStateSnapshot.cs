using System;
using System.Collections.Generic;

public sealed class BoardStateSnapshot
{
    private static readonly IReadOnlyCollection<ItemType> NoFilteredTypes = Array.Empty<ItemType>();

    public IReadOnlyList<ShelfStateSnapshot> Shelves { get; }
    public IReadOnlyCollection<ItemType> FilteredTypes { get; }
    public int EmptyColumnCount { get; }
    public int ItemCount { get; }
    public bool HasConveyors { get; }
    public bool IsCleared => ItemCount == 0;

    public BoardStateSnapshot(IReadOnlyList<ShelfStateSnapshot> shelves)
    {
        if (shelves == null)
            throw new ArgumentNullException(nameof(shelves));

        ShelfStateSnapshot[] copy = new ShelfStateSnapshot[shelves.Count];
        HashSet<ItemType> filteredTypes = null;

        for (int index = 0; index < shelves.Count; index++)
        {
            ShelfStateSnapshot shelf = shelves[index] ?? throw new ArgumentException("The board contains a null shelf.", nameof(shelves));
            copy[index] = shelf;
            HasConveyors |= shelf.IsConveyor;

            if (shelf.IsFiltered)
            {
                filteredTypes = filteredTypes ?? new HashSet<ItemType>();
                filteredTypes.UnionWith(shelf.AcceptedTypes);
            }

            foreach (ColumnStateSnapshot column in shelf.Columns)
            {
                ItemCount += column.Count;

                if (shelf.IsOpen && column.IsEmpty)
                    EmptyColumnCount++;
            }
        }

        Shelves = Array.AsReadOnly(copy);
        FilteredTypes = filteredTypes ?? NoFilteredTypes;
    }

    public bool CanMatch(int shelfIndex) => Shelves[shelfIndex].CanMatch(FilteredTypes);

    public bool Contains(ColumnPosition position)
    {
        return position.ShelfIndex >= 0 && position.ShelfIndex < Shelves.Count
            && position.ColumnIndex >= 0 && position.ColumnIndex < Shelves[position.ShelfIndex].Capacity;
    }
}
