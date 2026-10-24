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
    private readonly HashSet<Shelf> _keyLockedShelves = new HashSet<Shelf>();
    private readonly Dictionary<Shelf, ShelfCycleSnapshot> _shelfCycles = new Dictionary<Shelf, ShelfCycleSnapshot>();
    private readonly Dictionary<Shelf, bool> _cyclePhases = new Dictionary<Shelf, bool>();
    private readonly Dictionary<Shelf, IReadOnlyList<ItemType>> _shelfFilters = new Dictionary<Shelf, IReadOnlyList<ItemType>>();
    private IReadOnlyCollection<ItemType> _filteredTypes = Array.Empty<ItemType>();
    private readonly Dictionary<ShelfColumn, ColumnPosition> _positions = new Dictionary<ShelfColumn, ColumnPosition>();
    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();
    private SlimeSnapshot _slime;
    private bool _initialized;

    public IReadOnlyList<Shelf> Shelves => _shelves;
    public bool IsCleared => _closedShelves.All(IsCyclic) && _shelves.All(shelf => shelf.IsCleared);
    public bool HasLockedShelves => _lockedShelves.Count > 0;
    public bool HasClosedShelves => _closedShelves.Count > 0;
    public bool HasConveyors => _conveyorShelves.Count > 0;
    public IReadOnlyList<Shelf> ConveyorShelves => _shelves.Where(IsConveyor).ToArray();
    public bool HasFilters => _shelfFilters.Count > 0;
    public bool HasKeyLocks => _keyLockedShelves.Count > 0;
    public bool HasCycles => _shelfCycles.Count > 0;
    public bool HasSlime => _slime != null;
    public SlimeSnapshot Slime => _slime;
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
        if (shelf == null || !_shelves.Contains(shelf) || !shelf.IsCleared || IsConveyor(shelf) || IsFiltered(shelf) || IsCyclic(shelf) || HasSlime || !_closedShelves.Add(shelf))
            throw new InvalidOperationException("Only an empty shelf on this board can be closed.");
    }

    public void OpenShelf(Shelf shelf) => _closedShelves.Remove(shelf);

    public bool IsKeyLocked(Shelf shelf) => _keyLockedShelves.Contains(shelf);

    public void CloseKeyLockedShelf(Shelf shelf)
    {
        if (shelf == null || !_shelves.Contains(shelf) || shelf.IsCleared || IsConveyor(shelf) || IsFiltered(shelf) || IsCyclic(shelf) || IsShelfClosed(shelf) || HasSlime)
            throw new InvalidOperationException("Only a filled regular open shelf on this board can be locked by a key.");

        _closedShelves.Add(shelf);
        _keyLockedShelves.Add(shelf);
    }

    public void OpenKeyLockedShelf(Shelf shelf)
    {
        if (!_keyLockedShelves.Remove(shelf))
            throw new InvalidOperationException("Only a key-locked shelf on this board can be opened by a key.");

        _closedShelves.Remove(shelf);
    }

    public bool IsConveyor(Shelf shelf) => _conveyorShelves.Contains(shelf);

    public void MarkConveyor(Shelf shelf)
    {
        if (shelf == null || !_shelves.Contains(shelf) || IsShelfClosed(shelf) || IsFiltered(shelf) || IsCyclic(shelf) || HasSlime || !_conveyorShelves.Add(shelf))
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
        if (shelf == null || !_shelves.Contains(shelf) || IsShelfClosed(shelf) || IsConveyor(shelf) || IsFiltered(shelf) || IsCyclic(shelf) || HasSlime)
            throw new InvalidOperationException("Only an open regular shelf on this board can be filtered once.");

        ItemType[] normalizedTypes = acceptedTypes?.Distinct().OrderBy(type => type).ToArray() ?? Array.Empty<ItemType>();

        if (normalizedTypes.Length == 0 || normalizedTypes.Length > ShelfStateSnapshot.MaximumAcceptedTypeCount)
            throw new InvalidOperationException($"A shelf filter must accept one to {ShelfStateSnapshot.MaximumAcceptedTypeCount} item types.");

        _shelfFilters.Add(shelf, Array.AsReadOnly(normalizedTypes));
        _filteredTypes = BoardStateSnapshot.CollectFilteredTypes(_shelfFilters.Values);
    }

    public bool IsCyclic(Shelf shelf) => shelf != null && _cyclePhases.ContainsKey(shelf);

    public ShelfCycleSnapshot GetCycle(Shelf shelf) => shelf != null && _shelfCycles.TryGetValue(shelf, out ShelfCycleSnapshot cycle) ? cycle : null;

    public bool IsCyclePhaseOpen(Shelf shelf)
    {
        return IsCyclic(shelf) ? _cyclePhases[shelf] : throw new InvalidOperationException("Only a cyclic shelf on this board has a cycle phase.");
    }

    public void MarkCyclic(Shelf shelf, ShelfCycleSnapshot cycle, bool isOpen)
    {
        if (cycle == null)
            throw new ArgumentNullException(nameof(cycle));

        if (shelf == null || !_shelves.Contains(shelf) || IsShelfClosed(shelf) || IsConveyor(shelf) || IsFiltered(shelf) || IsCyclic(shelf) || HasSlime)
            throw new InvalidOperationException("Only an open regular shelf on this board can be marked as cyclic once.");

        if (cycle.MovesToSwitch > cycle.GetDuration(isOpen))
            throw new InvalidOperationException("The first switch of a cyclic shelf cannot come later than its starting state lasts.");

        _shelfCycles.Add(shelf, cycle);
        _cyclePhases.Add(shelf, isOpen);

        if (!isOpen)
            _closedShelves.Add(shelf);
    }

    public void SetCyclicShelfOpen(Shelf shelf, bool isOpen)
    {
        if (!IsCyclic(shelf))
            throw new InvalidOperationException("Only a cyclic shelf on this board can be switched.");

        if (isOpen)
            _closedShelves.Remove(shelf);
        else
            _closedShelves.Add(shelf);
    }

    public IReadOnlyList<Shelf> AdvanceCycles()
    {
        List<Shelf> switchedShelves = new List<Shelf>();

        foreach (Shelf shelf in _shelfCycles.Keys.ToArray())
        {
            bool isOpen = _cyclePhases[shelf];
            _shelfCycles[shelf] = _shelfCycles[shelf].Advance(isOpen, out bool switched);

            if (!switched)
                continue;

            _cyclePhases[shelf] = !isOpen;
            switchedShelves.Add(shelf);
        }

        return switchedShelves;
    }

    public IReadOnlyList<Shelf> ApplyCycleRescue(bool isRepeated)
    {
        if (_cyclePhases.Any(phase => phase.Value == IsShelfClosed(phase.Key)))
            throw new InvalidOperationException("A cycle rescue requires every cyclic shelf to finish its switch.");

        BoardStateSnapshot rescuedBoard = CycleDeadlockRule.ApplyRescue(CreateSnapshot(), isRepeated, out IReadOnlyList<int> switchedShelfIndexes);

        foreach (Shelf shelf in _cyclePhases.Keys.ToArray())
        {
            ShelfStateSnapshot rescuedShelf = rescuedBoard.Shelves[GetShelfIndex(shelf)];
            _cyclePhases[shelf] = rescuedShelf.IsOpen;

            if (rescuedShelf.IsCyclic)
                _shelfCycles[shelf] = rescuedShelf.Cycle;
            else
                _shelfCycles.Remove(shelf);
        }

        return switchedShelfIndexes.Select(shelfIndex => _shelves[shelfIndex]).ToArray();
    }

    public bool IsSticky(ShelfColumn column)
    {
        Initialize();
        return HasSlime && column != null && _positions.TryGetValue(column, out ColumnPosition position) && _slime.IsSticky(position);
    }

    public void MarkSlime(SlimeSnapshot slime)
    {
        if (slime == null)
            throw new ArgumentNullException(nameof(slime));

        if (HasSlime || HasClosedShelves || HasConveyors || HasFilters || HasCycles)
            throw new InvalidOperationException("Slime can be added once to a board without other shelf mechanics.");

        CreateSnapshot().WithSlime(slime);
        _slime = slime;
    }

    public void SetSlime(SlimeSnapshot slime)
    {
        if (slime == null)
            throw new ArgumentNullException(nameof(slime));

        if (!HasSlime || !slime.Links.SequenceEqual(_slime.Links) || slime.GrowthPeriod != _slime.GrowthPeriod)
            throw new InvalidOperationException("Only the slime of this board can change its sticky columns and growth counter.");

        CreateSnapshot().WithSlime(slime);
        _slime = slime;
    }

    public IReadOnlyList<ColumnPosition> CleanSlime(IEnumerable<Shelf> matchedShelves)
    {
        if (matchedShelves == null)
            throw new ArgumentNullException(nameof(matchedShelves));

        if (!HasSlime)
            return Array.Empty<ColumnPosition>();

        BoardStateSnapshot cleanedBoard = SlimeRule.Clean(CreateSnapshot(), matchedShelves.Select(GetShelfIndex), out IReadOnlyList<ColumnPosition> cleanedColumns);
        _slime = cleanedBoard.Slime;
        return cleanedColumns;
    }

    public ColumnPosition? AdvanceSlimeGrowth(bool hasCleaned)
    {
        if (!HasSlime)
            return null;

        BoardStateSnapshot grownBoard = SlimeRule.AdvanceGrowth(CreateSnapshot(), hasCleaned, out ColumnPosition? grownColumn);
        _slime = grownBoard.Slime;
        return grownColumn;
    }

    public bool IsStuckInSlime() => HasSlime && SlimeRule.IsStuck(CreateSnapshot(), _simulator);

    public ColumnPosition ApplySlimeRescue()
    {
        BoardStateSnapshot rescuedBoard = SlimeRule.ApplyRescue(CreateSnapshot(), out ColumnPosition cleanedColumn);
        _slime = rescuedBoard.Slime;
        return cleanedColumn;
    }

    public ShelfColumn GetColumn(ColumnPosition position)
    {
        Initialize();

        if (position.ShelfIndex < 0 || position.ShelfIndex >= _shelves.Count || position.ColumnIndex < 0 || position.ColumnIndex >= _shelves[position.ShelfIndex].Capacity)
            throw new ArgumentOutOfRangeException(nameof(position));

        return _shelves[position.ShelfIndex].Columns[position.ColumnIndex];
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
        return column != null && column.FrontItem != null && _positions.TryGetValue(column, out ColumnPosition position)
            && IsShelfAvailable(_shelves[position.ShelfIndex])
            && !IsSticky(column)
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
        return new BoardStateSnapshot(_shelves.Select(CreateShelfSnapshot).ToArray(), _filteredTypes, _slime);
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

        return _simulator.TrySimulate(board.WithShelves(shelves), _positions[source], targetPosition, out simulation) && simulation.IsAllowed;
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

    private ShelfStateSnapshot CreateShelfSnapshot(Shelf shelf)
    {
        return shelf.CreateSnapshot(!IsShelfClosed(shelf), IsConveyor(shelf), GetAcceptedTypes(shelf), GetShelfIndex, GetCycle(shelf));
    }

    private int GetShelfIndex(Shelf shelf)
    {
        int shelfIndex = _shelves.IndexOf(shelf);
        return shelfIndex >= 0 ? shelfIndex : throw new InvalidOperationException("A key refers to a shelf from another board.");
    }

    private void OnDestroy() => ItemAnimations.Dispose();
}
