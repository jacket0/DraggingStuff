using System;
using System.Collections.Generic;

public sealed class SlimeLayoutMetrics
{
    public bool IsReplayed { get; }
    public bool HasDeadlockOnPath { get; }
    public int CleanedColumnCount { get; }
    public int GrowthCount { get; }
    public int MaximumStickyColumnCount { get; }
    public int StickyColumnsAtEnd { get; }

    private SlimeLayoutMetrics(bool isReplayed, bool hasDeadlockOnPath, int cleanedColumnCount, int growthCount, int maximumStickyColumnCount, int stickyColumnsAtEnd)
    {
        IsReplayed = isReplayed;
        HasDeadlockOnPath = hasDeadlockOnPath;
        CleanedColumnCount = cleanedColumnCount;
        GrowthCount = growthCount;
        MaximumStickyColumnCount = maximumStickyColumnCount;
        StickyColumnsAtEnd = stickyColumnsAtEnd;
    }

    public static SlimeLayoutMetrics Measure(BoardStateSnapshot layout, IReadOnlyList<TimedLevelMove> solution)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        if (solution == null)
            throw new ArgumentNullException(nameof(solution));

        if (!layout.HasSlime)
            throw new ArgumentException("Slime metrics require a layout with slime.", nameof(layout));

        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = layout;
        bool hasDeadlockOnPath = false;
        int cleanedColumnCount = 0;
        int growthCount = 0;
        int maximumStickyColumnCount = layout.Slime.StickyColumns.Count;

        foreach (TimedLevelMove move in solution)
        {
            hasDeadlockOnPath |= SlimeRule.IsStuck(state, simulator);

            if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
                return new SlimeLayoutMetrics(false, hasDeadlockOnPath, cleanedColumnCount, growthCount, maximumStickyColumnCount, state.Slime.StickyColumns.Count);

            cleanedColumnCount += simulation.CleanedColumns.Count;
            growthCount += simulation.GrownColumns.Count;
            state = simulation.State;
            maximumStickyColumnCount = Math.Max(maximumStickyColumnCount, state.Slime.StickyColumns.Count);
        }

        return new SlimeLayoutMetrics(state.IsCleared, hasDeadlockOnPath, cleanedColumnCount, growthCount, maximumStickyColumnCount, state.Slime.StickyColumns.Count);
    }
}
