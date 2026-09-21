using System;
using UnityEngine;

public class LevelSession : GameSession
{
    [SerializeField] private TimedLevelBuilder _levelBuilder;
    [SerializeField] private LevelSelectionState _levelSelection;
    [SerializeField] private LevelEntry _fallbackLevel;
    [SerializeField] private CountdownTimer _timer;
    [SerializeField] private ScoreSystem _scoreSystem;

    private TimedLevelBuildResult _buildResult;
    private LevelRunResult _result;

    public LevelEntry CurrentLevel { get; private set; }
    public CountdownTimer Timer => _timer;
    public LevelRunResult Result => _result;
    public int CurrentSeed => _buildResult != null ? _buildResult.Seed : 0;
    public int CurrentVariantIndex => _buildResult != null ? _buildResult.VariantIndex : -1;

    public event Action<LevelRunResult> RunEnded;
    public event Action SessionStarted;

    private void OnEnable()
    {
        if (_timer != null)
            _timer.Expired += HandleTimerExpired;
    }

    private void OnDisable()
    {
        if (_timer != null)
            _timer.Expired -= HandleTimerExpired;
    }

    private void Start()
    {
        ValidateDependencies();
        CurrentLevel = ResolveCurrentLevel();
        _buildResult = _levelBuilder.Build(CurrentLevel.Number, CurrentLevel.Definition);
        _timer.Configure(CurrentLevel.Definition.TimeLimitSeconds);
        StartSession();
        SessionStarted?.Invoke();
    }

    protected override void HandleBoardSettled(bool isBoardCleared)
    {
        if (State == LevelState.Ending)
        {
            FinishRun(isBoardCleared);
            return;
        }

        if (State == LevelState.Playing && isBoardCleared)
            FinishRun(true);
    }

    protected override void HandleMoveAccepted(MoveOutcome move)
    {
        if (State == LevelState.Playing)
            _timer.StartTimer();
    }

    protected override void HandlePaused()
    {
        _timer.Pause();
    }

    protected override void HandleResumed()
    {
        _timer.Resume();
    }

    private void HandleTimerExpired()
    {
        if (State != LevelState.Playing)
            return;

        BeginEnding();

        if (!IsResolvingMove)
            FinishRun(ShelfBoard.IsCleared);
    }

    private void FinishRun(bool won)
    {
        if (_result != null)
            return;

        _timer.Stop();
        long activeTimeMilliseconds = ToMilliseconds(_timer.ActiveTime);
        long remainingTimeMilliseconds = ToMilliseconds(_timer.RemainingTime);
        int remainingItemCount = ShelfBoard.CreateSnapshot().ItemCount;
        int stars = LevelStarCalculator.Calculate(won, activeTimeMilliseconds, CurrentLevel.Definition);

        _result = new LevelRunResult(
            CurrentLevel.Number,
            won,
            _scoreSystem.CurrentScore,
            activeTimeMilliseconds,
            remainingTimeMilliseconds,
            remainingItemCount,
            stars,
            _buildResult.Seed,
            _buildResult.VariantIndex);

        if (won)
            CompleteAsWon();
        else
            CompleteAsLost();

        RunEnded?.Invoke(_result);
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

    private void ValidateDependencies()
    {
        if (_levelBuilder == null)
            throw new InvalidOperationException(nameof(_levelBuilder));

        if (_timer == null)
            throw new InvalidOperationException(nameof(_timer));

        if (_scoreSystem == null)
            throw new InvalidOperationException(nameof(_scoreSystem));
    }

    private static long ToMilliseconds(float seconds)
    {
        return (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero);
    }
}
