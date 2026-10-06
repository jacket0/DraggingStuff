using System;

public static class LevelStarCalculator
{
    public const int MaximumStars = 3;

    public static int Calculate(bool won, long activeTimeMilliseconds, TimedLevelDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (activeTimeMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(activeTimeMilliseconds));

        if (!won)
            return 0;

        if (activeTimeMilliseconds <= definition.ThreeStarTimeSeconds * 1000L)
            return MaximumStars;

        if (activeTimeMilliseconds <= definition.TwoStarTimeSeconds * 1000L)
            return 2;

        return 1;
    }
}
