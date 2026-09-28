using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ClosedShelfLevelSimulation
{
    public enum Policy
    {
        IgnoresConditions,
        WatchesConditions
    }

    // Draft parameter: no recorded LevelRunResult playtests exist yet for TimedLevel_13-16
    // (they are brand new). Replace with time / group count from real playtests once available.
    private const double DraftSecondsPerMove = 2.5;
    private const int BaseSeed = 20260921;

    private sealed class RunResult
    {
        public bool Won;
        public bool Deadlocked;
        public string DeadlockBoard;
        public bool Faulted;
        public string FaultMessage;
        public int MoveCount;
        public readonly Dictionary<int, int> ShelfOpenedAtMove = new Dictionary<int, int>();
    }

    private sealed class PolicyReport
    {
        public int Runs;
        public int Wins;
        public int Deadlocks;
        public string FirstDeadlockBoard;
        public int Faults;
        public readonly List<int> WinMoveCounts = new List<int>();
        public readonly Dictionary<int, List<int>> ShelfOpenMoveCounts = new Dictionary<int, List<int>>();
    }

    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string ReportPath = "Temp/ClosedShelfBalanceReport.txt";

    [MenuItem("Tools/Timed Levels/Closed Shelves/Run Balance Bot (Preview)")]
    public static void RunPreview() => Debug.Log(RunCatalog(runsPerPolicy: 50));

    [MenuItem("Tools/Timed Levels/Closed Shelves/Run Balance Bot")]
    public static void RunFull() => Debug.Log(RunCatalog(runsPerPolicy: 1000));

    public static string RunCatalog(int runsPerPolicy) => RunLevels(runsPerPolicy, level => true);

    public static string RunLevel(int levelNumber, int runsPerPolicy) => RunLevels(runsPerPolicy, level => level.Number == levelNumber);

    private static string RunLevels(int runsPerPolicy, Func<LevelEntry, bool> filter)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null)
            throw new InvalidOperationException($"{CatalogPath} was not found.");

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        foreach (LevelEntry level in catalog.Levels.Where(level => level.Definition.HasClosedShelves && filter(level)))
            builder.Append(Run(level.Definition, runsPerPolicy));

        System.IO.File.WriteAllText(ReportPath, builder.ToString());
        return builder.ToString();
    }

    public static string Run(TimedLevelDefinition definition, int runsPerPolicy)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (!definition.HasClosedShelves)
            throw new InvalidOperationException($"{definition.name}: no closed shelves configured.");

        if (definition.Variants.Count == 0)
            throw new InvalidOperationException($"{definition.name}: no generated variants to start from.");

        BoardStateSnapshot startingLayout = definition.Variants[0].CreateLayout();
        IReadOnlyList<ItemType> levelTypes = definition.ItemGroups.Select(group => group.Type).Distinct().ToArray();
        int moveLimit = ComputeMoveLimit(definition);

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine($"ClosedShelfLevelSimulation: {definition.name}, moveLimit={moveLimit} ({runsPerPolicy} runs/policy)");

        foreach (Policy policy in new[] { Policy.IgnoresConditions, Policy.WatchesConditions })
        {
            PolicyReport report = new PolicyReport();

            for (int run = 0; run < runsPerPolicy; run++)
            {
                System.Random random = new System.Random(unchecked(BaseSeed * 397 ^ (int)policy * 131 ^ run));
                RunResult result = SimulateOne(definition, startingLayout, levelTypes, moveLimit, policy, random);
                Accumulate(report, result);
            }

            builder.Append(FormatReport(definition, policy, report, moveLimit));
        }

        return builder.ToString();
    }

    private static int ComputeMoveLimit(TimedLevelDefinition definition)
    {
        return Math.Max(1, (int)(definition.TimeLimitSeconds / DraftSecondsPerMove));
    }

    private static void Accumulate(PolicyReport report, RunResult result)
    {
        report.Runs++;

        if (result.Faulted)
        {
            report.Faults++;
            return;
        }

        if (result.Deadlocked)
        {
            report.Deadlocks++;
            report.FirstDeadlockBoard ??= result.DeadlockBoard;
        }

        if (result.Won)
        {
            report.Wins++;
            report.WinMoveCounts.Add(result.MoveCount);
        }

        foreach (KeyValuePair<int, int> pair in result.ShelfOpenedAtMove)
        {
            if (!report.ShelfOpenMoveCounts.TryGetValue(pair.Key, out List<int> counts))
            {
                counts = new List<int>();
                report.ShelfOpenMoveCounts[pair.Key] = counts;
            }

            counts.Add(pair.Value);
        }
    }

    private static string FormatReport(TimedLevelDefinition definition, Policy policy, PolicyReport report, int moveLimit)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine($"[{definition.name}] policy={policy}: runs={report.Runs}, faults={report.Faults}, deadlocks={report.Deadlocks} ({Percent(report.Deadlocks, report.Runs)}%)");
        builder.AppendLine($"  wins={report.Wins} ({Percent(report.Wins, report.Runs)}%), avg moves to win={Average(report.WinMoveCounts):0.#}");

        foreach (ClosedShelfDefinition closedShelf in definition.ClosedShelves.OrderBy(shelf => shelf.ShelfIndex))
        {
            report.ShelfOpenMoveCounts.TryGetValue(closedShelf.ShelfIndex, out List<int> openMoves);
            int openedCount = openMoves?.Count ?? 0;
            int withinLimit = openMoves?.Count(move => move <= moveLimit) ?? 0;
            builder.AppendLine(
                $"  shelf {closedShelf.ShelfIndex}: opened {openedCount}/{report.Runs} ({Percent(openedCount, report.Runs)}%), within limit {Percent(withinLimit, report.Runs)}%, avg move={Average(openMoves):0.#}");
        }

        if (report.FirstDeadlockBoard != null)
            builder.AppendLine($"  first deadlock: {report.FirstDeadlockBoard}");

        return builder.ToString();
    }

    private static string DescribeBoard(BoardStateSnapshot board)
    {
        return string.Join(" ", board.Shelves.Select((shelf, index) =>
            $"{index}{(shelf.IsOpen ? "" : "C")}:" + string.Join("|", shelf.Columns.Select(column => string.Join(",", column.Items.Select(type => type.ToString().Substring(0, 3)))))));
    }

    private static double Average(IReadOnlyCollection<int> values) => values == null || values.Count == 0 ? 0d : values.Average();
    private static double Percent(int count, int total) => total == 0 ? 0d : Math.Round(100d * count / total, 1);

    private static RunResult SimulateOne(TimedLevelDefinition definition, BoardStateSnapshot startingLayout, IReadOnlyList<ItemType> levelTypes, int moveLimit, Policy policy, System.Random random)
    {
        RunResult result = new RunResult();

        try
        {
            BoardMoveSimulator simulator = new BoardMoveSimulator();
            TripleRefillGenerator refill = new TripleRefillGenerator(random, definition.RefillSettings, levelTypes);
            ClosedShelfProgress progress = new ClosedShelfProgress(definition.ClosedShelves);
            BoardStateSnapshot board = startingLayout;

            for (int move = 0; move < moveLimit; move++)
            {
                if (board.IsCleared && progress.ClosedShelfIndexes.All(index => progress.GetState(index) == ClosedShelfState.Open))
                {
                    result.Won = true;
                    result.MoveCount = move;
                    return result;
                }

                List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> candidates = EnumerateMoves(simulator, board);

                if (candidates.Count == 0)
                {
                    result.Deadlocked = true;
                    result.DeadlockBoard = DescribeBoard(board);
                    result.MoveCount = move;
                    return result;
                }

                (ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation) chosen = policy == Policy.IgnoresConditions
                    ? ChooseIgnoresConditions(candidates, random)
                    : ChooseWatchesConditions(candidates, board, progress, random);

                foreach (MatchInfo match in chosen.Simulation.Matches)
                    progress.RegisterMatch(match);

                board = chosen.Simulation.State;
                board = RevealCompletedShelves(board, definition, progress, refill, result, move);

                if (refill.ShouldRefill(board, progress.IsRefillActive))
                {
                    GenerationBatch batch = refill.GenerateRefill(board, progress.PriorityTypes, definition.EmptyColumnCount);
                    board = ApplyBatch(board, batch);
                }
            }

            result.MoveCount = moveLimit;
            return result;
        }
        catch (Exception exception)
        {
            result.Faulted = true;
            result.FaultMessage = exception.Message;
            return result;
        }
    }

    private static List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> EnumerateMoves(BoardMoveSimulator simulator, BoardStateSnapshot board)
    {
        List<ColumnPosition> openColumns = new List<ColumnPosition>();

        for (int shelfIndex = 0; shelfIndex < board.Shelves.Count; shelfIndex++)
        {
            if (!board.Shelves[shelfIndex].IsOpen)
                continue;

            for (int columnIndex = 0; columnIndex < board.Shelves[shelfIndex].Capacity; columnIndex++)
                openColumns.Add(new ColumnPosition(shelfIndex, columnIndex));
        }

        List<(ColumnPosition, ColumnPosition, BoardMoveSimulation)> candidates = new List<(ColumnPosition, ColumnPosition, BoardMoveSimulation)>();

        foreach (ColumnPosition source in openColumns)
        {
            if (board.Shelves[source.ShelfIndex].Columns[source.ColumnIndex].IsEmpty)
                continue;

            foreach (ColumnPosition target in openColumns)
            {
                if (source.Equals(target))
                    continue;

                if (simulator.TrySimulate(board, source, target, out BoardMoveSimulation simulation) && simulation.IsAllowed)
                    candidates.Add((source, target, simulation));
            }
        }

        return candidates;
    }

    private static (ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation) ChooseIgnoresConditions(
        List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> candidates, System.Random random)
    {
        List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> matching = candidates.Where(c => c.Simulation.MatchCount > 0).ToList();
        List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> pool = matching.Count > 0 ? matching : candidates;
        return pool[random.Next(pool.Count)];
    }

    private static (ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation) ChooseWatchesConditions(
        List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> candidates, BoardStateSnapshot board, ClosedShelfProgress progress, System.Random random)
    {
        List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> matching = candidates.Where(c => c.Simulation.MatchCount > 0).ToList();

        if (matching.Count > 0)
        {
            List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> crediting = matching.Where(c => WouldCredit(c.Simulation, progress)).ToList();
            List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> pool = crediting.Count > 0 ? crediting : matching;
            return pool[random.Next(pool.Count)];
        }

        IReadOnlyDictionary<ItemType, int> priorityTypes = progress.PriorityTypes;

        HashSet<int> targetShelves = new HashSet<int>(progress.ClosedShelfIndexes
            .Where(index => progress.GetState(index) == ClosedShelfState.Closed && progress.GetCondition(index) is ShelfMatchesCondition)
            .Select(index => ((ShelfMatchesCondition)progress.GetCondition(index)).TargetShelfIndex));

        List<(ColumnPosition Source, ColumnPosition Target, BoardMoveSimulation Simulation)> preparing = candidates.Where(c =>
        {
            ItemType? frontType = board.Shelves[c.Source.ShelfIndex].Columns[c.Source.ColumnIndex].FrontItem;
            bool preparesType = frontType.HasValue && priorityTypes.ContainsKey(frontType.Value);
            bool preparesShelf = targetShelves.Contains(c.Target.ShelfIndex);
            return preparesType || preparesShelf;
        }).ToList();

        if (preparing.Count > 0)
            return preparing[random.Next(preparing.Count)];

        return candidates[random.Next(candidates.Count)];
    }

    private static bool WouldCredit(BoardMoveSimulation simulation, ClosedShelfProgress progress)
    {
        foreach (MatchInfo match in simulation.Matches)
        {
            foreach (int shelfIndex in progress.ClosedShelfIndexes)
            {
                if (progress.GetState(shelfIndex) != ClosedShelfState.Closed)
                    continue;

                ShelfUnlockCondition condition = progress.GetCondition(shelfIndex);

                if (condition.IsCompleted)
                    continue;

                switch (condition)
                {
                    case CollectItemsCondition collect when collect.RemainingByType.ContainsKey(match.Type):
                        return true;

                    case ShelfMatchesCondition shelfMatches when shelfMatches.TargetShelfIndex == match.ShelfIndex:
                        return true;
                }
            }
        }

        return false;
    }

    private static BoardStateSnapshot RevealCompletedShelves(BoardStateSnapshot board, TimedLevelDefinition definition, ClosedShelfProgress progress, TripleRefillGenerator refill, RunResult result, int move)
    {
        foreach (int shelfIndex in progress.ClosedShelfIndexes)
        {
            if (progress.GetState(shelfIndex) != ClosedShelfState.Revealing)
                continue;

            ClosedShelfDefinition closedShelf = definition.ClosedShelves.First(shelf => shelf.ShelfIndex == shelfIndex);
            Dictionary<ItemType, int> counts = ComputeTypeCounts(board);
            IReadOnlyList<IReadOnlyList<ItemType>> columns = refill.CreateRevealedShelf(closedShelf.RevealedGroupCount, counts);
            board = ApplyRevealedShelf(board, shelfIndex, columns);
            progress.MarkOpen(shelfIndex);
            result.ShelfOpenedAtMove[shelfIndex] = move;
        }

        return board;
    }

    private static BoardStateSnapshot ApplyRevealedShelf(BoardStateSnapshot board, int shelfIndex, IReadOnlyList<IReadOnlyList<ItemType>> columns)
    {
        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[board.Shelves.Count];

        for (int index = 0; index < shelves.Length; index++)
            shelves[index] = board.Shelves[index];

        ColumnStateSnapshot[] newColumns = columns.Select(items => new ColumnStateSnapshot(items.ToArray())).ToArray();
        shelves[shelfIndex] = new ShelfStateSnapshot(newColumns, true);
        return new BoardStateSnapshot(shelves);
    }

    private static BoardStateSnapshot ApplyBatch(BoardStateSnapshot board, GenerationBatch batch)
    {
        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[board.Shelves.Count];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            ShelfStateSnapshot shelf = board.Shelves[shelfIndex];
            ColumnStateSnapshot[] columns = new ColumnStateSnapshot[shelf.Capacity];

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                IReadOnlyList<ItemType> newItems = batch.GetItems(shelfIndex, columnIndex);
                columns[columnIndex] = newItems.Count == 0
                    ? shelf.Columns[columnIndex]
                    : new ColumnStateSnapshot(shelf.Columns[columnIndex].Items.Concat(newItems).ToArray());
            }

            shelves[shelfIndex] = new ShelfStateSnapshot(columns, shelf.IsOpen);
        }

        return new BoardStateSnapshot(shelves);
    }

    private static Dictionary<ItemType, int> ComputeTypeCounts(BoardStateSnapshot board)
    {
        Dictionary<ItemType, int> counts = new Dictionary<ItemType, int>();

        foreach (ShelfStateSnapshot shelf in board.Shelves)
        {
            foreach (ColumnStateSnapshot column in shelf.Columns)
            {
                foreach (ItemType type in column.Items)
                    counts[type] = counts.TryGetValue(type, out int existing) ? existing + 1 : 1;
            }
        }

        return counts;
    }
}
