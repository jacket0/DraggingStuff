using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SlimeBotSimulation
{
    public enum Policy
    {
        Greedy,
        Careful,
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

        BoardStateSnapshot board = _policy == Policy.Baseline ? layout.WithSlime(layout.Slime.WithStickyColumns(Array.Empty<ColumnPosition>(), layout.Slime.GrowthPeriod)) : layout;
        SlimeBotResult result = new SlimeBotResult { MaximumStickyCount = board.Slime.StickyColumns.Count };

        while (result.MoveCount < moveLimit && !board.IsCleared)
        {
            List<SlimeBotMove> moves = SlimeMoveFinder.FindMoves(board);
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
                if (!board.Slime.HasStickyColumns)
                {
                    result.HasNoMoves = true;
                    break;
                }

                board = SlimeRule.ApplyRescue(board, out _);
                result.RescueCount++;
                continue;
            }

            result.MoveCount++;
            result.MatchingMoveCount += simulation.MatchCount > 0 ? 1 : 0;
            result.CleanedColumnCount += simulation.CleanedColumns.Count;
            result.StuckItemCount += chosenMove.SticksItem ? 1 : 0;

            if (simulation.GrownColumn.HasValue)
            {
                result.GrowthCount++;

                if (result.FirstGrowthMove < 0)
                    result.FirstGrowthMove = result.MoveCount;
            }

            board = simulation.State;
            result.MaximumStickyCount = Math.Max(result.MaximumStickyCount, board.Slime.StickyColumns.Count);
        }

        result.IsWon = board.IsCleared;
        result.StickyAtEnd = board.Slime.StickyColumns.Count;
        return result;
    }

    private SlimeBotMove ChooseMove(List<SlimeBotMove> moves)
    {
        if (_policy == Policy.Random)
            return moves[_random.Next(moves.Count)];

        if (_policy == Policy.Careful)
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

        SlimeBotMove[] setups = moves.Where(move => move.IsSetup && (_policy != Policy.Careful || !move.SticksItem)).ToArray();

        if (setups.Length > 0)
            return setups[_random.Next(setups.Length)];

        SlimeBotMove[] safeMoves = moves.Where(move => _policy != Policy.Careful || !move.SticksItem).ToArray();
        return safeMoves.Length > 0 ? safeMoves[_random.Next(safeMoves.Length)] : moves[_random.Next(moves.Count)];
    }
}

public sealed class SlimeBotResult
{
    public bool IsWon { get; set; }
    public bool HasNoMoves { get; set; }
    public int MoveCount { get; set; }
    public int MatchingMoveCount { get; set; }
    public int GrowthCount { get; set; }
    public int FirstGrowthMove { get; set; } = -1;
    public int CleanedColumnCount { get; set; }
    public int RescueCount { get; set; }
    public int StuckItemCount { get; set; }
    public int MaximumStickyCount { get; set; }
    public int StickyAtEnd { get; set; }
}
