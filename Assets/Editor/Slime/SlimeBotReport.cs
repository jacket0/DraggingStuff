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
        RunVariant(layout, variantIndex, runsPerPolicy, moveLimit, (SlimeBotSimulation.Policy[])Enum.GetValues(typeof(SlimeBotSimulation.Policy)));
    }

    public void RunVariant(BoardStateSnapshot layout, int variantIndex, int runsPerPolicy, int moveLimit, IReadOnlyCollection<SlimeBotSimulation.Policy> policies)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        foreach (SlimeBotSimulation.Policy policy in policies)
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
                $"deadlocks {Percent(results.Count(result => result.IsDeadlocked), results.Count)}%, " +
                $"hopeless {Percent(results.Count(result => result.FirstHopelessMove >= 0), results.Count)}%");
        }

        return builder.ToString();
    }

    public string FormatGridRow(string configName)
    {
        IReadOnlyList<SlimeBotResult> careful = GetResults(SlimeBotSimulation.Policy.Careful);
        IReadOnlyList<SlimeBotResult> skilled = GetResults(SlimeBotSimulation.Policy.Skilled);
        IReadOnlyList<SlimeBotResult> greedy = GetResults(SlimeBotSimulation.Policy.Greedy);
        IReadOnlyList<SlimeBotResult> baseline = GetResults(SlimeBotSimulation.Policy.Baseline);
        int[] hopelessMoves = careful.Where(result => result.FirstHopelessMove >= 0).Select(result => result.FirstHopelessMove).OrderBy(move => move).ToArray();
        string medianHopelessMove = hopelessMoves.Length == 0 ? "-" : hopelessMoves[hopelessMoves.Length / 2].ToString();

        return $"SLIME_GRID {configName}: " +
            $"baseline {Wins(baseline)}% | skilled {Wins(skilled)}% hopeless {Hopeless(skilled)}% dead {Dead(skilled)}% | " +
            $"careful {Wins(careful)}% hopeless {Hopeless(careful)}% (median move {medianHopelessMove}, before 15: {Percent(careful.Count(result => result.FirstHopelessMove >= 0 && result.FirstHopelessMove < 15), careful.Count)}%) dead {Dead(careful)}% | " +
            $"greedy {Wins(greedy)}% hopeless {Hopeless(greedy)}% dead {Dead(greedy)}% | " +
            $"sticky@10/20/30 {AverageStickyAt(careful, 10):0.#}/{AverageStickyAt(careful, 20):0.#}/{AverageStickyAt(careful, 30):0.#}";
    }

    private static double Wins(IReadOnlyList<SlimeBotResult> results) => Percent(results.Count(result => result.IsWon), results.Count);

    private static double Hopeless(IReadOnlyList<SlimeBotResult> results) => Percent(results.Count(result => result.FirstHopelessMove >= 0), results.Count);

    private static double Dead(IReadOnlyList<SlimeBotResult> results) => Percent(results.Count(result => result.IsDeadlocked), results.Count);

    private static double AverageStickyAt(IReadOnlyList<SlimeBotResult> results, int move)
    {
        return Average(results.Where(result => result.StickyAfterMove.Count >= move).Select(result => result.StickyAfterMove[move - 1]));
    }

    private static double Average(IEnumerable<int> values)
    {
        int[] array = values.ToArray();
        return array.Length == 0 ? 0d : array.Average();
    }

    private static double Percent(int count, int total) => total == 0 ? 0d : Math.Round(100d * count / total, 1);
}
