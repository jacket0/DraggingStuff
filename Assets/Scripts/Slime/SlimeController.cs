using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SlimeController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private ShelfItemDragController _dragController;
    [SerializeField] private SlimeBoardView _view;

    private int _growthCount;

    public bool IsGrowing => _growthCount > 0;

    public event Action<IReadOnlyList<ColumnPosition>> SlimeCleaned;
    public event Action<ColumnPosition> SlimeGrown;
    public event Action<ColumnPosition> RescueApplied;
    public event Action<ShelfItem> PickUpRejected;

    private void Awake()
    {
        if (_session == null)
            throw new InvalidOperationException(nameof(_session));

        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_dragController == null)
            throw new InvalidOperationException(nameof(_dragController));

        if (_view == null)
            throw new InvalidOperationException(nameof(_view));
    }

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _session.MatchingMoveResolved += HandleMatchingMoveResolved;
        _session.CascadeMatchesResolved += HandleCascadeMatchesResolved;
        _session.BoardSettled += HandleBoardSettled;
        _session.StateChanged += HandleStateChanged;
        _dragController.PickUpRejected += HandlePickUpRejected;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _session.MatchingMoveResolved -= HandleMatchingMoveResolved;
        _session.CascadeMatchesResolved -= HandleCascadeMatchesResolved;
        _session.BoardSettled -= HandleBoardSettled;
        _session.StateChanged -= HandleStateChanged;
        _dragController.PickUpRejected -= HandlePickUpRejected;
    }

    private void HandleSessionStarted()
    {
        _growthCount = 0;

        if (!_shelfBoard.HasSlime)
        {
            _view.HideForLevel();
            return;
        }

        _view.ShowLevel(_shelfBoard.Slime.StickyColumns);
        RefreshForecast();
    }

    private void HandleMatchingMoveResolved(IReadOnlyList<Shelf> matchedShelves)
    {
        if (!_shelfBoard.HasSlime)
            return;

        bool hasCleaned = Clean(matchedShelves);
        ColumnPosition? grownColumn = _shelfBoard.AdvanceSlimeGrowth(hasCleaned);

        if (grownColumn.HasValue)
            RequestGrowth(grownColumn.Value);

        RefreshForecast();
    }

    private void HandleCascadeMatchesResolved(IReadOnlyList<Shelf> matchedShelves)
    {
        if (!_shelfBoard.HasSlime)
            return;

        Clean(matchedShelves);
        RefreshForecast();
    }

    private void HandleBoardSettled()
    {
        while (_session.State == LevelState.Playing && _shelfBoard.IsStuckInSlime())
        {
            ColumnPosition cleanedColumn = _shelfBoard.ApplySlimeRescue();
            _view.PlayCleaned(new[] { cleanedColumn });
            RescueApplied?.Invoke(cleanedColumn);
        }

        if (_shelfBoard.HasSlime)
            RefreshForecast();
    }

    private void HandleStateChanged(LevelState state)
    {
        if (state == LevelState.Won)
            _view.PlayDried();
        else if (state == LevelState.Ending || state == LevelState.Lost)
            _view.HideForecast();
    }

    private void HandlePickUpRejected(ShelfItem item)
    {
        if (!_shelfBoard.IsSticky(item.Column))
            return;

        _view.PlayPickUpRejected(item);
        PickUpRejected?.Invoke(item);
    }

    private bool Clean(IReadOnlyList<Shelf> matchedShelves)
    {
        IReadOnlyList<ColumnPosition> cleanedColumns = _shelfBoard.CleanSlime(matchedShelves);

        if (cleanedColumns.Count == 0)
            return false;

        _view.PlayCleaned(cleanedColumns);
        SlimeCleaned?.Invoke(cleanedColumns);
        return true;
    }

    private void RequestGrowth(ColumnPosition column)
    {
        Shelf shelf = _shelfBoard.Shelves[column.ShelfIndex];
        _growthCount++;
        _session.RequestBoardChange(new[] { shelf }, completed => PlayGrowth(column, completed));
    }

    private void PlayGrowth(ColumnPosition column, Action completed)
    {
        if (!_shelfBoard.Slime.IsSticky(column))
        {
            _growthCount--;
            completed();
            return;
        }

        _view.PlayGrowth(column, () =>
        {
            _growthCount--;
            SlimeGrown?.Invoke(column);
            completed();
        });
    }

    private void RefreshForecast()
    {
        if (!_shelfBoard.Slime.HasStickyColumns || _session.State != LevelState.Playing)
        {
            _view.HideForecast();
            return;
        }

        ColumnPosition? target = SlimeRule.FindGrowthTarget(_shelfBoard.CreateSnapshot());

        if (target.HasValue)
            _view.ShowForecast(target.Value, _shelfBoard.Slime.MovesToGrowth);
        else
            _view.HideForecast();
    }
}
