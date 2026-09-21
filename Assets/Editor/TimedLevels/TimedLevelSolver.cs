using System;
using System.Collections.Generic;
using System.Diagnostics;

public sealed class TimedLevelSolver
{
    private readonly BoardMoveSimulator _moveSimulator = new BoardMoveSimulator();
    private readonly Dictionary<string, int> _visitedDepths = new Dictionary<string, int>();
    private readonly int _maximumDepth;
    private readonly int _maximumNodeCount;
    private readonly TimeSpan _timeLimit;

    private Stopwatch _stopwatch;
    private int _visitedNodeCount;
    private bool _limitReached;

    public TimedLevelSolver(int maximumDepth, int maximumNodeCount, TimeSpan timeLimit)
    {
        if (maximumDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));

        if (maximumNodeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumNodeCount));

        if (timeLimit <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeLimit));

        _maximumDepth = maximumDepth;
        _maximumNodeCount = maximumNodeCount;
        _timeLimit = timeLimit;
    }

    public TimedLevelSolveResult Solve(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        _visitedDepths.Clear();
        _visitedNodeCount = 0;
        _limitReached = false;
        _stopwatch = Stopwatch.StartNew();
        int moveCount = Search(board, 0);
        _stopwatch.Stop();

        TimedLevelSolveStatus status = moveCount >= 0
            ? TimedLevelSolveStatus.Solved
            : _limitReached ? TimedLevelSolveStatus.LimitReached : TimedLevelSolveStatus.Unsolvable;

        return new TimedLevelSolveResult(status, Math.Max(0, moveCount), _visitedNodeCount, _stopwatch.Elapsed);
    }

    public TimedLevelSolveResult Solve(BoardStateSnapshot board, IReadOnlyList<TimedLevelMove> solutionMoves)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (solutionMoves == null)
            throw new ArgumentNullException(nameof(solutionMoves));

        Stopwatch stopwatch = Stopwatch.StartNew();
        BoardStateSnapshot state = board;
        int visitedNodeCount = 1;

        foreach (TimedLevelMove move in solutionMoves)
        {
            if (visitedNodeCount >= _maximumNodeCount || stopwatch.Elapsed >= _timeLimit)
            {
                stopwatch.Stop();
                return new TimedLevelSolveResult(TimedLevelSolveStatus.LimitReached, 0, visitedNodeCount, stopwatch.Elapsed);
            }

            if (!_moveSimulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                || !simulation.IsAllowed)
            {
                stopwatch.Stop();
                return new TimedLevelSolveResult(TimedLevelSolveStatus.Unsolvable, 0, visitedNodeCount, stopwatch.Elapsed);
            }

            state = simulation.State;
            visitedNodeCount++;
        }

        stopwatch.Stop();
        TimedLevelSolveStatus status = state.IsCleared ? TimedLevelSolveStatus.Solved : TimedLevelSolveStatus.Unsolvable;
        int moveCount = state.IsCleared ? solutionMoves.Count : 0;
        return new TimedLevelSolveResult(status, moveCount, visitedNodeCount, stopwatch.Elapsed);
    }

    private int Search(BoardStateSnapshot board, int depth)
    {
        if (board.IsCleared)
            return depth;

        if (depth >= _maximumDepth || ReachedLimit())
            return -1;

        string key = BoardStateFingerprint.CreateKey(board);

        if (_visitedDepths.TryGetValue(key, out int previousDepth) && previousDepth <= depth)
            return -1;

        _visitedDepths[key] = depth;
        _visitedNodeCount++;
        List<BoardMoveSimulation> moves = CreateMoves(board);

        foreach (BoardMoveSimulation move in moves)
        {
            int result = Search(move.State, depth + 1);

            if (result >= 0)
                return result;

            if (_limitReached)
                return -1;
        }

        return -1;
    }

    private List<BoardMoveSimulation> CreateMoves(BoardStateSnapshot board)
    {
        List<BoardMoveSimulation> matchingMoves = new List<BoardMoveSimulation>();
        HashSet<string> resultingStates = new HashSet<string>();

        for (int sourceShelfIndex = 0; sourceShelfIndex < board.Shelves.Count; sourceShelfIndex++)
        {
            ShelfStateSnapshot sourceShelf = board.Shelves[sourceShelfIndex];

            for (int sourceColumnIndex = 0; sourceColumnIndex < sourceShelf.Capacity; sourceColumnIndex++)
            {
                if (sourceShelf.Columns[sourceColumnIndex].IsEmpty)
                    continue;

                ColumnPosition source = new ColumnPosition(sourceShelfIndex, sourceColumnIndex);

                for (int targetShelfIndex = 0; targetShelfIndex < board.Shelves.Count; targetShelfIndex++)
                {
                    ShelfStateSnapshot targetShelf = board.Shelves[targetShelfIndex];

                    for (int targetColumnIndex = 0; targetColumnIndex < targetShelf.Capacity; targetColumnIndex++)
                    {
                        if (!targetShelf.Columns[targetColumnIndex].IsEmpty)
                            continue;

                        ColumnPosition target = new ColumnPosition(targetShelfIndex, targetColumnIndex);

                        if (!_moveSimulator.TrySimulate(board, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
                            continue;

                        if (!resultingStates.Add(BoardStateFingerprint.CreateKey(simulation.State)))
                            continue;

                        if (simulation.MatchCount > 0)
                            matchingMoves.Add(simulation);
                    }
                }
            }
        }

        matchingMoves.Sort(CompareMoves);
        return matchingMoves;
    }

    private bool ReachedLimit()
    {
        if (_visitedNodeCount < _maximumNodeCount && _stopwatch.Elapsed < _timeLimit)
            return false;

        _limitReached = true;
        return true;
    }

    private static int CompareMoves(BoardMoveSimulation first, BoardMoveSimulation second)
    {
        int matchComparison = second.MatchCount.CompareTo(first.MatchCount);

        if (matchComparison != 0)
            return matchComparison;

        int itemComparison = first.State.ItemCount.CompareTo(second.State.ItemCount);

        if (itemComparison != 0)
            return itemComparison;

        return string.CompareOrdinal(BoardStateFingerprint.CreateKey(first.State), BoardStateFingerprint.CreateKey(second.State));
    }
}
