using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class EndlessBoardSolver
{
    public bool CanReachMatches(
        BoardSnapshot snapshot,
        int requiredMatchCount,
        int maximumMoveCount,
        int nodeLimit,
        int maximumMovesPerState,
        int timeLimitMilliseconds)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        if (requiredMatchCount <= 0)
            return true;

        if (maximumMoveCount <= 0 || nodeLimit <= 0 || maximumMovesPerState <= 0 || timeLimitMilliseconds <= 0)
            return false;

        Stopwatch stopwatch = Stopwatch.StartNew();
        SolverState initialState = SolverState.Create(snapshot);
        int initialMatches = initialState.ResolveAutomaticMatches();

        if (initialMatches >= requiredMatchCount)
            return true;

        Queue<SearchNode> nodes = new Queue<SearchNode>();
        Dictionary<string, int> bestMatchCounts = new Dictionary<string, int>();

        nodes.Enqueue(new SearchNode(initialState, 0, initialMatches));
        bestMatchCounts[initialState.CreateKey()] = initialMatches;

        int visitedNodeCount = 0;

        while (nodes.Count > 0 && visitedNodeCount < nodeLimit && stopwatch.ElapsedMilliseconds < timeLimitMilliseconds)
        {
            SearchNode node = nodes.Dequeue();
            visitedNodeCount++;

            if (node.MoveCount >= maximumMoveCount)
                continue;

            IReadOnlyList<Move> moves = node.State.GetMeaningfulMoves(maximumMovesPerState);

            foreach (Move move in moves)
            {
                if (stopwatch.ElapsedMilliseconds >= timeLimitMilliseconds)
                    return false;

                SolverState nextState = node.State.Copy();
                nextState.Apply(move);

                int totalMatches = node.MatchCount + nextState.ResolveAutomaticMatches();

                if (totalMatches >= requiredMatchCount)
                    return true;

                string stateKey = nextState.CreateKey();

                if (bestMatchCounts.TryGetValue(stateKey, out int bestMatchCount) && bestMatchCount >= totalMatches)
                    continue;

                bestMatchCounts[stateKey] = totalMatches;
                nodes.Enqueue(new SearchNode(nextState, node.MoveCount + 1, totalMatches));
            }
        }

        return false;
    }

    public int CountImmediateMatchMoves(BoardSnapshot snapshot)
    {
        return CountImmediateMatchMoves(snapshot, null);
    }

    public int CountImmediateMatchMoves(BoardSnapshot snapshot, int targetCapacity)
    {
        if (!Shelf.IsValidCapacity(targetCapacity))
            throw new ArgumentOutOfRangeException(nameof(targetCapacity));

        return CountImmediateMatchMoves(snapshot, (int?)targetCapacity);
    }

    private int CountImmediateMatchMoves(BoardSnapshot snapshot, int? targetCapacity)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        SolverState state = SolverState.Create(snapshot);
        state.ResolveAutomaticMatches();
        return state.CountImmediateMatchMoves(targetCapacity);
    }

    private readonly struct SearchNode
    {
        public SolverState State { get; }
        public int MoveCount { get; }
        public int MatchCount { get; }

        public SearchNode(SolverState state, int moveCount, int matchCount)
        {
            State = state;
            MoveCount = moveCount;
            MatchCount = matchCount;
        }
    }

    private readonly struct Move
    {
        public int SourceShelf { get; }
        public int SourceSlot { get; }
        public int TargetShelf { get; }
        public int TargetSlot { get; }

        public Move(int sourceShelf, int sourceSlot, int targetShelf, int targetSlot)
        {
            SourceShelf = sourceShelf;
            SourceSlot = sourceSlot;
            TargetShelf = targetShelf;
            TargetSlot = targetSlot;
        }
    }

    private readonly struct CandidateMove
    {
        public Move Move { get; }
        public int Priority { get; }
        public int MatchingItemCount { get; }

        public CandidateMove(Move move, int priority, int matchingItemCount)
        {
            Move = move;
            Priority = priority;
            MatchingItemCount = matchingItemCount;
        }
    }

    private sealed class SolverState
    {
        private readonly List<ItemType?[]>[] _shelves;

        private SolverState(List<ItemType?[]>[] shelves)
        {
            _shelves = shelves;
        }

        public static SolverState Create(BoardSnapshot snapshot)
        {
            List<ItemType?[]>[] shelves = new List<ItemType?[]>[snapshot.Shelves.Count];

            for (int shelfIndex = 0; shelfIndex < snapshot.Shelves.Count; shelfIndex++)
            {
                shelves[shelfIndex] = new List<ItemType?[]>();

                foreach (BoardLayerSnapshot layer in snapshot.Shelves[shelfIndex].Layers)
                {
                    ItemType?[] items = new ItemType?[layer.Items.Count];

                    for (int slotIndex = 0; slotIndex < items.Length; slotIndex++)
                        items[slotIndex] = layer.Items[slotIndex];

                    shelves[shelfIndex].Add(items);
                }
            }

            return new SolverState(shelves);
        }

        public SolverState Copy()
        {
            List<ItemType?[]>[] shelves = new List<ItemType?[]>[_shelves.Length];

            for (int shelfIndex = 0; shelfIndex < _shelves.Length; shelfIndex++)
            {
                shelves[shelfIndex] = new List<ItemType?[]>(_shelves[shelfIndex].Count);

                foreach (ItemType?[] layer in _shelves[shelfIndex])
                    shelves[shelfIndex].Add((ItemType?[])layer.Clone());
            }

            return new SolverState(shelves);
        }

        public int CountImmediateMatchMoves(int? targetCapacity)
        {
            int moveCount = 0;

            for (int targetShelf = 0; targetShelf < _shelves.Length; targetShelf++)
            {
                if (!TryGetActiveLayer(targetShelf, out ItemType?[] targetLayer))
                    continue;

                if (targetCapacity.HasValue && targetLayer.Length != targetCapacity.Value)
                    continue;

                if (FindEmptySlot(targetLayer) < 0 || !TryGetCompletableType(targetLayer, out ItemType matchType))
                    continue;

                for (int sourceShelf = 0; sourceShelf < _shelves.Length; sourceShelf++)
                {
                    if (sourceShelf == targetShelf || !TryGetActiveLayer(sourceShelf, out ItemType?[] sourceLayer))
                        continue;

                    int sourceSlot = FindItemSlot(sourceLayer, matchType);

                    if (sourceSlot >= 0 && IsMoveAllowed(new Move(sourceShelf, sourceSlot, targetShelf, FindEmptySlot(targetLayer))))
                        moveCount++;
                }
            }

            return moveCount;
        }

        public IReadOnlyList<Move> GetMeaningfulMoves(int maximumMoveCount)
        {
            List<CandidateMove> candidates = new List<CandidateMove>();

            for (int sourceShelf = 0; sourceShelf < _shelves.Length; sourceShelf++)
            {
                if (!TryGetActiveLayer(sourceShelf, out ItemType?[] sourceLayer))
                    continue;

                int sourceItemCount = CountItems(sourceLayer);
                HashSet<ItemType> sourceTypes = new HashSet<ItemType>();

                for (int sourceSlot = 0; sourceSlot < sourceLayer.Length; sourceSlot++)
                {
                    if (!sourceLayer[sourceSlot].HasValue || !sourceTypes.Add(sourceLayer[sourceSlot].Value))
                        continue;

                    ItemType sourceType = sourceLayer[sourceSlot].Value;

                    for (int targetShelf = 0; targetShelf < _shelves.Length; targetShelf++)
                    {
                        if (targetShelf == sourceShelf || !TryGetActiveLayer(targetShelf, out ItemType?[] targetLayer))
                            continue;

                        int targetSlot = FindEmptySlot(targetLayer);

                        if (targetSlot < 0)
                            continue;

                        int matchingItemCount = CountItems(targetLayer, sourceType);

                        bool createsMatch = WouldCreateMatch(targetLayer, sourceType);
                        int priority = GetMovePriority(
                            createsMatch,
                            matchingItemCount,
                            sourceItemCount,
                            targetLayer.Length);
                        Move move = new Move(sourceShelf, sourceSlot, targetShelf, targetSlot);

                        if (!IsMoveAllowed(move))
                            continue;

                        candidates.Add(new CandidateMove(move, priority, matchingItemCount));
                    }
                }
            }

            candidates.Sort(CompareCandidateMoves);

            int resultCount = Math.Min(maximumMoveCount, candidates.Count);
            List<Move> moves = new List<Move>(resultCount);

            for (int index = 0; index < resultCount; index++)
                moves.Add(candidates[index].Move);

            return moves;
        }

        public void Apply(Move move)
        {
            ItemType?[] sourceLayer = _shelves[move.SourceShelf][0];
            ItemType?[] targetLayer = _shelves[move.TargetShelf][0];

            targetLayer[move.TargetSlot] = sourceLayer[move.SourceSlot];
            sourceLayer[move.SourceSlot] = null;
        }

        public int ResolveAutomaticMatches()
        {
            int matchCount = 0;
            bool changed;

            do
            {
                changed = AdvanceEmptyLayers();

                for (int shelfIndex = 0; shelfIndex < _shelves.Length; shelfIndex++)
                {
                    if (!TryGetActiveLayer(shelfIndex, out ItemType?[] layer) || !HasMatch(layer))
                        continue;

                    Array.Clear(layer, 0, layer.Length);
                    matchCount++;
                    changed = true;
                }
            }
            while (changed);

            return matchCount;
        }

        public string CreateKey()
        {
            StringBuilder key = new StringBuilder();

            foreach (List<ItemType?[]> shelf in _shelves)
            {
                key.Append('|');

                foreach (ItemType?[] layer in shelf)
                {
                    key.Append('/');

                    int[] normalizedItems = layer
                        .Select(item => item.HasValue ? (int)item.Value + 1 : 0)
                        .OrderBy(value => value)
                        .ToArray();

                    foreach (int item in normalizedItems)
                        key.Append(item).Append(',');
                }
            }

            return key.ToString();
        }

        private static int CompareCandidateMoves(CandidateMove first, CandidateMove second)
        {
            int priorityComparison = first.Priority.CompareTo(second.Priority);

            if (priorityComparison != 0)
                return priorityComparison;

            return second.MatchingItemCount.CompareTo(first.MatchingItemCount);
        }

        private static int GetMovePriority(
            bool createsMatch,
            int matchingItemCount,
            int sourceItemCount,
            int targetCapacity)
        {
            if (createsMatch)
                return 0;

            if (matchingItemCount > 0)
                return 1;

            if (sourceItemCount == 1)
                return 2;

            return targetCapacity < Shelf.MinimumMatchCapacity ? 3 : 4;
        }

        private bool AdvanceEmptyLayers()
        {
            bool changed = false;

            foreach (List<ItemType?[]> shelf in _shelves)
            {
                while (shelf.Count > 1 && IsEmpty(shelf[0]))
                {
                    shelf.RemoveAt(0);
                    changed = true;
                }
            }

            return changed;
        }

        private bool TryGetActiveLayer(int shelfIndex, out ItemType?[] layer)
        {
            if (_shelves[shelfIndex].Count == 0)
            {
                layer = null;
                return false;
            }

            layer = _shelves[shelfIndex][0];
            return true;
        }

        private bool IsMoveAllowed(Move move)
        {
            SolverState projectedState = Copy();
            projectedState.Apply(move);
            int matchCount = projectedState.ResolveAutomaticMatches();
            return matchCount > 0 || projectedState.CountActiveEmptySlots() > 0;
        }

        private int CountActiveEmptySlots()
        {
            int emptySlotCount = 0;

            foreach (List<ItemType?[]> shelf in _shelves)
            {
                if (shelf.Count == 0)
                    continue;

                foreach (ItemType? item in shelf[0])
                {
                    if (!item.HasValue)
                        emptySlotCount++;
                }
            }

            return emptySlotCount;
        }

        private static bool TryGetCompletableType(ItemType?[] layer, out ItemType matchType)
        {
            matchType = default;

            if (layer.Length < Shelf.MinimumMatchCapacity || layer.Count(item => !item.HasValue) != 1)
                return false;

            ItemType? firstItem = null;

            foreach (ItemType? item in layer)
            {
                if (!item.HasValue)
                    continue;

                if (firstItem.HasValue && firstItem.Value != item.Value)
                    return false;

                firstItem = item.Value;
            }

            if (!firstItem.HasValue)
                return false;

            matchType = firstItem.Value;
            return true;
        }

        private static int FindEmptySlot(ItemType?[] layer)
        {
            for (int slotIndex = 0; slotIndex < layer.Length; slotIndex++)
            {
                if (!layer[slotIndex].HasValue)
                    return slotIndex;
            }

            return -1;
        }

        private static int FindItemSlot(ItemType?[] layer, ItemType type)
        {
            for (int slotIndex = 0; slotIndex < layer.Length; slotIndex++)
            {
                if (layer[slotIndex] == type)
                    return slotIndex;
            }

            return -1;
        }

        private static int CountItems(ItemType?[] layer)
        {
            int count = 0;

            foreach (ItemType? item in layer)
            {
                if (item.HasValue)
                    count++;
            }

            return count;
        }

        private static int CountItems(ItemType?[] layer, ItemType type)
        {
            int count = 0;

            foreach (ItemType? item in layer)
            {
                if (item == type)
                    count++;
            }

            return count;
        }

        private static bool HasMatch(ItemType?[] layer)
        {
            if (layer.Length < Shelf.MinimumMatchCapacity || !layer[0].HasValue)
                return false;

            ItemType type = layer[0].Value;

            foreach (ItemType? item in layer)
            {
                if (!item.HasValue || item.Value != type)
                    return false;
            }

            return true;
        }

        private static bool WouldCreateMatch(ItemType?[] targetLayer, ItemType sourceType)
        {
            if (targetLayer.Length < Shelf.MinimumMatchCapacity)
                return false;

            int emptySlotCount = 0;

            foreach (ItemType? item in targetLayer)
            {
                if (!item.HasValue)
                {
                    emptySlotCount++;
                    continue;
                }

                if (item.Value != sourceType)
                    return false;
            }

            return emptySlotCount == 1;
        }

        private static bool IsEmpty(ItemType?[] layer)
        {
            foreach (ItemType? item in layer)
            {
                if (item.HasValue)
                    return false;
            }

            return true;
        }
    }
}
