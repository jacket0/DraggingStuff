using System;
using System.Collections.Generic;

public static class SlimeSolutionFinder
{
    public static IReadOnlyList<TimedLevelMove> Find(BoardStateSnapshot layout, int seed, int attempts, int moveLimit)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        if (attempts < 1)
            throw new ArgumentOutOfRangeException(nameof(attempts));

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            SlimeBotResult result = new SlimeBotSimulation(SlimeBotSimulation.Policy.Skilled, new Random(unchecked(seed * 31 + attempt))).Run(layout, moveLimit);

            if (result.IsWon)
                return result.Moves.AsReadOnly();
        }

        return null;
    }
}
