using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CyclicShelfDeadlockProbe
{
    public const int MaximumSimulationCount = 400000;

    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();
    private int _simulationCount;

    public enum ProbeStatus
    {
        NoDeadlock,
        Deadlock,
        Unknown
    }

    public ProbeStatus Check(BoardStateSnapshot layout, int maximumMatchCount, out int deadlockMatchCount)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        if (maximumMatchCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMatchCount));

        deadlockMatchCount = 0;
        _simulationCount = 0;
        List<BoardStateSnapshot> frontier = new List<BoardStateSnapshot> { layout };
        HashSet<string> visitedStates = new HashSet<string> { BoardStateFingerprint.CreateKey(layout) };

        for (int matchCount = 1; matchCount <= maximumMatchCount && frontier.Count > 0; matchCount++)
        {
            List<BoardStateSnapshot> nextFrontier = new List<BoardStateSnapshot>();

            foreach (BoardStateSnapshot state in frontier)
            {
                foreach (CyclicShelfBotMove move in CyclicShelfMoveFinder.FindMoves(state).Where(move => move.IsMatch))
                {
                    if (++_simulationCount > MaximumSimulationCount)
                        return ProbeStatus.Unknown;

                    if (!_simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
                        continue;

                    if (CycleDeadlockRule.IsStuck(simulation.State))
                    {
                        deadlockMatchCount = matchCount;
                        return ProbeStatus.Deadlock;
                    }

                    if (visitedStates.Add(BoardStateFingerprint.CreateKey(simulation.State)))
                        nextFrontier.Add(simulation.State);
                }
            }

            frontier = nextFrontier;
        }

        return ProbeStatus.NoDeadlock;
    }
}
