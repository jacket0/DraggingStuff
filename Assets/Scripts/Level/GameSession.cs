using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

public abstract class GameSession : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private MoveResolutionPlayer _moveResolutionPlayer;

    private bool _isResolvingMove;

    public LevelState State { get; private set; }
    public bool IsPlaying => State == LevelState.Playing;
    public bool CanInteract => IsPlaying && !_isResolvingMove;
    protected bool IsResolvingMove => _isResolvingMove;

    public event Action<MatchResolution> MatchSucceeded;
    public event Action<LevelState> StateChanged;

    protected ShelfBoard ShelfBoard => _shelfBoard;

    protected void StartSession()
    {
        Time.timeScale = 1;
        _isResolvingMove = false;
        SetState(LevelState.Playing);
    }

    public MoveOutcome TryMove(ShelfSlot source, ShelfSlot target)
    {
        if (!CanInteract)
            return MoveOutcome.Rejected();

        MoveOutcome moveOutcome = _shelfBoard.TryMove(source, target);

        if (!moveOutcome.IsSuccessful)
            return moveOutcome;

        _isResolvingMove = true;

        if (moveOutcome.HasMatch)
            RegisterMatch(moveOutcome.Match);

        PlayMoveResolution(moveOutcome);

        return moveOutcome;
    }

    public void Restart()
    {
        Time.timeScale = 1;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public bool TryPause()
    {
        if (State != LevelState.Playing)
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

    private void OnDestroy()
    {
        if (YG2.isGameplaying)
            YG2.GameplayStop();
    }

    protected abstract void HandleBoardSettled(bool isBoardCleared);
    protected virtual void PrepareBoardAfterMove(Action completed) => completed?.Invoke();
    protected virtual void SettleBoard(Action completed) => completed?.Invoke();
    protected virtual void HandleMatchRegistered(MatchResolution match) { }

    protected void CompleteAsWon() => SetState(LevelState.Won);
    protected void BeginEnding() => SetState(LevelState.Ending);
    protected void CompleteAsLost() => SetState(LevelState.Lost);

    private void PlayMoveResolution(MoveOutcome moveOutcome)
    {
        if (!moveOutcome.HasMatch)
        {
            PrepareAndAdvanceLayers();
            return;
        }

        _moveResolutionPlayer.Play(moveOutcome.Match, PrepareAndAdvanceLayers);
        _shelfBoard.HideActiveLayers(moveOutcome.EmptiedShelves);
    }

    private void PrepareAndAdvanceLayers()
    {
        PrepareBoardAfterMove(() =>
        {
            IReadOnlyList<Shelf> shelves = _shelfBoard.GetShelvesReadyToAdvance();
            _shelfBoard.AdvanceLayers(shelves, ResolveRevealedMatches);
        });
    }

    private void ResolveRevealedMatches()
    {
        if (State == LevelState.Ending)
        {
            CompleteResolution();
            return;
        }

        if (_shelfBoard.TryResolveActiveMatch(out Shelf matchedShelf, out MatchResolution match))
        {
            RegisterMatch(match);

            _moveResolutionPlayer.Play(match, PrepareAndAdvanceLayers);

            if (matchedShelf.CanRevealNextLayer)
                matchedShelf.HideActiveLayer();

            return;
        }

        SettleBoard(CompleteResolution);
    }

    private void RegisterMatch(MatchResolution match)
    {
        MatchSucceeded?.Invoke(match);
        HandleMatchRegistered(match);
    }

    private void CompleteResolution()
    {
        _isResolvingMove = false;
        HandleBoardSettled(_shelfBoard.IsCleared);
    }

    private void SetState(LevelState state)
    {
        if (State == state)
            return;

        State = state;

        if (State == LevelState.Playing)
            YG2.GameplayStart();
        else
            YG2.GameplayStop();

        StateChanged?.Invoke(State);
    }
}
