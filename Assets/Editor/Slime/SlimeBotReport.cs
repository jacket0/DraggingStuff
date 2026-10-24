using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public sealed class SlimeBotReport
{
    private const int BaseSeed = 20261024;

    private readonly Dictionary<SlimeBotSimulation.Policy, List<SlimeBotResult>> _results = new Dictionary<SlimeBotSimulation.Policy, List<SlimeBotResult>>();

    public IReadOnlyList<SlimeBotResult> GetResults(SlimeBotSimulation.Policy policy)
    {
        return _results.TryGetValue(policy, out List<SlimeBotResult> results) ? results : (IReadOnlyList<SlimeBotResult>)Array.Empty<SlimeBotResult>();
    }

    public void RunVariant(BoardStateSnapshot layout, int variantIndex, int runsPerPolicy, int moveLimit)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        foreach (SlimeBotSimulation.Policy policy in (SlimeBotSimulation.Policy[])Enum.GetValues(typeof(SlimeBotSimulation.Policy)))
        {
            if (!_results.TryGetValue(policy, out List<SlimeBotResult> results))
            {
                results = new List<SlimeBotResult>();
                _results.Add(policy, results);
            }

            for (int run = 0; run < runsPerPolicy; run++)
            {
                Random random = new Random(unchecked(BaseSeed * 397 ^ (int)policy * 131 ^ variantIndex * 7919 ^ run));
                results.Add(new SlimeBotSimulation(policy, random).Run(layout, moveLimit));
            }
        }
    }

    public string Format(string title, int moveLimit)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"SLIME_BOT {title}: move limit {moveLimit}");

        foreach (KeyValuePair<SlimeBotSimulation.Policy, List<SlimeBotResult>> pair in _results)
        {
            List<SlimeBotResult> results = pair.Value;
            List<SlimeBotResult> wins = results.Where(result => result.IsWon).ToList();
            builder.AppendLine(
                $"  {pair.Key}: runs {results.Count}, wins {Percent(wins.Count, results.Count)}% (avg {Average(wins.Select(result => result.MoveCount)):0.#} moves), " +
                $"growths {Average(results.Select(result => result.GrowthCount)):0.#} (first at move {Average(results.Where(result => result.FirstGrowthMove >= 0).Select(result => result.FirstGrowthMove)):0.#}), " +
                $"cleaned {Average(results.Select(result => result.CleanedColumnCount)):0.#}, " +
                $"max sticky {Average(results.Select(result => result.MaximumStickyCount)):0.#}, " +
                $"sticky at end {Average(results.Select(result => result.StickyAtEnd)):0.#}, " +
                $"stuck items {Average(results.Select(result => result.StuckItemCount)):0.#}, " +
                $"rescues in {Percent(results.Count(result => result.RescueCount > 0), results.Count)}% of runs, " +
                $"no moves {results.Count(result => result.HasNoMoves)}");
        }

        return builder.ToString();
    }

    private static double Average(IEnumerable<int> values)
    {
        int[] array = values.ToArray();
        return array.Length == 0 ? 0d : array.Average();
    }

    private static double Percent(int count, int total) => total == 0 ? 0d : Math.Round(100d * count / total, 1);
}
