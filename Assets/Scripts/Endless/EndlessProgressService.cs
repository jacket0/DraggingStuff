using System;

public sealed class EndlessProgressService : LeaderboardScoreSource
{
    private long _knownBestScore;

    public long BestScore => GameProgressRepository.EndlessBestScore;
    public override long Value => BestScore;

    public event Action<long> BestScoreChanged;

    private void Awake()
    {
        _knownBestScore = BestScore;
    }

    private void OnEnable()
    {
        GameProgressRepository.ProgressChanged += HandleProgressChanged;
    }

    private void OnDisable()
    {
        GameProgressRepository.ProgressChanged -= HandleProgressChanged;
    }

    public bool RegisterResult(long score)
    {
        if (score < 0)
            throw new ArgumentOutOfRangeException(nameof(score));

        return GameProgressRepository.RegisterEndlessResult(score);
    }

    private void HandleProgressChanged()
    {
        long bestScore = BestScore;

        if (bestScore == _knownBestScore)
            return;

        _knownBestScore = bestScore;
        BestScoreChanged?.Invoke(bestScore);
        NotifyValueChanged();
    }
}
