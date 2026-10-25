using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SlimeBotSimulation
{
    public enum Policy
    {
        Greedy,
        Careful,
        Skilled,
        Random,
        Baseline
    }

    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();
    private readonly Policy _policy;
    private readonly Random _random;

    public SlimeBotSimulation(Policy policy, Random random)
    {
        _policy = policy;
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public SlimeBotResult Run(BoardStateSnapshot layout, int moveLimit)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        if (!layout.HasSlime)
            throw new ArgumentException("The slime bot requires a layout with slime.", nameof(layout));

        if (moveLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(moveLimit));

        BoardStateSnapshot board = _policy == Policy.Baseline ? layout.WithSlime(null) : layout;
        SlimeBotResult result = new SlimeBotResult { MaximumStickyCount = CountSticky(board) };

        while (result.MoveCount < moveLimit && !board.IsCleared)
        {
            if (SlimeRule.HasHopelessShelf(board))
            {
                result.FirstHopelessMove = result.MoveCount;
                break;
            }

            List<SlimeBotMove> moves = SlimeMoveFinder.FindMoves(board);

            if (_policy == Policy.Skilled)
                moves = KeepSafeMoves(board, moves);

            BoardMoveSimulation simulation = null;
            SlimeBotMove chosenMove = null;

            while (moves.Count > 0 && simulation == null)
            {
                SlimeBotMove move = ChooseMove(moves);
                moves.Remove(move);

                if (_simulator.TrySimulate(board, move.Source, move.Target, out BoardMoveSimulation candidate) && candidate.IsAllowed)
                {
                    simulation = candidate;
                    chosenMove = move;
                }
            }

            if (simulation == null)
            {
                result.IsDeadlocked = SlimeRule.IsStuck(board, _simulator);
                break;
            }

            result.MoveCount++;
            result.MatchingMoveCount += simulation.MatchCount > 0 ? 1 : 0;
            result.CleanedColumnCount += simulation.CleanedColumns.Count;
            result.StuckItemCount += chosenMove.SticksItem ? 1 : 0;

            if (simulation.GrownColumns.Count > 0)
            {
                result.GrowthCount += simulation.GrownColumns.Count;

                if (result.FirstGrowthMove < 0)
                    result.FirstGrowthMove = result.MoveCount;
            }

            board = simulation.State;
            result.MaximumStickyCount = Math.Max(result.MaximumStickyCount, CountSticky(board));
            result.StickyAfterMove.Add(CountSticky(board));
        }

        result.IsWon = board.IsCleared;
        result.StickyAtEnd = CountSticky(board);
        return result;
    }

    private static int CountSticky(BoardStateSnapshot board) => board.HasSlime ? board.Slime.StickyColumns.Count : 0;

    private List<SlimeBotMove> KeepSafeMoves(BoardStateSnapshot board, List<SlimeBotMove> moves)
    {
        List<SlimeBotMove> safeMoves = moves.Where(move => IsSafe(board, move)).ToList();
        return safeMoves.Count > 0 ? safeMoves : moves;
    }

    private bool IsSafe(BoardStateSnapshot board, SlimeBotMove move)
    {
        if (!_simulator.TrySimulate(board, move.Source, move.Target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            return false;

        if (SlimeRule.HasHopelessShelf(simulation.State))
            return false;

        if (!simulation.State.HasSlime)
            return true;

        SlimeSnapshot slime = simulation.State.Slime;
        BoardStateSnapshot imminent = simulation.State.WithSlime(slime.WithStickyColumns(slime.StickyColumns, 1));
        BoardStateSnapshot afterGrowth = SlimeRule.AdvanceGrowth(imminent, out _);
        return !SlimeRule.HasHopelessShelf(afterGrowth);
    }

    private SlimeBotMove ChooseMove(List<SlimeBotMove> moves)
    {
        if (_policy == Policy.Random)
            return moves[_random.Next(moves.Count)];

        if (_policy == Policy.Careful || _policy == Policy.Skilled)
        {
            SlimeBotMove[] preferred = moves.Where(move => move.IsCleaning).ToArray();

            if (preferred.Length == 0)
                preferred = moves.Where(move => move.IsEvacuation && !move.SticksItem).ToArray();

            if (preferred.Length > 0)
                return preferred[_random.Next(preferred.Length)];
        }

        SlimeBotMove[] matches = moves.Where(move => move.IsMatch).ToArray();

        if (matches.Length > 0)
            return matches[_random.Next(matches.Length)];

        SlimeBotMove[] setups = moves.Where(move => move.IsSetup && (_policy != Policy.Careful && _policy != Policy.Skilled || !move.SticksItem)).ToArray();

        if (setups.Length > 0)
            return setups[_random.Next(setups.Length)];

        SlimeBotMove[] safeMoves = moves.Where(move => _policy != Policy.Careful && _policy != Policy.Skilled || !move.SticksItem).ToArray();
        return safeMoves.Length > 0 ? safeMoves[_random.Next(safeMoves.Length)] : moves[_random.Next(moves.Count)];
    }
}

public sealed class SlimeBotResult
{
    public bool IsWon { get; set; }
    public bool IsDeadlocked { get; set; }
    public int FirstHopelessMove { get; set; } = -1;
    public List<int> StickyAfterMove { get; } = new List<int>();
    public int MoveCount { get; set; }
    public int MatchingMoveCount { get; set; }
    public int GrowthCount { get; set; }
    public int FirstGrowthMove { get; set; } = -1;
    public int CleanedColumnCount { get; set; }
    public int StuckItemCount { get; set; }
    public int MaximumStickyCount { get; set; }
    public int StickyAtEnd { get; set; }
}
