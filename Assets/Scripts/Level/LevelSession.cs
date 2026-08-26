using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSession : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private LevelBuilder _levelBuilder;
    [SerializeField] private MoveResolutionPlayer _moveResolutionPlayer;
    [SerializeField] private LevelSelectionState _levelSelection;
    [SerializeField] private LevelEntry _fallbackLevel;

    private bool _isResolvingMove;

    public LevelState State { get; private set; }
    public bool IsPlaying => State == LevelState.Playing;
    public bool CanInteract => IsPlaying && !_isResolvingMove;
    public LevelEntry CurrentLevel { get; private set; }

    public event Action LevelCompleted;
    public event Action<MatchResolution> MatchSucceeded;

    private void Start()
    {
        CurrentLevel = ResolveCurrentLevel();

        _levelBuilder.Build(CurrentLevel.Definition);
        _shelfBoard.InitializeViews();
        StartLevel();
    }

    public void StartLevel()
    {
        Time.timeScale = 1;
        _isResolvingMove = false;
        State = LevelState.Playing;
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
            MatchSucceeded?.Invoke(moveOutcome.Match);

        PlayMoveResolution(moveOutcome);

        return moveOutcome;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public bool TryPauseLevel()
    {
        if (State != LevelState.Playing)
            return false;

        State = LevelState.Paused;
        Time.timeScale = 0;
        return true;
    }

    public void ResumeLevel()
    {
        if (State != LevelState.Paused)
            return;

        Time.timeScale = 1;
        State = LevelState.Playing;
    }

    private void PlayMoveResolution(MoveOutcome moveOutcome)
    {
        if (!moveOutcome.HasMatch)
        {
            AdvanceLayers(moveOutcome.ShelvesToAdvance);
            return;
        }

        _moveResolutionPlayer.Play(moveOutcome.Match, () => AdvanceLayers(moveOutcome.ShelvesToAdvance));
        _shelfBoard.HideActiveLayers(moveOutcome.ShelvesToAdvance);
    }

    private void AdvanceLayers(IReadOnlyList<Shelf> shelves)
    {
        _shelfBoard.AdvanceLayers(shelves, ResolveRevealedMatches);
    }

    private void ResolveRevealedMatches()
    {
        if (_shelfBoard.TryResolveActiveMatch(out Shelf matchedShelf, out MatchResolution match))
        {
            MatchSucceeded?.Invoke(match);

            IReadOnlyList<Shelf> shelvesToAdvance = matchedShelf.CanRevealNextLayer
                ? new[] { matchedShelf }
                : Array.Empty<Shelf>();

            _moveResolutionPlayer.Play(match, () => AdvanceLayers(shelvesToAdvance));
            _shelfBoard.HideActiveLayers(shelvesToAdvance);
            return;
        }

        _isResolvingMove = false;
        HandleMoveResolutionCompleted(_shelfBoard.IsCleared);
    }

    private void HandleMoveResolutionCompleted(bool isLevelCompleted)
    {
        if (!isLevelCompleted)
            return;

        if (State != LevelState.Playing)
            return;

        State = LevelState.Won;
        LevelCompleted?.Invoke();
    }

    private LevelEntry ResolveCurrentLevel()
    {
        LevelEntry currentLevel;

        if (_levelSelection != null && _levelSelection.TryGetSelected(out LevelEntry selectionEntry))
            currentLevel = selectionEntry;
        else
            currentLevel = _fallbackLevel;

        if (currentLevel == null)
            throw new InvalidOperationException(nameof(currentLevel));

        if (currentLevel.Definition == null)
            throw new InvalidCastException(nameof(currentLevel.Definition));

        return currentLevel;
    }
}
