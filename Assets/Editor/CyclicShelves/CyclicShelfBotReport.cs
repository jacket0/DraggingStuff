using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public sealed class CyclicShelfBotReport
{
    public const int DefaultProbeMatchCount = 4;
    private const int BaseSeed = 20261014;

    private readonly Dictionary<CyclicShelfBotSimulation.Policy, List<CyclicShelfBotResult>> _results = new Dictionary<CyclicShelfBotSimulation.Policy, List<CyclicShelfBotResult>>();
    private readonly List<string> _probeResults = new List<string>();

    public IReadOnlyList<CyclicShelfBotResult> GetResults(CyclicShelfBotSimulation.Policy policy)
    {
        return _results.TryGetValue(policy, out List<CyclicShelfBotResult> results) ? results : (IReadOnlyList<CyclicShelfBotResult>)Array.Empty<CyclicShelfBotResult>();
    }

    public void RunVariant(BoardStateSnapshot layout, int variantIndex, int runsPerPolicy, int moveLimit, int probeMatchCount)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        foreach (CyclicShelfBotSimulation.Policy policy in (CyclicShelfBotSimulation.Policy[])Enum.GetValues(typeof(CyclicShelfBotSimulation.Policy)))
        {
            if (!_results.TryGetValue(policy, out List<CyclicShelfBotResult> results))
            {
                results = new List<CyclicShelfBotResult>();
                _results.Add(policy, results);
            }

            for (int run = 0; run < runsPerPolicy; run++)
            {
                Random random = new Random(unchecked(BaseSeed * 397 ^ (int)policy * 131 ^ variantIndex * 7919 ^ run));
                results.Add(new CyclicShelfBotSimulation(policy, random).Run(layout, moveLimit));
            }
        }

        CyclicShelfDeadlockProbe.ProbeStatus status = new CyclicShelfDeadlockProbe().Check(layout, probeMatchCount, out int deadlockMatchCount);
        _probeResults.Add(status == CyclicShelfDeadlockProbe.ProbeStatus.Deadlock ? $"deadlock@{deadlockMatchCount}" : status.ToString());
    }

    public string Format(string title, int moveLimit)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"CYCLIC_SHELF_BOT {title}: move limit {moveLimit}");

        foreach (KeyValuePair<CyclicShelfBotSimulation.Policy, List<CyclicShelfBotResult>> pair in _results)
        {
            List<CyclicShelfBotResult> results = pair.Value;
            List<CyclicShelfBotResult> wins = results.Where(result => result.IsWon).ToList();
            List<int> firstRescueMoves = results.Where(result => result.FirstRescueMove >= 0).Select(result => result.FirstRescueMove).ToList();
            int closingCount = results.Sum(result => result.ClosingCount);
            builder.AppendLine(
                $"  {pair.Key}: runs {results.Count}, wins {Percent(wins.Count, results.Count)}% (avg {Average(wins.Select(result => result.MoveCount)):0.#} moves), " +
                $"early switch {Percent(results.Count(result => result.EarlySwitchCount > 0), results.Count)}%, " +
                $"open all {Percent(results.Count(result => result.StopCount > 0), results.Count)}%, " +
                $"first help at move {Average(firstRescueMoves):0.#}, " +
                $"cutting closings {Percent(results.Sum(result => result.CuttingClosingCount), closingCount)}% of {closingCount}, " +
                $"no moves {results.Count(result => result.HasNoMoves)}");
        }

        builder.AppendLine("  direct match probe: " + string.Join(" ", _probeResults));
        return builder.ToString();
    }

    private static double Average(IEnumerable<int> values)
    {
        int[] array = values.ToArray();
        return array.Length == 0 ? 0d : array.Average();
    }

    private static double Percent(int count, int total) => total == 0 ? 0d : Math.Round(100d * count / total, 1);
}
