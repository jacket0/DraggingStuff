using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ConveyorController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private List<ConveyorBeltView> _beltViews = new List<ConveyorBeltView>();
    [SerializeField, Min(0.01f)] private float _shiftDuration = 0.45f;

    public bool IsShifting { get; private set; }

    public event Action ShiftStarted;
    public event Action ShiftCompleted;

    private void Awake()
    {
        if (_session == null)
            throw new InvalidOperationException(nameof(_session));

        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_beltViews.Contains(null) || _beltViews.Select(view => view.Shelf).Distinct().Count() != _beltViews.Count)
            throw new InvalidOperationException($"{name}: every belt view must drive its own shelf.");
    }

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _session.MatchingMoveResolved += RequestShift;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _session.MatchingMoveResolved -= RequestShift;
    }

    public void RequestShift()
    {
        if (_shelfBoard.HasConveyors)
            _session.RequestBoardChange(_shelfBoard.ConveyorShelves, PlayShift);
    }

    private void HandleSessionStarted()
    {
        foreach (Shelf shelf in _shelfBoard.ConveyorShelves)
        {
            if (_beltViews.All(view => view.Shelf != shelf))
                throw new InvalidOperationException($"{shelf.name}: the conveyor shelf has no belt view.");
        }

        foreach (ConveyorBeltView view in _beltViews)
            view.SetRunning(_shelfBoard.IsConveyor(view.Shelf));
    }

    private void PlayShift(Action completed)
    {
        List<ShelfShift> shifts = new List<ShelfShift>();

        foreach (Shelf shelf in _shelfBoard.ConveyorShelves)
        {
            int[] movingLanes = Enumerable.Range(0, shelf.Capacity).Where(lane => shelf.Columns[lane].Count >= 2).ToArray();

            if (movingLanes.Length > 0 && shelf.ShiftConveyor())
                shifts.Add(new ShelfShift(shelf, movingLanes));
        }

        if (shifts.Count == 0)
        {
            completed();
            return;
        }

        IsShifting = true;
        ShiftStarted?.Invoke();
        int pendingAnimationCount = shifts.Count * 2;

        void CompleteAnimation()
        {
            pendingAnimationCount--;

            if (pendingAnimationCount > 0)
                return;

            IsShifting = false;
            ShiftCompleted?.Invoke();
            completed();
        }

        foreach (ShelfShift shift in shifts)
        {
            shift.Shelf.View.PlayConveyorShift(_shiftDuration, CompleteAnimation);
            _beltViews.First(view => view.Shelf == shift.Shelf).PlayShift(shift.MovingLanes, _shiftDuration, CompleteAnimation);
        }
    }

    private readonly struct ShelfShift
    {
        public Shelf Shelf { get; }
        public IReadOnlyList<int> MovingLanes { get; }

        public ShelfShift(Shelf shelf, IReadOnlyList<int> movingLanes)
        {
            Shelf = shelf;
            MovingLanes = movingLanes;
        }
    }
}
