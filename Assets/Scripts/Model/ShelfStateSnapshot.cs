using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ShelfStateSnapshot
{
    public const int MinimumCapacity = 1;
    public const int MaximumCapacity = 5;
    public const int MinimumMatchCapacity = 3;
    public const int MaximumAcceptedTypeCount = 3;

    private static readonly IReadOnlyList<ItemType> NoAcceptedTypes = Array.AsReadOnly(Array.Empty<ItemType>());

    public IReadOnlyList<ColumnStateSnapshot> Columns { get; }
    public int Capacity => Columns.Count;
    public bool IsOpen { get; }
    public bool IsConveyor { get; }
    public IReadOnlyList<ItemType> AcceptedTypes { get; }
    public bool IsFiltered => AcceptedTypes.Count > 0;

    public ShelfStateSnapshot(IReadOnlyList<ColumnStateSnapshot> columns) : this(columns, true)
    {
    }

    public ShelfStateSnapshot(IReadOnlyList<ColumnStateSnapshot> columns, bool isOpen) : this(columns, isOpen, false)
    {
    }

    public ShelfStateSnapshot(IReadOnlyList<ColumnStateSnapshot> columns, bool isOpen, bool isConveyor)
        : this(columns, isOpen, isConveyor, NoAcceptedTypes)
    {
    }

    public ShelfStateSnapshot(IReadOnlyList<ColumnStateSnapshot> columns, bool isOpen, bool isConveyor, IReadOnlyList<ItemType> acceptedTypes)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        if (acceptedTypes == null)
            throw new ArgumentNullException(nameof(acceptedTypes));

        if (isConveyor && !isOpen)
            throw new ArgumentException("A closed shelf cannot be a conveyor.", nameof(isConveyor));

        if (columns.Count < MinimumCapacity || columns.Count > MaximumCapacity)
            throw new ArgumentException("A shelf must contain between one and five columns.", nameof(columns));

        ColumnStateSnapshot[] copy = new ColumnStateSnapshot[columns.Count];

        for (int index = 0; index < columns.Count; index++)
            copy[index] = columns[index] ?? throw new ArgumentException("A shelf contains a null column.", nameof(columns));

        Columns = Array.AsReadOnly(copy);
        IsOpen = isOpen;
        IsConveyor = isConveyor;
        AcceptedTypes = NormalizeAcceptedTypes(acceptedTypes);

        if (IsFiltered && (!isOpen || isConveyor))
            throw new ArgumentException("Only an open regular shelf can be filtered.", nameof(acceptedTypes));
    }

    public bool Accepts(ItemType type) => !IsFiltered || AcceptedTypes.Contains(type);

    public ShelfStateSnapshot WithColumns(IReadOnlyList<ColumnStateSnapshot> columns)
    {
        return new ShelfStateSnapshot(columns, IsOpen, IsConveyor, AcceptedTypes);
    }

    public ShelfStateSnapshot ShiftConveyor()
    {
        ColumnStateSnapshot[] columns = new ColumnStateSnapshot[Capacity];

        for (int index = 0; index < columns.Length; index++)
            columns[index] = Columns[index].MoveFrontToBack();

        return WithColumns(columns);
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

    public bool CanMatch(IReadOnlyCollection<ItemType> filteredTypes)
    {
        if (!HasMatch())
            return false;

        ItemType type = Columns[0].Items[0];
        return AcceptedTypes.Contains(type) || !filteredTypes.Contains(type);
    }

    private static IReadOnlyList<ItemType> NormalizeAcceptedTypes(IReadOnlyList<ItemType> acceptedTypes)
    {
        if (acceptedTypes.Count == 0)
            return NoAcceptedTypes;

        ItemType[] normalizedTypes = acceptedTypes.Distinct().OrderBy(type => type).ToArray();

        if (normalizedTypes.Length > MaximumAcceptedTypeCount)
            throw new ArgumentException($"A shelf can accept at most {MaximumAcceptedTypeCount} item types.", nameof(acceptedTypes));

        return Array.AsReadOnly(normalizedTypes);
    }
}
