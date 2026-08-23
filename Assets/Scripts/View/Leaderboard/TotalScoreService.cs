using UnityEngine;

public class TotalScoreService : MonoBehaviour
{
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private LevelProgressService _progress;

    public long Value
    {
        get
        {
            long totalBestScore = 0;

            foreach (LevelEntry level in _catalog.Levels)
                totalBestScore = checked(totalBestScore + _progress.GetBestScore(level));

            return totalBestScore;
        }
    }
}
