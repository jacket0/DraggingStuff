using System;
using UnityEngine;

public class ScoreSystem : MonoBehaviour
{
    [SerializeField, Min(1)] private int _baseMatchScore = 100;

    public long CurrentScore { get; private set; }

    public event Action<long> ScoreChanged;
    public event Action<long> ScoreIncreased;

    public long RegisterMatch(MatchResolution match, int comboMultiplier)
    {
        if (match == null)
            throw new ArgumentNullException(nameof(match));

        if (comboMultiplier < 1)
            throw new ArgumentOutOfRangeException(nameof(comboMultiplier));

        long earnedScore = (long)_baseMatchScore * comboMultiplier;

        CurrentScore += earnedScore;

        ScoreChanged?.Invoke(CurrentScore);
        ScoreIncreased?.Invoke(earnedScore);

        return earnedScore;
    }

    public void ResetState()
    {
        CurrentScore = 0;
        ScoreChanged?.Invoke(CurrentScore);
    }
}
