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
        string sceneName = gameObject.scene.name;
        LevelEntry currentLevel = _fallbackLevel;

        if (_levelSelection != null
            && _levelSelection.TryGetSelected(out LevelEntry selectionEntry)
            && selectionEntry.SceneName == sceneName)
            currentLevel = selectionEntry;

        if (currentLevel == null)
            throw new InvalidOperationException($"Для сцены {sceneName} не задан запасной уровень.");

        if (currentLevel.SceneName != sceneName)
            throw new InvalidOperationException($"Уровень {currentLevel.Number} настроен для сцены {currentLevel.SceneName}, а загружена {sceneName}.");

        if (currentLevel.Definition == null)
            throw new InvalidOperationException($"Для уровня {currentLevel.Number} не задана расстановка предметов.");

        return currentLevel;
    }
}
