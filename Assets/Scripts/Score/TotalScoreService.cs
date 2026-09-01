using UnityEngine;

public class TotalScoreService : LeaderboardScoreSource
{
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private LevelProgressService _progress;

    public override long Value
    {
        get
        {
            long totalBestScore = 0;

            foreach (LevelEntry level in _catalog.Levels)
                totalBestScore = checked(totalBestScore + _progress.GetBestScore(level));

            return totalBestScore;
        }
    }

    private void OnEnable()
    {
        _progress.ProgressChanged += NotifyValueChanged;
    }

    private void OnDisable()
    {
        if (_progress != null)
            _progress.ProgressChanged -= NotifyValueChanged;
    }
}
