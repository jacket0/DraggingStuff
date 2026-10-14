using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CyclicShelfBotSimulation
{
    public enum Policy
    {
        Greedy,
        Cautious,
        Random
    }

    private const int MaximumEvacuationsPerClosing = 2;

    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();
    private readonly Policy _policy;
    private readonly Random _random;

    public CyclicShelfBotSimulation(Policy policy, Random random)
    {
        _policy = policy;
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public CyclicShelfBotResult Run(BoardStateSnapshot layout, int moveLimit)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        if (moveLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(moveLimit));

        CyclicShelfBotResult result = new CyclicShelfBotResult();
        BoardStateSnapshot board = layout;
        bool hasMatchSinceRescue = true;
        int evacuationCount = 0;

        while (result.MoveCount < moveLimit)
        {
            if (board.IsCleared)
            {
                result.IsWon = true;
                return result;
            }

            if (CycleDeadlockRule.IsStuck(board))
            {
                board = ApplyRescue(board, !hasMatchSinceRescue, result);
                hasMatchSinceRescue = false;
                continue;
            }

            List<CyclicShelfBotMove> moves = CyclicShelfMoveFinder.FindMoves(board);
            BoardMoveSimulation simulation = null;

            while (moves.Count > 0 && simulation == null)
            {
                CyclicShelfBotMove move = ChooseMove(board, moves, evacuationCount);
                moves.Remove(move);

                if (_simulator.TrySimulate(board, move.Source, move.Target, out BoardMoveSimulation candidate) && candidate.IsAllowed)
                {
                    simulation = candidate;
                    evacuationCount = move.IsEvacuation ? evacuationCount + 1 : evacuationCount;
                }
            }

            if (simulation == null)
            {
                result.HasNoMoves = true;
                return result;
            }

            result.MoveCount++;

            if (simulation.MatchCount > 0)
            {
                hasMatchSinceRescue = true;
                evacuationCount = 0;
                result.MatchingMoveCount++;
                RegisterClosings(simulation, result);
            }

            board = simulation.State;
        }

        result.IsWon = board.IsCleared;
        return result;
    }

    private BoardStateSnapshot ApplyRescue(BoardStateSnapshot board, bool isRepeated, CyclicShelfBotResult result)
    {
        if (result.FirstRescueMove < 0)
            result.FirstRescueMove = result.MoveCount;

        if (isRepeated)
            result.StopCount++;
        else
            result.EarlySwitchCount++;

        return _simulator.ApplyCycleRescue(board, isRepeated, out _);
    }

    private static void RegisterClosings(BoardMoveSimulation simulation, CyclicShelfBotResult result)
    {
        foreach (int shelfIndex in simulation.SwitchedShelfIndexes.Where(shelfIndex => !simulation.State.Shelves[shelfIndex].IsOpen))
        {
            result.ClosingCount++;

            if (CutsOffItems(simulation.State, shelfIndex))
                result.CuttingClosingCount++;
        }
    }

    private static bool CutsOffItems(BoardStateSnapshot board, int closedShelfIndex)
    {
        Dictionary<ItemType, int> openCounts = CountOpenItems(board);

        foreach (ItemType type in board.Shelves[closedShelfIndex].Columns.SelectMany(column => column.Items).Distinct())
        {
            openCounts.TryGetValue(type, out int openCount);

            if (openCount % ShelfStateSnapshot.MinimumMatchCapacity != 0)
                return true;
        }

        return false;
    }

    private CyclicShelfBotMove ChooseMove(BoardStateSnapshot board, List<CyclicShelfBotMove> moves, int evacuationCount)
    {
        if (_policy == Policy.Random)
            return moves[_random.Next(moves.Count)];

        if (_policy == Policy.Cautious)
        {
            CyclicShelfBotMove[] closingMatches = moves.Where(move => move.IsMatch && CyclicShelfMoveFinder.IsClosingNext(board, move.Source.ShelfIndex)).ToArray();

            if (closingMatches.Length > 0)
                return closingMatches[_random.Next(closingMatches.Length)];

            if (evacuationCount < MaximumEvacuationsPerClosing)
            {
                CyclicShelfBotMove[] evacuations = moves.Where(move => move.IsEvacuation).ToArray();

                if (evacuations.Length > 0)
                    return evacuations[_random.Next(evacuations.Length)];
            }
        }

        CyclicShelfBotMove[] matches = moves.Where(move => move.IsMatch).ToArray();

        if (matches.Length > 0)
            return matches[_random.Next(matches.Length)];

        CyclicShelfBotMove[] setups = moves.Where(move => move.IsSetup && (_policy != Policy.Cautious || !CyclicShelfMoveFinder.IsClosingNext(board, move.Target.ShelfIndex))).ToArray();

        if (setups.Length > 0)
            return setups[_random.Next(setups.Length)];

        return moves[_random.Next(moves.Count)];
    }

    private static Dictionary<ItemType, int> CountOpenItems(BoardStateSnapshot board)
    {
        Dictionary<ItemType, int> counts = new Dictionary<ItemType, int>();

        foreach (ItemType type in board.Shelves.Where(shelf => shelf.IsOpen).SelectMany(shelf => shelf.Columns).SelectMany(column => column.Items))
        {
            counts.TryGetValue(type, out int count);
            counts[type] = count + 1;
        }

        return counts;
    }
}

public sealed class CyclicShelfBotResult
{
    public bool IsWon { get; set; }
    public bool HasNoMoves { get; set; }
    public int MoveCount { get; set; }
    public int MatchingMoveCount { get; set; }
    public int EarlySwitchCount { get; set; }
    public int StopCount { get; set; }
    public int FirstRescueMove { get; set; } = -1;
    public int ClosingCount { get; set; }
    public int CuttingClosingCount { get; set; }
}
