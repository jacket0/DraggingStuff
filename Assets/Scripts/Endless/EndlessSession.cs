using System;
using UnityEngine;

public sealed class EndlessSession : GameSession
{
    [SerializeField] private EndlessBoardRefiller _boardRefiller;
    [SerializeField] private EndlessTimer _timer;

    public int Seed { get; private set; }
    public int MatchCount { get; private set; }

    public event Action RunEnded;

    private void OnEnable()
    {
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

        Seed = Guid.NewGuid().GetHashCode();
        MatchCount = 0;

        _boardRefiller.Initialize(Seed);
        StartSession();
        _timer.StartTimer();

#if DEVELOPMENT_BUILD
        Debug.Log($"Endless seed: {Seed}");
#endif
    }

    protected override void HandleMatchRegistered(MatchResolution match)
    {
        if (State != LevelState.Playing)
            return;

        MatchCount++;
        _timer.RegisterMatch(MatchCount);
        _boardRefiller.PrepareRefill(MatchCount);
    }

    protected override void PrepareBoardAfterMove(Action completed)
    {
        if (State == LevelState.Playing)
            _boardRefiller.ApplyPreparedRefill(MatchCount);

        completed?.Invoke();
    }

    protected override void HandleBoardSettled(bool isBoardCleared)
    {
        if (State == LevelState.Ending)
            FinishRun();
    }

    private void HandleTimerExpired()
    {
        if (State != LevelState.Playing)
            return;

        BeginEnding();

        if (!IsResolvingMove)
            FinishRun();
    }

    private void FinishRun()
    {
        if (State == LevelState.Lost)
            return;

        _timer.StopTimer();
        CompleteAsLost();
        RunEnded?.Invoke();
    }

    private void ValidateDependencies()
    {
        if (_boardRefiller == null)
            throw new InvalidOperationException(nameof(_boardRefiller));

        if (_timer == null)
            throw new InvalidOperationException(nameof(_timer));
    }
}
