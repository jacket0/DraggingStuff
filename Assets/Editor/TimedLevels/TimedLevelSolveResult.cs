using System;

public sealed class TimedLevelSolveResult
{
    public TimedLevelSolveStatus Status { get; }
    public int MoveCount { get; }
    public int VisitedNodeCount { get; }
    public TimeSpan ElapsedTime { get; }

    public TimedLevelSolveResult(TimedLevelSolveStatus status, int moveCount, int visitedNodeCount, TimeSpan elapsedTime)
    {
        Status = status;
        MoveCount = moveCount;
        VisitedNodeCount = visitedNodeCount;
        ElapsedTime = elapsedTime;
    }
}
