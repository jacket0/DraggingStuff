using System;
using System.Collections.Generic;

public sealed class TimedLevelSolveResult
{
    public TimedLevelSolveStatus Status { get; }
    public int MoveCount { get; }
    public int VisitedNodeCount { get; }
    public TimeSpan ElapsedTime { get; }
    public IReadOnlyList<TimedLevelMove> Moves { get; }

    public TimedLevelSolveResult(TimedLevelSolveStatus status, int moveCount, int visitedNodeCount, TimeSpan elapsedTime, IReadOnlyList<TimedLevelMove> moves = null)
    {
        Status = status;
        MoveCount = moveCount;
        VisitedNodeCount = visitedNodeCount;
        ElapsedTime = elapsedTime;
        Moves = moves ?? Array.Empty<TimedLevelMove>();
    }
}
