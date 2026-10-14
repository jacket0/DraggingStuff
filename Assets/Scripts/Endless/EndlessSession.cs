using System;
using UnityEngine;

public sealed class EndlessSession : GameSession
{
    [SerializeField] private EndlessBoardRefiller _boardRefiller;
    [SerializeField] private EndlessTimer _timer;
    [SerializeField, Min(1f)] private float _continueTime = 15f;

    private bool _hasContinued;
    private bool _isContinueOffered;

    public int Seed { get; private set; }
    public int MatchCount { get; private set; }
    public bool HasContinued => _hasContinued;
    public bool IsContinueOffered => _isContinueOffered;

    public event Action ContinueOffered;
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
        if (State != LevelState.Playing && State != LevelState.Ending)
            return;

        MatchCount++;

        if (State == LevelState.Playing)
            _timer.RegisterMatch(MatchCount, match.Items.Count);
    }

    protected override void PrepareBoardAfterMove(Action completed)
    {
        if (State == LevelState.Playing)
            _boardRefiller.RefillIfNeeded(MatchCount);

        completed?.Invoke();
    }

    protected override void HandleBoardSettled(bool isBoardCleared)
    {
        if (State == LevelState.Ending)
            OfferContinueOrFinish();
    }

    private void HandleTimerExpired()
    {
        if (State != LevelState.Playing)
            return;

        BeginEnding();

        if (!IsResolvingMove)
            OfferContinueOrFinish();
    }

    public void Continue()
    {
        if (!_isContinueOffered)
            return;

        _isContinueOffered = false;
        _hasContinued = true;
        _boardRefiller.RefillIfNeeded(MatchCount);
        _timer.Continue(_continueTime);
        ContinuePlaying();
    }

    public void DeclineContinue()
    {
        if (!_isContinueOffered)
            return;

        _isContinueOffered = false;
        FinishRun();
    }

    private void OfferContinueOrFinish()
    {
        if (_isContinueOffered)
            return;

        if (_hasContinued)
        {
            FinishRun();
            return;
        }

        _isContinueOffered = true;
        ContinueOffered?.Invoke();
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
