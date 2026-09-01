using System;
using UnityEngine;

public class LevelSession : GameSession
{
    [SerializeField] private LevelBuilder _levelBuilder;
    [SerializeField] private LevelSelectionState _levelSelection;
    [SerializeField] private LevelEntry _fallbackLevel;

    public LevelEntry CurrentLevel { get; private set; }

    public event Action LevelCompleted;

    private void Start()
    {
        CurrentLevel = ResolveCurrentLevel();

        _levelBuilder.Build(CurrentLevel.Definition);
        ShelfBoard.InitializeViews();
        StartSession();
    }

    protected override void HandleBoardSettled(bool isBoardCleared)
    {
        if (!isBoardCleared)
            return;

        if (State != LevelState.Playing)
            return;

        CompleteAsWon();
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
