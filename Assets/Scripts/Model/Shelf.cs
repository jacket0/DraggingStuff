using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class Shelf : MonoBehaviour
{
    public const int MinimumCapacity = ShelfStateSnapshot.MinimumCapacity;
    public const int MaximumCapacity = ShelfStateSnapshot.MaximumCapacity;
    public const int MinimumMatchCapacity = ShelfStateSnapshot.MinimumMatchCapacity;

    [SerializeField] private List<ShelfColumnView> _columnViews = new List<ShelfColumnView>();
    [SerializeField] private ShelfView _view;

    private IReadOnlyList<ShelfColumn> _columns;

    public int Capacity => _columnViews.Count;
    public IReadOnlyList<ShelfColumn> Columns { get { Initialize(); return _columns; } }
    public IReadOnlyList<ShelfColumnView> ColumnViews => _columnViews;
    public ShelfView View => _view;
    public bool IsCleared => Columns.All(column => column.IsEmpty);

    public static bool IsValidCapacity(int capacity) => capacity >= MinimumCapacity && capacity <= MaximumCapacity;

    public void Initialize()
    {
        if (_columns != null)
            return;

        if (!IsValidCapacity(Capacity) || _view == null || _view.Columns.Count != Capacity)
            throw new InvalidOperationException($"{name}: invalid column configuration.");

        HashSet<ShelfColumnView> uniqueViews = new HashSet<ShelfColumnView>();
        ShelfColumn[] columns = new ShelfColumn[Capacity];

        for (int index = 0; index < Capacity; index++)
        {
            ShelfColumnView view = _columnViews[index];

            if (view == null || !uniqueViews.Add(view) || _view.Columns[index] != view)
                throw new InvalidOperationException($"{name}: duplicate or missing column view.");

            columns[index] = new ShelfColumn();
            view.Bind(this, columns[index]);
        }

        _columns = Array.AsReadOnly(columns);
    }

    public ShelfStateSnapshot CreateSnapshot()
    {
        return new ShelfStateSnapshot(Columns.Select(column => new ColumnStateSnapshot(column.Items.Select(item => item.Type).ToArray())).ToArray());
    }

    public bool HasMatch() => CreateSnapshot().HasMatch();

    public bool TryResolveMatch(out MatchResolution match)
    {
        match = null;

        if (!HasMatch())
            return false;

        match = new MatchResolution(Columns.Select(column => column.TakeFront()).ToArray());
        return true;
    }
}
