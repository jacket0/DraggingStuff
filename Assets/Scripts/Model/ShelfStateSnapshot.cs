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
    public bool HasKeys => Columns.Any(column => column.HasKeys);
    public ShelfCycleSnapshot Cycle { get; }
    public bool IsCyclic => Cycle != null;

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
        : this(columns, isOpen, isConveyor, acceptedTypes, null)
    {
    }

    public ShelfStateSnapshot(
        IReadOnlyList<ColumnStateSnapshot> columns,
        bool isOpen,
        bool isConveyor,
        IReadOnlyList<ItemType> acceptedTypes,
        ShelfCycleSnapshot cycle)
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

        if (cycle != null && (isConveyor || IsFiltered))
            throw new ArgumentException("Only a regular shelf can be cyclic.", nameof(cycle));

        Cycle = cycle;
    }

    public bool Accepts(ItemType type) => !IsFiltered || AcceptedTypes.Contains(type);

    public ShelfStateSnapshot WithColumns(IReadOnlyList<ColumnStateSnapshot> columns)
    {
        return new ShelfStateSnapshot(columns, IsOpen, IsConveyor, AcceptedTypes, Cycle);
    }

    public ShelfStateSnapshot WithOpenState(bool isOpen)
    {
        return new ShelfStateSnapshot(Columns, isOpen, IsConveyor, AcceptedTypes, Cycle);
    }

    public ShelfStateSnapshot WithCycle(ShelfCycleSnapshot cycle, bool isOpen)
    {
        return new ShelfStateSnapshot(Columns, isOpen, IsConveyor, AcceptedTypes, cycle);
    }

    public ShelfStateSnapshot AdvanceCycle(int moves, out bool switched)
    {
        if (!IsCyclic)
            throw new InvalidOperationException("Only a cyclic shelf can advance its cycle.");

        ShelfCycleSnapshot cycle = Cycle.Advance(IsOpen, moves, out switched);
        return WithCycle(cycle, switched ? !IsOpen : IsOpen);
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
        ItemType? type = Columns[0].FrontItem;

        if (Capacity < MinimumMatchCapacity || type == null)
            return false;

        foreach (ColumnStateSnapshot column in Columns)
        {
            if (column.FrontItem != type)
                return false;
        }

        return true;
    }

    public bool CanMatch(IReadOnlyCollection<ItemType> filteredTypes) => IsOpen && HasMatch() && CanMatchType(Columns[0].FrontItem.Value, filteredTypes);

    public bool CanMatchType(ItemType type, IReadOnlyCollection<ItemType> filteredTypes) => CanMatchType(AcceptedTypes, type, filteredTypes);

    public static bool CanMatchType(IReadOnlyList<ItemType> acceptedTypes, ItemType type, IReadOnlyCollection<ItemType> filteredTypes)
    {
        return acceptedTypes.Contains(type) || !filteredTypes.Contains(type);
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
