using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BoardStateSnapshot
{
    private static readonly IReadOnlyCollection<ItemType> NoFilteredTypes = Array.Empty<ItemType>();

    public IReadOnlyList<ShelfStateSnapshot> Shelves { get; }
    public IReadOnlyCollection<ItemType> FilteredTypes { get; }
    public int EmptyColumnCount { get; }
    public int ItemCount { get; }
    public bool HasConveyors { get; }
    public bool HasCycles { get; }
    public int KeyCount { get; }
    public bool HasKeys => KeyCount > 0;
    public bool IsCleared => ItemCount == 0;
    public SlimeSnapshot Slime { get; }
    public bool HasSlime => Slime != null;

    public BoardStateSnapshot(IReadOnlyList<ShelfStateSnapshot> shelves) : this(shelves, CollectShelfFilteredTypes(shelves))
    {
    }

    public BoardStateSnapshot(IReadOnlyList<ShelfStateSnapshot> shelves, IReadOnlyCollection<ItemType> filteredTypes) : this(shelves, filteredTypes, null)
    {
    }

    public BoardStateSnapshot(IReadOnlyList<ShelfStateSnapshot> shelves, IReadOnlyCollection<ItemType> filteredTypes, SlimeSnapshot slime)
    {
        if (shelves == null)
            throw new ArgumentNullException(nameof(shelves));

        ShelfStateSnapshot[] copy = new ShelfStateSnapshot[shelves.Count];

        for (int index = 0; index < shelves.Count; index++)
        {
            ShelfStateSnapshot shelf = shelves[index] ?? throw new ArgumentException("The board contains a null shelf.", nameof(shelves));
            copy[index] = shelf;
            HasConveyors |= shelf.IsConveyor;
            HasCycles |= shelf.IsCyclic;

            foreach (ColumnStateSnapshot column in shelf.Columns)
            {
                ItemCount += column.Count;
                KeyCount += column.Keys.Count;

                if (shelf.IsOpen && column.IsEmpty)
                    EmptyColumnCount++;
            }
        }

        Shelves = Array.AsReadOnly(copy);
        FilteredTypes = filteredTypes ?? throw new ArgumentNullException(nameof(filteredTypes));

        if (slime != null)
            ValidateSlime(slime);

        Slime = slime;
    }

    public BoardStateSnapshot WithShelves(IReadOnlyList<ShelfStateSnapshot> shelves) => new BoardStateSnapshot(shelves, FilteredTypes, Slime);

    public BoardStateSnapshot WithSlime(SlimeSnapshot slime) => new BoardStateSnapshot(Shelves, FilteredTypes, slime);

    public bool IsSticky(ColumnPosition position) => HasSlime && Slime.IsSticky(position);

    public static IReadOnlyCollection<ItemType> CollectFilteredTypes(IEnumerable<IReadOnlyList<ItemType>> acceptedTypes)
    {
        HashSet<ItemType> filteredTypes = new HashSet<ItemType>();

        foreach (IReadOnlyList<ItemType> types in acceptedTypes)
            filteredTypes.UnionWith(types);

        return filteredTypes.Count > 0 ? filteredTypes : NoFilteredTypes;
    }

    public bool CanMatch(int shelfIndex) => Shelves[shelfIndex].CanMatch(FilteredTypes);

    public bool Contains(ColumnPosition position)
    {
        return position.ShelfIndex >= 0 && position.ShelfIndex < Shelves.Count
            && position.ColumnIndex >= 0 && position.ColumnIndex < Shelves[position.ShelfIndex].Capacity;
    }

    private void ValidateSlime(SlimeSnapshot slime)
    {
        if (HasConveyors || HasCycles || HasKeys || Shelves.Any(shelf => !shelf.IsOpen || shelf.IsFiltered))
            throw new ArgumentException("Slime cannot share a board with other shelf mechanics.", nameof(slime));

        if (slime.StickyColumns.Any(column => !Contains(column)))
            throw new ArgumentException("A sticky column must belong to the board.", nameof(slime));

        if (slime.Links.Any(link => !Contains(link.First) || !Contains(link.Second)))
            throw new ArgumentException("A slime link must connect columns of the board.", nameof(slime));
    }

    private static IReadOnlyCollection<ItemType> CollectShelfFilteredTypes(IReadOnlyList<ShelfStateSnapshot> shelves)
    {
        return shelves != null ? CollectFilteredTypes(shelves.Where(shelf => shelf != null).Select(shelf => shelf.AcceptedTypes)) : NoFilteredTypes;
    }
}
