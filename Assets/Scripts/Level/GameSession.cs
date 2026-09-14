using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

public abstract class GameSession : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private MoveResolutionPlayer _moveResolutionPlayer;

    private readonly Dictionary<Shelf, ShelfOperation> _operations = new Dictionary<Shelf, ShelfOperation>();
    private readonly List<ResolutionWave> _waves = new List<ResolutionWave>();
    private ShelfColumn _dragSource;
    private bool _needsSettlement;
    private bool _needsPreparation;
    private bool _isPreparing;
    private int _generation;

    public LevelState State { get; private set; }
    public bool IsPlaying => State == LevelState.Playing;
    public bool CanInteract => IsPlaying && !_isPreparing;
    public bool IsBoardSettled => !_needsSettlement && !_isPreparing && _operations.Count == 0 && _dragSource == null && !ItemAnimations.HasAnimations;
    public ShelfItemPlacementAnimator ItemAnimations => _shelfBoard.ItemAnimations;
    protected bool IsResolvingMove => !IsBoardSettled;
    protected ShelfBoard ShelfBoard => _shelfBoard;

    public event Action<MatchResolution> MatchSucceeded;
    public event Action<LevelState> StateChanged;

    protected void StartSession()
    {
        Time.timeScale = 1;
        _generation++;
        _dragSource = null;
        _needsSettlement = false;
        _needsPreparation = false;
        _isPreparing = false;
        SetState(LevelState.Playing);
    }

    public bool CanPickUp(ShelfColumnView view) => CanInteract && view != null && _shelfBoard.CanPickUp(view.Column);

    public bool TryBeginDrag(ShelfColumnView source)
    {
        if (_dragSource != null || !CanPickUp(source))
            return false;

        _dragSource = source.Column;
        return true;
    }

    public void EndDrag() => _dragSource = null;

    public MoveOutcome TryStartMove(ShelfColumnView source, ShelfColumnView target, out Action completePlacement)
    {
        completePlacement = null;

        if (!CanInteract || source == null || target == null || _dragSource != null && _dragSource != source.Column)
            return MoveOutcome.Rejected();

        MoveOutcome outcome = _shelfBoard.TryMove(source.Column, target.Column);

        if (!outcome.IsSuccessful)
            return outcome;

        ResolutionWave wave = CreateWave(outcome.AffectedShelves);
        wave.PendingInitialAnimations = 2;
        target.AttachFront(outcome.Item);
        _needsSettlement = true;
        _needsPreparation = true;
        int generation = _generation;
        bool placementCompleted = false;

        void CompleteInitialAnimation()
        {
            if (this == null || generation != _generation || !_waves.Contains(wave))
                return;

            wave.PendingInitialAnimations--;

            if (wave.PendingInitialAnimations == 0)
            {
                foreach (ShelfOperation operation in wave.Operations)
                    operation.IsReady = true;
            }
        }

        source.Shelf.View.Advance(CompleteInitialAnimation, outcome.Item);
        completePlacement = () =>
        {
            if (placementCompleted || this == null || generation != _generation || outcome.Item == null || outcome.Item.Column != target.Column)
                return;

            placementCompleted = true;
            CompleteInitialAnimation();
        };

        return outcome;
    }

    private void Update()
    {
        if (!_needsSettlement || _isPreparing || State == LevelState.Paused)
            return;

        foreach (ResolutionWave wave in _waves.ToArray())
        {
            if (wave.PendingInitialAnimations > 0 || wave.Operations.Any(operation => !operation.IsReady))
                continue;

            foreach (ShelfOperation operation in wave.Operations.ToArray())
            {
                Shelf shelf = operation.Shelf;

                if (shelf.TryResolveMatch(out MatchResolution match))
                {
                    PlayMatch(operation, match);
                }
                else
                {
                    _operations.Remove(shelf);
                    _shelfBoard.UnlockShelf(shelf);
                    wave.Operations.Remove(operation);
                }
            }

            if (wave.Operations.Count == 0)
                _waves.Remove(wave);
        }

        if (_operations.Count > 0 || _dragSource != null || ItemAnimations.HasAnimations)
            return;

        _isPreparing = true;
        int generation = _generation;

        if (_needsPreparation)
        {
            _needsPreparation = false;
            PrepareBoardAfterMove(() =>
            {
                if (this == null || generation != _generation)
                    return;

                _isPreparing = false;
                Shelf[] matchedShelves = _shelfBoard.Shelves.Where(shelf => shelf.HasMatch()).ToArray();

                if (matchedShelves.Length > 0)
                {
                    foreach (ShelfOperation operation in CreateWave(matchedShelves).Operations)
                        operation.IsReady = true;
                }
            });
            return;
        }

        SettleBoard(() =>
        {
            if (this == null || generation != _generation)
                return;

            _isPreparing = false;
            _needsSettlement = false;
            HandleBoardSettled(_shelfBoard.IsCleared);
        });
    }

    public void Restart()
    {
        CancelOperations();
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public bool TryPause()
    {
        if (!IsPlaying)
            return false;

        SetState(LevelState.Paused);
        Time.timeScale = 0;
        return true;
    }

    public void Resume()
    {
        if (State != LevelState.Paused)
            return;

        Time.timeScale = 1;
        SetState(LevelState.Playing);
    }

    protected abstract void HandleBoardSettled(bool isBoardCleared);
    protected virtual void PrepareBoardAfterMove(Action completed) => completed?.Invoke();
    protected virtual void SettleBoard(Action completed) => completed?.Invoke();
    protected virtual void HandleMatchRegistered(MatchResolution match) { }
    protected void CompleteAsWon() => SetState(LevelState.Won);
    protected void BeginEnding()
    {
        _needsSettlement = true;
        SetState(LevelState.Ending);
    }
    protected void CompleteAsLost() => SetState(LevelState.Lost);

    private ResolutionWave CreateWave(IReadOnlyList<Shelf> shelves)
    {
        ResolutionWave wave = new ResolutionWave();

        foreach (Shelf shelf in shelves)
        {
            ShelfOperation operation = new ShelfOperation(shelf);
            _shelfBoard.LockShelf(shelf);
            _operations.Add(shelf, operation);
            wave.Operations.Add(operation);
        }

        _waves.Add(wave);
        return wave;
    }

    private bool IsCurrent(ShelfOperation operation)
    {
        return this != null && _operations.TryGetValue(operation.Shelf, out ShelfOperation current) && current == operation;
    }

    private void PlayMatch(ShelfOperation operation, MatchResolution match)
    {
        operation.IsReady = false;
        _needsPreparation = true;
        MatchSucceeded?.Invoke(match);
        HandleMatchRegistered(match);
        _moveResolutionPlayer.Play(match, () =>
        {
            if (!IsCurrent(operation))
                return;

            operation.Shelf.View.Advance(() =>
            {
                if (IsCurrent(operation))
                    operation.IsReady = true;
            });
        });
    }

    private void CancelOperations()
    {
        _generation++;
        _waves.Clear();

        foreach (Shelf shelf in _operations.Keys)
        {
            shelf.View.Cancel();
            _shelfBoard.UnlockShelf(shelf);
        }

        _operations.Clear();
        _moveResolutionPlayer.CancelAll();
        ItemAnimations.Dispose();
    }

    private void OnDestroy()
    {
        CancelOperations();

        if (YG2.isGameplaying)
            YG2.GameplayStop();
    }

    private void SetState(LevelState state)
    {
        if (State == state)
            return;

        State = state;

        if (state == LevelState.Playing)
            YG2.GameplayStart();
        else
            YG2.GameplayStop();

        StateChanged?.Invoke(state);
    }

    private sealed class ShelfOperation
    {
        public Shelf Shelf { get; }
        public bool IsReady { get; set; }
        public ShelfOperation(Shelf shelf) => Shelf = shelf;
    }

    private sealed class ResolutionWave
    {
        public List<ShelfOperation> Operations { get; } = new List<ShelfOperation>();
        public int PendingInitialAnimations { get; set; }
    }
}
