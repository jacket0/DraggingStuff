using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CyclicShelfLayoutMetrics
{
    public bool IsReplayed { get; }
    public bool HasDeadlockOnPath { get; }
    public IReadOnlyList<int> ItemsAtClosings { get; }
    public int PreparationMoveCount { get; }
    public int SwitchCount { get; }

    private CyclicShelfLayoutMetrics(bool isReplayed, bool hasDeadlockOnPath, IReadOnlyList<int> itemsAtClosings, int preparationMoveCount, int switchCount)
    {
        IsReplayed = isReplayed;
        HasDeadlockOnPath = hasDeadlockOnPath;
        ItemsAtClosings = itemsAtClosings;
        PreparationMoveCount = preparationMoveCount;
        SwitchCount = switchCount;
    }

    public static CyclicShelfLayoutMetrics Measure(BoardStateSnapshot layout, IReadOnlyList<TimedLevelMove> solution)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        if (solution == null)
            throw new ArgumentNullException(nameof(solution));

        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = layout;
        List<int> itemsAtClosings = layout.Shelves.Where(shelf => shelf.IsCyclic && !shelf.IsOpen).Select(CountItems).ToList();
        IReadOnlyList<int> openedShelfIndexes = Array.Empty<int>();
        bool hasDeadlockOnPath = false;
        int preparationMoveCount = 0;
        int switchCount = 0;

        foreach (TimedLevelMove move in solution)
        {
            hasDeadlockOnPath |= CycleDeadlockRule.IsStuck(state);

            if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                || !simulation.IsAllowed
                || simulation.MatchCount != (simulation.IsSwap ? 0 : 1))
            {
                return new CyclicShelfLayoutMetrics(false, hasDeadlockOnPath, itemsAtClosings, preparationMoveCount, switchCount);
            }

            int[] closedShelfIndexes = simulation.SwitchedShelfIndexes.Where(shelfIndex => !simulation.State.Shelves[shelfIndex].IsOpen).ToArray();

            if (closedShelfIndexes.Contains(move.Source.ShelfIndex) || openedShelfIndexes.Contains(move.Target.ShelfIndex))
                preparationMoveCount++;

            itemsAtClosings.AddRange(closedShelfIndexes.Select(shelfIndex => CountItems(simulation.State.Shelves[shelfIndex])));
            openedShelfIndexes = simulation.SwitchedShelfIndexes.Where(shelfIndex => simulation.State.Shelves[shelfIndex].IsOpen).ToArray();
            switchCount += simulation.SwitchedShelfIndexes.Count;
            state = simulation.State;
        }

        return new CyclicShelfLayoutMetrics(state.IsCleared, hasDeadlockOnPath, itemsAtClosings, preparationMoveCount, switchCount);
    }

    private static int CountItems(ShelfStateSnapshot shelf) => shelf.Columns.Sum(column => column.Count);
}
