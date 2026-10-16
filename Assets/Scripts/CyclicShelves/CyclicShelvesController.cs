using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CyclicShelvesController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private List<ShelfShutterView> _shutters = new List<ShelfShutterView>();
    [SerializeField] private DepartureBoardView _departureBoard;
    [SerializeField] private CyclicShelfAudioPlayer _audio;

    private bool _wasRescued;
    private bool _hasMatchingMoveSinceRescue;
    private int _switchCount;

    public bool IsSwitching => _switchCount > 0;

    public event Action CyclesAdvanced;
    public event Action<IReadOnlyList<Shelf>> ShelvesSwitched;
    public event Action<bool> RescueApplied;

    private void Awake()
    {
        if (_session == null)
            throw new InvalidOperationException(nameof(_session));

        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_dragController == null)
            throw new InvalidOperationException(nameof(_dragController));

        if (_departureBoard == null)
            throw new InvalidOperationException(nameof(_departureBoard));

        if (_audio == null)
            throw new InvalidOperationException(nameof(_audio));

        if (_shutters.Contains(null) || _shutters.Select(shutter => shutter.Shelf).Distinct().Count() != _shutters.Count)
            throw new InvalidOperationException($"{name}: every shutter must belong to its own shelf.");
    }

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _session.MatchingMoveResolved += HandleMatchingMoveResolved;
        _dragController.HoveredShelfChanged += HandleHoveredShelfChanged;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _session.MatchingMoveResolved -= HandleMatchingMoveResolved;
        _dragController.HoveredShelfChanged -= HandleHoveredShelfChanged;
    }

    public void RescueIfStuck()
    {
        if (!CycleDeadlockRule.IsStuck(_shelfBoard.CreateSnapshot()))
            return;

        bool isRepeated = _wasRescued && !_hasMatchingMoveSinceRescue;
        IReadOnlyList<Shelf> switchedShelves = _shelfBoard.ApplyCycleRescue(isRepeated);
        _wasRescued = true;
        _hasMatchingMoveSinceRescue = false;
        ShowCycleProgress(switchedShelves);
        RescueApplied?.Invoke(isRepeated);
        RequestSwitch(switchedShelves);
    }

    private void HandleSessionStarted()
    {
        _wasRescued = false;
        _hasMatchingMoveSinceRescue = false;

        foreach (Shelf shelf in _shelfBoard.Shelves.Where(_shelfBoard.IsCyclic))
        {
            if (_shutters.All(shutter => shutter.Shelf != shelf) || !_departureBoard.HasSlot(shelf))
                throw new InvalidOperationException($"{shelf.name}: the cyclic shelf needs a shutter and a departure board slot.");
        }

        foreach (ShelfShutterView shutter in _shutters)
        {
            if (!_shelfBoard.IsCyclic(shutter.Shelf))
                shutter.HideForLevel();
            else if (_shelfBoard.IsShelfClosed(shutter.Shelf))
                shutter.ShowClosed();
            else
                shutter.ShowOpen();
        }

        _departureBoard.ShowLevel(_shelfBoard);
        RefreshShutterWarnings(Array.Empty<Shelf>());
    }

    private void HandleMatchingMoveResolved()
    {
        if (!_shelfBoard.HasCycles)
            return;

        _hasMatchingMoveSinceRescue = true;
        IReadOnlyList<Shelf> switchedShelves = _shelfBoard.AdvanceCycles();
        ShowCycleProgress(switchedShelves);
        CyclesAdvanced?.Invoke();
        RequestSwitch(switchedShelves);
    }

    private void HandleHoveredShelfChanged(Shelf shelf)
    {
        if (shelf != null && _shelfBoard.IsCyclic(shelf) && _shelfBoard.IsShelfClosed(shelf))
            GetShutter(shelf).PlayShudder();
    }

    private void ShowCycleProgress(IReadOnlyList<Shelf> switchedShelves)
    {
        if (_departureBoard.Refresh(_shelfBoard))
            _audio.PlayFlip();

        if (RefreshShutterWarnings(switchedShelves))
            _audio.PlayWarning();
    }

    private bool RefreshShutterWarnings(IReadOnlyList<Shelf> switchingShelves)
    {
        bool hasWarning = false;

        foreach (ShelfShutterView shutter in _shutters.Where(shutter => _shelfBoard.IsCyclic(shutter.Shelf) && !switchingShelves.Contains(shutter.Shelf)))
        {
            ShelfCycleSnapshot cycle = _shelfBoard.GetCycle(shutter.Shelf);
            bool isWarning = cycle != null && cycle.MovesToSwitch == 1;
            shutter.SetWarning(isWarning);
            hasWarning |= isWarning;
        }

        return hasWarning;
    }

    private void RequestSwitch(IReadOnlyList<Shelf> shelves)
    {
        if (shelves.Count > 0)
            _session.RequestBoardChange(shelves, completed => PlaySwitch(shelves, completed));
    }

    private void PlaySwitch(IReadOnlyList<Shelf> shelves, Action completed)
    {
        Shelf[] changingShelves = shelves.Where(shelf => _shelfBoard.IsCyclePhaseOpen(shelf) == _shelfBoard.IsShelfClosed(shelf)).ToArray();

        if (changingShelves.Length == 0)
        {
            completed();
            return;
        }

        _switchCount++;
        int pendingShutterCount = changingShelves.Length;

        void CompleteShutter()
        {
            pendingShutterCount--;

            if (pendingShutterCount > 0)
                return;

            _switchCount--;
            ShelvesSwitched?.Invoke(changingShelves);
            completed();
        }

        foreach (Shelf shelf in changingShelves)
        {
            ShelfShutterView shutter = GetShutter(shelf);

            if (_shelfBoard.IsCyclePhaseOpen(shelf))
            {
                _audio.PlayShutterUp();
                shutter.PlayOpen(() =>
                {
                    _shelfBoard.SetCyclicShelfOpen(shelf, true);
                    CompleteShutter();
                });
            }
            else
            {
                _shelfBoard.SetCyclicShelfOpen(shelf, false);
                _audio.PlayShutterDown();
                shutter.PlayClose(CompleteShutter);
            }
        }
    }

    private ShelfShutterView GetShutter(Shelf shelf) => _shutters.First(shutter => shutter.Shelf == shelf);
}
