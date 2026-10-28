using System;
using System.Collections.Generic;
using System.Linq;

public static class SlimeEarlyLossCheck
{
    public const int MinimumMovesBeforeLoss = 7;
    public const int RunsPerPolicy = 12;

    private static readonly SlimeBotSimulation.Policy[] Policies = { SlimeBotSimulation.Policy.Greedy, SlimeBotSimulation.Policy.Careful };

    public static bool HasEarlyLoss(BoardStateSnapshot layout) => RunBots(layout).Any(isLost => isLost);

    public static int CountEarlyLosses(BoardStateSnapshot layout) => RunBots(layout).Count(isLost => isLost);

    private static IEnumerable<bool> RunBots(BoardStateSnapshot layout)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        int layoutSeed = BoardStateFingerprint.CreateHash(layout).Aggregate(17, (hash, symbol) => unchecked(hash * 31 + symbol));

        foreach (SlimeBotSimulation.Policy policy in Policies)
        {
            for (int run = 0; run < RunsPerPolicy; run++)
            {
                SlimeBotSimulation bot = new SlimeBotSimulation(policy, new Random(unchecked(layoutSeed + (int)policy * 7919 + run)));
                SlimeBotResult result = bot.Run(layout, MinimumMovesBeforeLoss);
                yield return !result.IsWon && (result.IsDeadlocked || result.FirstHopelessMove >= 0);
            }
        }
    }
}
