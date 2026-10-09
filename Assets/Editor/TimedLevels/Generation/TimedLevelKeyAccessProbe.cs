using System;
using System.Collections.Generic;

public sealed class TimedLevelKeyAccessProbe
{
    public const int MaximumSimulationCount = 400000;

    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();
    private int _simulationCount;

    public enum AccessStatus
    {
        NoEarlyAccess,
        EarlyAccess,
        Unknown
    }

    public AccessStatus Check(BoardStateSnapshot layout, int minimumMovesToFirstKey)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        int depthLimit = minimumMovesToFirstKey - 1;
        _simulationCount = 0;

        if (depthLimit <= 0 || !layout.HasKeys)
            return AccessStatus.NoEarlyAccess;

        List<BoardStateSnapshot> frontier = new List<BoardStateSnapshot> { layout };
        HashSet<string> visitedStates = new HashSet<string> { BoardStateFingerprint.CreateKey(layout) };

        for (int depth = 1; depth <= depthLimit; depth++)
        {
            bool isLastDepth = depth == depthLimit;
            List<BoardStateSnapshot> nextFrontier = new List<BoardStateSnapshot>();

            foreach (BoardStateSnapshot state in frontier)
            {
                AccessStatus status = ExpandState(state, isLastDepth, visitedStates, nextFrontier);

                if (status != AccessStatus.NoEarlyAccess)
                    return status;
            }

            frontier = nextFrontier;
        }

        return AccessStatus.NoEarlyAccess;
    }

    private AccessStatus ExpandState(BoardStateSnapshot state, bool isLastDepth, HashSet<string> visitedStates, List<BoardStateSnapshot> nextFrontier)
    {
        List<ColumnPosition> sources = new List<ColumnPosition>();
        List<ColumnPosition> emptyTargets = new List<ColumnPosition>();

        for (int shelfIndex = 0; shelfIndex < state.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = state.Shelves[shelfIndex];

            if (!shelf.IsOpen)
                continue;

            bool hasEmptyTarget = false;

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                ColumnStateSnapshot column = shelf.Columns[columnIndex];

                if (column.FrontItem != null)
                    sources.Add(new ColumnPosition(shelfIndex, columnIndex));
                else if (column.IsEmpty && !hasEmptyTarget)
                {
                    emptyTargets.Add(new ColumnPosition(shelfIndex, columnIndex));
                    hasEmptyTarget = true;
                }
            }
        }

        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            foreach (ColumnPosition target in emptyTargets)
            {
                AccessStatus status = TryMove(state, sources[sourceIndex], target, isLastDepth, visitedStates, nextFrontier);

                if (status != AccessStatus.NoEarlyAccess)
                    return status;
            }

            for (int targetIndex = sourceIndex + 1; targetIndex < sources.Count; targetIndex++)
            {
                AccessStatus status = TryMove(state, sources[sourceIndex], sources[targetIndex], isLastDepth, visitedStates, nextFrontier);

                if (status != AccessStatus.NoEarlyAccess)
                    return status;
            }
        }

        return AccessStatus.NoEarlyAccess;
    }

    private AccessStatus TryMove(
        BoardStateSnapshot state,
        ColumnPosition source,
        ColumnPosition target,
        bool isLastDepth,
        HashSet<string> visitedStates,
        List<BoardStateSnapshot> nextFrontier)
    {
        if (isLastDepth && !state.Shelves[source.ShelfIndex].HasKeys && !state.Shelves[target.ShelfIndex].HasKeys)
            return AccessStatus.NoEarlyAccess;

        if (++_simulationCount > MaximumSimulationCount)
            return AccessStatus.Unknown;

        if (!_simulator.TrySimulate(state, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            return AccessStatus.NoEarlyAccess;

        if (simulation.OpenedShelfCount > 0)
            return AccessStatus.EarlyAccess;

        if (!isLastDepth && visitedStates.Add(BoardStateFingerprint.CreateKey(simulation.State)))
            nextFrontier.Add(simulation.State);

        return AccessStatus.NoEarlyAccess;
    }
}
