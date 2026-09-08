using UnityEngine;

public class TotalScoreService : LeaderboardScoreSource
{
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private LevelProgressService _progress;
    [SerializeField] private EndlessProgressService _endlessProgress;

    public override long Value
    {
        get
        {
            long totalBestScore = 0;

            foreach (LevelEntry level in _catalog.Levels)
                totalBestScore = checked(totalBestScore + _progress.GetBestScore(level));

            totalBestScore = checked(totalBestScore + _endlessProgress.BestScore);
            return totalBestScore;
        }
    }

    private void OnEnable()
    {
        _progress.ProgressChanged += NotifyValueChanged;
        _endlessProgress.BestScoreChanged += HandleEndlessBestScoreChanged;
    }

    private void OnDisable()
    {
        if (_progress != null)
            _progress.ProgressChanged -= NotifyValueChanged;

        if (_endlessProgress != null)
            _endlessProgress.BestScoreChanged -= HandleEndlessBestScoreChanged;
    }

    private void HandleEndlessBestScoreChanged(long bestScore)
    {
        NotifyValueChanged();
    }
}
