using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ShelfFilterBotReport
{
    private const int RunsPerVariant = 200;
    private const int MoveLimit = 300;
    private const int RandomMoveAttempts = 200;

    private enum BotKind
    {
        Greedy,
        Thrifty
    }

    private enum RunOutcome
    {
        Cleared,
        DeadEnd,
        MoveLimit
    }

    [MenuItem("Tools/Timed Levels/Filter Bot Report")]
    public static void Run()
    {
        TimedLevelDefinition definition = ShelfFilterGenerationPreview.CreatePreviewDefinition();

        try
        {
            Dictionary<TimedLevelVariantGenerator.CandidateRejection, int> rejections = new Dictionary<TimedLevelVariantGenerator.CandidateRejection, int>();
            List<TimedLevelVariant> candidates = TimedLevelVariantGenerator.GenerateCandidates(
                definition,
                ShelfFilterGenerationPreview.PreviewSeedLevelNumber,
                ShelfFilterGenerationPreview.CreateBoardShape(),
                rejections);
            int medianMoveCount = TimedLevelVariantGenerator.GetMedian(candidates.Select(candidate => candidate.MoveCount));
            TimedLevelVariant[] variants = candidates
                .Where(candidate => Math.Abs(candidate.MoveCount - medianMoveCount) <= medianMoveCount * 0.1d)
                .Take(ShelfFilterGenerationPreview.RequiredVariantCount)
                .ToArray();
            Report(definition.name, variants);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    public static void Report(string label, IReadOnlyList<TimedLevelVariant> variants)
    {
        if (variants == null || variants.Count == 0)
            throw new ArgumentException("At least one variant is required.", nameof(variants));

        List<string> lines = new List<string>();

        foreach (BotKind bot in new[] { BotKind.Greedy, BotKind.Thrifty })
        {
            foreach (bool hasFilters in new[] { true, false })
            {
                BotStatistics statistics = new BotStatistics();

                foreach (TimedLevelVariant variant in variants)
                {
                    BoardStateSnapshot layout = variant.CreateLayout();

                    if (!hasFilters)
                        layout = RemoveFilters(layout);

                    for (int run = 0; run < RunsPerVariant; run++)
                        statistics.Add(PlayRun(layout, bot, new System.Random(checked(variant.Seed * 1000 + run))));
                }

                lines.Add($"{bot} {(hasFilters ? "filters" : "control")}: {statistics}");
            }
        }

        Debug.Log($"SHELF_FILTER_BOT_REPORT {label}, {variants.Count} variants x {RunsPerVariant} runs, limit {MoveLimit} moves:\n{string.Join("\n", lines)}");
    }

    private static RunResult PlayRun(BoardStateSnapshot layout, BotKind bot, System.Random random)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = layout;
        bool reachedOnlyFilteredEmptyColumns = false;

        for (int moveCount = 0; moveCount < MoveLimit; moveCount++)
        {
            if (state.IsCleared)
                return new RunResult(RunOutcome.Cleared, moveCount, reachedOnlyFilteredEmptyColumns);

            reachedOnlyFilteredEmptyColumns |= CountUniversalEmptyColumns(state) == 0 && TimedLevelLayoutRules.CountFilteredEmptyColumns(state) > 0;

            if (!TryChooseMove(simulator, state, bot, random, out BoardMoveSimulation simulation))
                return new RunResult(RunOutcome.DeadEnd, moveCount, reachedOnlyFilteredEmptyColumns);

            state = simulation.State;
        }

        return new RunResult(state.IsCleared ? RunOutcome.Cleared : RunOutcome.MoveLimit, MoveLimit, reachedOnlyFilteredEmptyColumns);
    }

    private static bool TryChooseMove(BoardMoveSimulator simulator, BoardStateSnapshot state, BotKind bot, System.Random random, out BoardMoveSimulation simulation)
    {
        if (TryChooseMatchMove(simulator, state, random, out simulation))
            return true;

        if (bot == BotKind.Thrifty && TryChooseFilteredPlacement(simulator, state, random, out simulation))
            return true;

        Func<BoardMoveSimulation, bool> isPreferred = bot == BotKind.Thrifty
            ? candidate => CountUniversalEmptyColumns(candidate.State) > 0 || CountUniversalEmptyColumns(state) == 0
            : (Func<BoardMoveSimulation, bool>)(_ => true);

        if (TrySampleMove(simulator, state, random, isPreferred, out simulation))
            return true;

        if (bot == BotKind.Thrifty && TrySampleMove(simulator, state, random, _ => true, out simulation))
            return true;

        List<BoardMoveSimulation> allowedMoves = EnumerateAllowedMoves(simulator, state).ToList();

        if (allowedMoves.Count == 0)
            return false;

        List<BoardMoveSimulation> preferredMoves = allowedMoves.Where(isPreferred).ToList();
        List<BoardMoveSimulation> pool = preferredMoves.Count > 0 ? preferredMoves : allowedMoves;
        simulation = pool[random.Next(pool.Count)];
        return true;
    }

    private static bool TryChooseMatchMove(BoardMoveSimulator simulator, BoardStateSnapshot state, System.Random random, out BoardMoveSimulation simulation)
    {
        List<BoardMoveSimulation> matchMoves = new List<BoardMoveSimulation>();

        foreach ((ColumnPosition slot, ItemType neededType) in FindMatchSlots(state))
        {
            foreach (ColumnPosition source in FindColumnsWithFront(state, neededType))
            {
                if (source.Equals(slot))
                    continue;

                if (simulator.TrySimulate(state, source, slot, out BoardMoveSimulation candidate) && candidate.IsAllowed && candidate.MatchCount > 0)
                    matchMoves.Add(candidate);
            }
        }

        if (matchMoves.Count == 0)
        {
            simulation = null;
            return false;
        }

        int bestMatchCount = matchMoves.Max(candidate => candidate.MatchCount);
        List<BoardMoveSimulation> bestMoves = matchMoves.Where(candidate => candidate.MatchCount == bestMatchCount).ToList();
        simulation = bestMoves[random.Next(bestMoves.Count)];
        return true;
    }

    private static bool TryChooseFilteredPlacement(BoardMoveSimulator simulator, BoardStateSnapshot state, System.Random random, out BoardMoveSimulation simulation)
    {
        List<BoardMoveSimulation> placements = new List<BoardMoveSimulation>();

        foreach (ColumnPosition target in EnumeratePositions(state).Where(position => IsFilteredEmptyColumn(state, position)))
        {
            foreach (ColumnPosition source in EnumeratePositions(state).Where(position => !state.Shelves[position.ShelfIndex].IsFiltered))
            {
                if (simulator.TrySimulate(state, source, target, out BoardMoveSimulation candidate) && candidate.IsAllowed)
                    placements.Add(candidate);
            }
        }

        simulation = placements.Count > 0 ? placements[random.Next(placements.Count)] : null;
        return simulation != null;
    }

    private static bool TrySampleMove(
        BoardMoveSimulator simulator,
        BoardStateSnapshot state,
        System.Random random,
        Func<BoardMoveSimulation, bool> isPreferred,
        out BoardMoveSimulation simulation)
    {
        ColumnPosition[] positions = EnumeratePositions(state).ToArray();
        ColumnPosition[] emptyPositions = positions.Where(position => state.Shelves[position.ShelfIndex].Columns[position.ColumnIndex].IsEmpty).ToArray();

        for (int attempt = 0; attempt < RandomMoveAttempts; attempt++)
        {
            ColumnPosition[] targets = emptyPositions.Length > 0 && random.Next(2) == 0 ? emptyPositions : positions;
            ColumnPosition source = positions[random.Next(positions.Length)];
            ColumnPosition target = targets[random.Next(targets.Length)];

            if (simulator.TrySimulate(state, source, target, out BoardMoveSimulation candidate) && candidate.IsAllowed && isPreferred(candidate))
            {
                simulation = candidate;
                return true;
            }
        }

        simulation = null;
        return false;
    }

    private static IEnumerable<BoardMoveSimulation> EnumerateAllowedMoves(BoardMoveSimulator simulator, BoardStateSnapshot state)
    {
        ColumnPosition[] positions = EnumeratePositions(state).ToArray();

        foreach (ColumnPosition source in positions)
        {
            foreach (ColumnPosition target in positions)
            {
                if (simulator.TrySimulate(state, source, target, out BoardMoveSimulation candidate) && candidate.IsAllowed)
                    yield return candidate;
            }
        }
    }

    private static IEnumerable<(ColumnPosition Slot, ItemType NeededType)> FindMatchSlots(BoardStateSnapshot state)
    {
        for (int shelfIndex = 0; shelfIndex < state.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = state.Shelves[shelfIndex];

            if (!shelf.IsOpen)
                continue;

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                ColumnStateSnapshot[] otherColumns = shelf.Columns.Where((_, index) => index != columnIndex).ToArray();

                if (otherColumns.Any(column => column.IsEmpty))
                    continue;

                ItemType neededType = otherColumns[0].Items[0];

                if (otherColumns.All(column => column.Items[0] == neededType))
                    yield return (new ColumnPosition(shelfIndex, columnIndex), neededType);
            }
        }
    }

    private static IEnumerable<ColumnPosition> FindColumnsWithFront(BoardStateSnapshot state, ItemType type)
    {
        return EnumeratePositions(state).Where(position =>
        {
            ColumnStateSnapshot column = state.Shelves[position.ShelfIndex].Columns[position.ColumnIndex];
            return !column.IsEmpty && column.Items[0] == type;
        });
    }

    private static IEnumerable<ColumnPosition> EnumeratePositions(BoardStateSnapshot state)
    {
        for (int shelfIndex = 0; shelfIndex < state.Shelves.Count; shelfIndex++)
        {
            for (int columnIndex = 0; columnIndex < state.Shelves[shelfIndex].Capacity; columnIndex++)
                yield return new ColumnPosition(shelfIndex, columnIndex);
        }
    }

    private static bool IsFilteredEmptyColumn(BoardStateSnapshot state, ColumnPosition position)
    {
        ShelfStateSnapshot shelf = state.Shelves[position.ShelfIndex];
        return shelf.IsFiltered && shelf.Columns[position.ColumnIndex].IsEmpty;
    }

    private static int CountUniversalEmptyColumns(BoardStateSnapshot state)
    {
        return state.Shelves
            .Where(shelf => shelf.IsOpen && !shelf.IsFiltered)
            .Sum(shelf => shelf.Columns.Count(column => column.IsEmpty));
    }

    private static BoardStateSnapshot RemoveFilters(BoardStateSnapshot board)
    {
        return new BoardStateSnapshot(board.Shelves.Select(shelf => new ShelfStateSnapshot(shelf.Columns, shelf.IsOpen, shelf.IsConveyor)).ToArray());
    }

    private readonly struct RunResult
    {
        public RunOutcome Outcome { get; }
        public int MoveCount { get; }
        public bool ReachedOnlyFilteredEmptyColumns { get; }

        public RunResult(RunOutcome outcome, int moveCount, bool reachedOnlyFilteredEmptyColumns)
        {
            Outcome = outcome;
            MoveCount = moveCount;
            ReachedOnlyFilteredEmptyColumns = reachedOnlyFilteredEmptyColumns;
        }
    }

    private sealed class BotStatistics
    {
        private int _runCount;
        private int _clearedCount;
        private int _deadEndCount;
        private long _clearedMoveSum;
        private int _onlyFilteredEmptyCount;
        private int _onlyFilteredEmptyFailureCount;

        public void Add(RunResult result)
        {
            _runCount++;

            if (result.Outcome == RunOutcome.Cleared)
            {
                _clearedCount++;
                _clearedMoveSum += result.MoveCount;
            }

            if (result.Outcome == RunOutcome.DeadEnd)
                _deadEndCount++;

            if (!result.ReachedOnlyFilteredEmptyColumns)
                return;

            _onlyFilteredEmptyCount++;

            if (result.Outcome != RunOutcome.Cleared)
                _onlyFilteredEmptyFailureCount++;
        }

        public override string ToString()
        {
            double averageMoves = _clearedCount > 0 ? (double)_clearedMoveSum / _clearedCount : 0d;
            return $"cleared {Percent(_clearedCount, _runCount)}, avg moves to clear {averageMoves:F1}, dead ends {Percent(_deadEndCount, _runCount)}, " +
                $"only filtered empty columns in {Percent(_onlyFilteredEmptyCount, _runCount)} of runs, " +
                $"{_onlyFilteredEmptyFailureCount}/{_onlyFilteredEmptyCount} of them failed";
        }

        private static string Percent(int count, int total) => total > 0 ? $"{100d * count / total:F1}%" : "n/a";
    }
}
