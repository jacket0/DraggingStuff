using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TimedLevelKeyPlacer
{
    private const int RandomSeedSalt = 0x4B45594C;

    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();

    public enum PlacementStatus
    {
        Placed,
        LockedShelfNotFilled,
        Failed
    }

    public PlacementStatus TryPlace(
        BoardStateSnapshot openLayout,
        IReadOnlyList<TimedLevelMove> solution,
        IReadOnlyList<int> lockedShelfIndices,
        int minimumKeyDepth,
        int seed,
        out BoardStateSnapshot keyedLayout)
    {
        if (openLayout == null)
            throw new ArgumentNullException(nameof(openLayout));

        if (solution == null)
            throw new ArgumentNullException(nameof(solution));

        if (lockedShelfIndices == null)
            throw new ArgumentNullException(nameof(lockedShelfIndices));

        if (minimumKeyDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumKeyDepth));

        keyedLayout = null;

        if (lockedShelfIndices.Any(shelfIndex => openLayout.Shelves[shelfIndex].Columns.Any(column => column.Count == 0)))
            return PlacementStatus.LockedShelfNotFilled;

        SolutionTrace trace = new SolutionTrace(_simulator, openLayout, solution);
        Random random = new Random(seed ^ RandomSeedSalt);
        Dictionary<ColumnPosition, ColumnKeySnapshot> keys = new Dictionary<ColumnPosition, ColumnKeySnapshot>();

        foreach (int lockedShelfIndex in lockedShelfIndices)
        {
            List<KeyCandidate> candidates = FindCandidates(openLayout, trace, lockedShelfIndices, keys, lockedShelfIndex, minimumKeyDepth);

            if (candidates.Count == 0)
                return PlacementStatus.Failed;

            KeyCandidate best = candidates[0];
            KeyCandidate[] bestCandidates = candidates.TakeWhile(candidate => candidate.RevealStep == best.RevealStep && candidate.ItemsInFront == best.ItemsInFront).ToArray();
            KeyCandidate chosen = bestCandidates[random.Next(bestCandidates.Length)];
            keys.Add(chosen.Position, new ColumnKeySnapshot(chosen.ItemsInFront, lockedShelfIndex));
        }

        BoardStateSnapshot layout = CreateKeyedLayout(openLayout, keys, lockedShelfIndices);

        if (!ReplaysLikeOpenLayout(layout, solution, trace, lockedShelfIndices))
            return PlacementStatus.Failed;

        keyedLayout = layout;
        return PlacementStatus.Placed;
    }

    private static List<KeyCandidate> FindCandidates(
        BoardStateSnapshot openLayout,
        SolutionTrace trace,
        IReadOnlyList<int> lockedShelfIndices,
        Dictionary<ColumnPosition, ColumnKeySnapshot> keys,
        int lockedShelfIndex,
        int minimumKeyDepth)
    {
        int latestRevealStep = trace.GetFirstTouchStep(lockedShelfIndex) - 1;
        List<KeyCandidate> candidates = new List<KeyCandidate>();

        for (int shelfIndex = 0; shelfIndex < openLayout.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = openLayout.Shelves[shelfIndex];

            if (lockedShelfIndices.Contains(shelfIndex) || shelf.IsConveyor || shelf.IsFiltered)
                continue;

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                ColumnPosition position = new ColumnPosition(shelfIndex, columnIndex);
                int itemCount = shelf.Columns[columnIndex].Count;

                if (keys.ContainsKey(position) || itemCount + 1 > TimedLevelLayoutRules.MaximumColumnDepth)
                    continue;

                for (int itemsInFront = minimumKeyDepth; itemsInFront <= itemCount; itemsInFront++)
                {
                    int revealStep = trace.GetRevealStep(position, itemsInFront);

                    if (revealStep >= 0 && revealStep <= latestRevealStep)
                        candidates.Add(new KeyCandidate(position, itemsInFront, revealStep));
                }
            }
        }

        candidates.Sort(CompareCandidates);
        return candidates;
    }

    private static int CompareCandidates(KeyCandidate first, KeyCandidate second)
    {
        int stepComparison = second.RevealStep.CompareTo(first.RevealStep);

        if (stepComparison != 0)
            return stepComparison;

        int depthComparison = second.ItemsInFront.CompareTo(first.ItemsInFront);

        if (depthComparison != 0)
            return depthComparison;

        int shelfComparison = first.Position.ShelfIndex.CompareTo(second.Position.ShelfIndex);
        return shelfComparison != 0 ? shelfComparison : first.Position.ColumnIndex.CompareTo(second.Position.ColumnIndex);
    }

    private static BoardStateSnapshot CreateKeyedLayout(
        BoardStateSnapshot openLayout,
        Dictionary<ColumnPosition, ColumnKeySnapshot> keys,
        IReadOnlyList<int> lockedShelfIndices)
    {
        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[openLayout.Shelves.Count];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            ShelfStateSnapshot shelf = openLayout.Shelves[shelfIndex];
            ColumnStateSnapshot[] columns = new ColumnStateSnapshot[shelf.Capacity];

            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                ColumnStateSnapshot column = shelf.Columns[columnIndex];
                columns[columnIndex] = keys.TryGetValue(new ColumnPosition(shelfIndex, columnIndex), out ColumnKeySnapshot key)
                    ? new ColumnStateSnapshot(column.Items, new[] { key })
                    : column;
            }

            shelves[shelfIndex] = shelf.WithColumns(columns).WithOpenState(!lockedShelfIndices.Contains(shelfIndex));
        }

        return new BoardStateSnapshot(shelves, openLayout.FilteredTypes);
    }

    private bool ReplaysLikeOpenLayout(
        BoardStateSnapshot layout,
        IReadOnlyList<TimedLevelMove> solution,
        SolutionTrace trace,
        IReadOnlyList<int> lockedShelfIndices)
    {
        BoardStateSnapshot state = layout;

        for (int step = 0; step < solution.Count; step++)
        {
            if (lockedShelfIndices.Any(shelfIndex => trace.GetFirstTouchStep(shelfIndex) == step && !state.Shelves[shelfIndex].IsOpen))
                return false;

            if (!_simulator.TrySimulate(state, solution[step].Source, solution[step].Target, out BoardMoveSimulation simulation)
                || !simulation.IsAllowed
                || simulation.MatchCount != trace.GetMatchCount(step))
            {
                return false;
            }

            state = simulation.State;
        }

        return state.IsCleared && !state.HasKeys;
    }

    private sealed class SolutionTrace
    {
        private readonly Dictionary<ColumnPosition, List<int>> _removalSteps = new Dictionary<ColumnPosition, List<int>>();
        private readonly Dictionary<int, int> _firstTouchSteps = new Dictionary<int, int>();
        private readonly List<int> _matchCounts = new List<int>();
        private readonly int _stepCount;

        public SolutionTrace(BoardMoveSimulator simulator, BoardStateSnapshot openLayout, IReadOnlyList<TimedLevelMove> solution)
        {
            BoardStateSnapshot state = openLayout;
            _stepCount = solution.Count;

            for (int step = 0; step < solution.Count; step++)
            {
                TimedLevelMove move = solution[step];

                if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
                    throw new InvalidOperationException($"The generated solution does not replay at step {step + 1}.");

                RecordRemovals(state, simulation, move, step);

                foreach (int shelfIndex in simulation.AffectedShelfIndexes)
                {
                    if (!_firstTouchSteps.ContainsKey(shelfIndex))
                        _firstTouchSteps.Add(shelfIndex, step);
                }

                _matchCounts.Add(simulation.MatchCount);
                state = simulation.State;
            }
        }

        public int GetFirstTouchStep(int shelfIndex) => _firstTouchSteps.TryGetValue(shelfIndex, out int step) ? step : _stepCount;

        public int GetRevealStep(ColumnPosition position, int itemsInFront)
        {
            return _removalSteps.TryGetValue(position, out List<int> steps) && steps.Count >= itemsInFront ? steps[itemsInFront - 1] : -1;
        }

        public int GetMatchCount(int step) => _matchCounts[step];

        private void RecordRemovals(BoardStateSnapshot before, BoardMoveSimulation simulation, TimedLevelMove move, int step)
        {
            for (int shelfIndex = 0; shelfIndex < before.Shelves.Count; shelfIndex++)
            {
                for (int columnIndex = 0; columnIndex < before.Shelves[shelfIndex].Capacity; columnIndex++)
                {
                    ColumnPosition position = new ColumnPosition(shelfIndex, columnIndex);
                    int placedCount = !simulation.IsSwap && position.Equals(move.Target) ? 1 : 0;
                    int removedCount = before.Shelves[shelfIndex].Columns[columnIndex].Count
                        - simulation.State.Shelves[shelfIndex].Columns[columnIndex].Count
                        + placedCount;

                    if (removedCount <= 0)
                        continue;

                    if (!_removalSteps.TryGetValue(position, out List<int> steps))
                    {
                        steps = new List<int>();
                        _removalSteps.Add(position, steps);
                    }

                    for (int removal = 0; removal < removedCount; removal++)
                        steps.Add(step);
                }
            }
        }
    }

    private readonly struct KeyCandidate
    {
        public ColumnPosition Position { get; }
        public int ItemsInFront { get; }
        public int RevealStep { get; }

        public KeyCandidate(ColumnPosition position, int itemsInFront, int revealStep)
        {
            Position = position;
            ItemsInFront = itemsInFront;
            RevealStep = revealStep;
        }
    }
}
