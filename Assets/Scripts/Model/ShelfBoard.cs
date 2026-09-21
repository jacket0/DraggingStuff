using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ShelfBoard : MonoBehaviour
{
    [SerializeField] private List<Shelf> _shelves;

    private readonly HashSet<Shelf> _lockedShelves = new HashSet<Shelf>();
    private readonly Dictionary<ShelfColumn, ColumnPosition> _positions = new Dictionary<ShelfColumn, ColumnPosition>();
    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();
    private bool _initialized;

    public IReadOnlyList<Shelf> Shelves => _shelves;
    public bool IsCleared => _shelves.All(shelf => shelf.IsCleared);
    public bool HasLockedShelves => _lockedShelves.Count > 0;
    public ShelfItemPlacementAnimator ItemAnimations { get; } = new ShelfItemPlacementAnimator();

    public void Initialize()
    {
        if (_initialized)
            return;

        if (_shelves == null || _shelves.Count == 0 || _shelves.Any(shelf => shelf == null) || _shelves.Distinct().Count() != _shelves.Count)
            throw new InvalidOperationException("The board requires unique shelves.");

        for (int shelfIndex = 0; shelfIndex < _shelves.Count; shelfIndex++)
        {
            Shelf shelf = _shelves[shelfIndex];
            shelf.Initialize();

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
                _positions.Add(shelf.Columns[columnIndex], new ColumnPosition(shelfIndex, columnIndex));
        }

        _initialized = true;
    }

    public void InitializeViews()
    {
        Initialize();

        foreach (Shelf shelf in _shelves)
            shelf.View.Refresh();
    }

    public bool IsShelfLocked(Shelf shelf) => _lockedShelves.Contains(shelf);

    public void LockShelf(Shelf shelf)
    {
        if (shelf == null || !_shelves.Contains(shelf) || !_lockedShelves.Add(shelf))
            throw new InvalidOperationException("The shelf cannot be locked twice.");
    }

    public void UnlockShelf(Shelf shelf) => _lockedShelves.Remove(shelf);

    public bool CanPickUp(ShelfColumn column)
    {
        Initialize();
        return column != null && !column.IsEmpty && _positions.TryGetValue(column, out ColumnPosition position)
            && _shelves[position.ShelfIndex].isActiveAndEnabled && !IsShelfLocked(_shelves[position.ShelfIndex])
            && !ItemAnimations.IsAnimating(column.FrontItem);
    }

    public ShelfColumnView GetView(ShelfColumn column)
    {
        Initialize();

        if (column == null || !_positions.TryGetValue(column, out ColumnPosition position))
            throw new ArgumentException("The column does not belong to this board.", nameof(column));

        return _shelves[position.ShelfIndex].ColumnViews[position.ColumnIndex];
    }

    public BoardStateSnapshot CreateSnapshot()
    {
        Initialize();
        return new BoardStateSnapshot(_shelves.Select(shelf => shelf.CreateSnapshot()).ToArray());
    }

    public bool CanMove(ShelfColumn source, ShelfColumn target) => TrySimulateMove(source, target, out _);

    public bool TrySimulateMove(ShelfColumn source, ShelfColumn target, out BoardMoveSimulation simulation)
    {
        simulation = null;

        if (!CanPickUp(source) || target == null || !_positions.TryGetValue(target, out ColumnPosition targetPosition))
            return false;

        Shelf targetShelf = _shelves[targetPosition.ShelfIndex];

        if (!targetShelf.isActiveAndEnabled || IsShelfLocked(targetShelf) || ItemAnimations.IsAnimating(target.FrontItem))
            return false;

        ShelfStateSnapshot[] shelves = _shelves.Select(shelf => shelf.CreateSnapshot()).ToArray();

        for (int index = 0; index < shelves.Length; index++)
        {
            if (IsShelfLocked(_shelves[index]))
                shelves[index] = _simulator.ResolveMatches(new BoardStateSnapshot(new[] { shelves[index] })).State.Shelves[0];
        }

        return _simulator.TrySimulate(new BoardStateSnapshot(shelves), _positions[source], targetPosition, out simulation) && simulation.IsAllowed;
    }

    public MoveOutcome TryMove(ShelfColumn source, ShelfColumn target)
    {
        if (!TrySimulateMove(source, target, out _))
            return MoveOutcome.Rejected();

        Shelf sourceShelf = GetView(source).Shelf;
        Shelf targetShelf = GetView(target).Shelf;
        Shelf[] affected = _shelves.Where(shelf => shelf == sourceShelf || shelf == targetShelf).ToArray();

        if (target.IsEmpty)
        {
            ShelfItem item = source.TakeFront();
            target.PlaceFront(item);
            return MoveOutcome.Successful(item, source, target, affected);
        }

        ShelfItem sourceItem = source.FrontItem;
        ShelfItem targetItem = target.FrontItem;
        source.SwapFrontWith(target);
        return MoveOutcome.SuccessfulSwap(sourceItem, targetItem, source, target, affected);
    }

    private void OnDestroy() => ItemAnimations.Dispose();
}
