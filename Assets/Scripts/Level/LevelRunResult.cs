using System;

public sealed class LevelRunResult
{
    public int LevelNumber { get; }
    public bool Won { get; }
    public long Score { get; }
    public long ActiveTimeMilliseconds { get; }
    public long RemainingTimeMilliseconds { get; }
    public int RemainingItemCount { get; }
    public int Stars { get; }
    public int Seed { get; }
    public int VariantIndex { get; }

    public LevelRunResult(
        int levelNumber,
        bool won,
        long score,
        long activeTimeMilliseconds,
        long remainingTimeMilliseconds,
        int remainingItemCount,
        int stars,
        int seed,
        int variantIndex)
    {
        if (levelNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelNumber));

        if (score < 0)
            throw new ArgumentOutOfRangeException(nameof(score));

        if (activeTimeMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(activeTimeMilliseconds));

        if (remainingTimeMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(remainingTimeMilliseconds));

        if (remainingItemCount < 0)
            throw new ArgumentOutOfRangeException(nameof(remainingItemCount));

        if (stars < 0 || stars > 3 || !won && stars != 0)
            throw new ArgumentOutOfRangeException(nameof(stars));

        if (variantIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(variantIndex));

        LevelNumber = levelNumber;
        Won = won;
        Score = score;
        ActiveTimeMilliseconds = activeTimeMilliseconds;
        RemainingTimeMilliseconds = remainingTimeMilliseconds;
        RemainingItemCount = remainingItemCount;
        Stars = stars;
        Seed = seed;
        VariantIndex = variantIndex;
    }
}
