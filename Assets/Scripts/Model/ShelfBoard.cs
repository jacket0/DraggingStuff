using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ShelfBoard : MonoBehaviour
{
    [SerializeField] private List<Shelf> _shelves;

    private readonly HashSet<Shelf> _lockedShelves = new HashSet<Shelf>();
    private readonly HashSet<Shelf> _closedShelves = new HashSet<Shelf>();
    private readonly HashSet<Shelf> _conveyorShelves = new HashSet<Shelf>();
    private readonly Dictionary<Shelf, IReadOnlyList<ItemType>> _shelfFilters = new Dictionary<Shelf, IReadOnlyList<ItemType>>();
    private IReadOnlyCollection<ItemType> _filteredTypes = Array.Empty<ItemType>();
    private readonly Dictionary<ShelfColumn, ColumnPosition> _positions = new Dictionary<ShelfColumn, ColumnPosition>();
    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();
    private bool _initialized;

    public IReadOnlyList<Shelf> Shelves => _shelves;
    public bool IsCleared => _closedShelves.Count == 0 && _shelves.All(shelf => shelf.IsCleared);
    public bool HasLockedShelves => _lockedShelves.Count > 0;
    public bool HasClosedShelves => _closedShelves.Count > 0;
    public bool HasConveyors => _conveyorShelves.Count > 0;
    public IReadOnlyList<Shelf> ConveyorShelves => _shelves.Where(IsConveyor).ToArray();
    public bool HasFilters => _shelfFilters.Count > 0;
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

    public bool IsShelfClosed(Shelf shelf) => _closedShelves.Contains(shelf);

    public bool IsShelfAvailable(Shelf shelf) => shelf.isActiveAndEnabled && !IsShelfLocked(shelf) && !IsShelfClosed(shelf);

    public void CloseShelf(Shelf shelf)
    {
        if (shelf == null || !_shelves.Contains(shelf) || !shelf.IsCleared || IsConveyor(shelf) || IsFiltered(shelf) || !_closedShelves.Add(shelf))
            throw new InvalidOperationException("Only an empty shelf on this board can be closed.");
    }

    public void OpenShelf(Shelf shelf) => _closedShelves.Remove(shelf);

    public bool IsConveyor(Shelf shelf) => _conveyorShelves.Contains(shelf);

    public void MarkConveyor(Shelf shelf)
    {
        if (shelf == null || !_shelves.Contains(shelf) || IsShelfClosed(shelf) || IsFiltered(shelf) || !_conveyorShelves.Add(shelf))
            throw new InvalidOperationException("Only an open shelf on this board can be marked as a conveyor once.");
    }

    public bool IsFiltered(Shelf shelf) => shelf != null && _shelfFilters.ContainsKey(shelf);

    public IReadOnlyList<ItemType> GetAcceptedTypes(Shelf shelf)
    {
        return shelf != null && _shelfFilters.TryGetValue(shelf, out IReadOnlyList<ItemType> acceptedTypes) ? acceptedTypes : Array.Empty<ItemType>();
    }

    public bool Accepts(Shelf shelf, ItemType type) => !IsFiltered(shelf) || _shelfFilters[shelf].Contains(type);

    public void SetFilter(Shelf shelf, IReadOnlyList<ItemType> acceptedTypes)
    {
        if (shelf == null || !_shelves.Contains(shelf) || IsShelfClosed(shelf) || IsConveyor(shelf) || IsFiltered(shelf))
            throw new InvalidOperationException("Only an open regular shelf on this board can be filtered once.");

        ItemType[] normalizedTypes = acceptedTypes?.Distinct().OrderBy(type => type).ToArray() ?? Array.Empty<ItemType>();

        if (normalizedTypes.Length == 0 || normalizedTypes.Length > ShelfStateSnapshot.MaximumAcceptedTypeCount)
            throw new InvalidOperationException($"A shelf filter must accept one to {ShelfStateSnapshot.MaximumAcceptedTypeCount} item types.");

        _shelfFilters.Add(shelf, Array.AsReadOnly(normalizedTypes));
        _filteredTypes = BoardStateSnapshot.CollectFilteredTypes(_shelfFilters.Values);
    }

    public bool CanMatch(Shelf shelf) => CreateShelfSnapshot(shelf).CanMatch(_filteredTypes);

    public bool CanMatchType(Shelf shelf, ItemType type) => ShelfStateSnapshot.CanMatchType(GetAcceptedTypes(shelf), type, _filteredTypes);

    public bool TryResolveMatch(Shelf shelf, out MatchResolution match)
    {
        match = null;
        return CanMatch(shelf) && shelf.TryResolveMatch(out match);
    }

    public bool CanPickUp(ShelfColumn column)
    {
        Initialize();
        return column != null && !column.IsEmpty && _positions.TryGetValue(column, out ColumnPosition position)
            && IsShelfAvailable(_shelves[position.ShelfIndex])
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
        return new BoardStateSnapshot(_shelves.Select(CreateShelfSnapshot).ToArray(), _filteredTypes);
    }

    public bool CanMove(ShelfColumn source, ShelfColumn target) => TrySimulateMove(source, target, out _);

    public bool TrySimulateMove(ShelfColumn source, ShelfColumn target, out BoardMoveSimulation simulation)
    {
        simulation = null;

        if (!CanPickUp(source) || target == null || !_positions.TryGetValue(target, out ColumnPosition targetPosition))
            return false;

        Shelf targetShelf = _shelves[targetPosition.ShelfIndex];

        if (!IsShelfAvailable(targetShelf) || ItemAnimations.IsAnimating(target.FrontItem))
            return false;

        BoardStateSnapshot board = CreateSnapshot();
        ShelfStateSnapshot[] shelves = board.Shelves.ToArray();

        for (int index = 0; index < shelves.Length; index++)
        {
            if (IsShelfLocked(_shelves[index]))
                shelves[index] = _simulator.ResolveShelfMatches(board, index);
        }

        return _simulator.TrySimulate(new BoardStateSnapshot(shelves, board.FilteredTypes), _positions[source], targetPosition, out simulation) && simulation.IsAllowed;
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

    private ShelfStateSnapshot CreateShelfSnapshot(Shelf shelf) => shelf.CreateSnapshot(!IsShelfClosed(shelf), IsConveyor(shelf), GetAcceptedTypes(shelf));

    private void OnDestroy() => ItemAnimations.Dispose();
}
