using System;

public sealed class LevelRunResult
{
    public int LevelNumber { get; }
    public bool Won { get; }
    public long Score { get; }
    public long ActiveTimeMilliseconds { get; }
    public long RemainingTimeMilliseconds { get; }
    public int RemainingItemCount { get; }
    public int CollectedItemCount { get; }
    public int TotalItemCount => CollectedItemCount + RemainingItemCount;
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
        int collectedItemCount,
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

        if (collectedItemCount < 0)
            throw new ArgumentOutOfRangeException(nameof(collectedItemCount));

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
        CollectedItemCount = collectedItemCount;
        Stars = stars;
        Seed = seed;
        VariantIndex = variantIndex;
    }
}
