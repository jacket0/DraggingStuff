using System.Collections.Generic;
using UnityEngine;

public sealed class MoveSuggestionProvider : MonoBehaviour, IMoveSuggestionProvider
{
    private const int MatchPriority = 4;
    private const int GroupPriority = 3;
    private const int LayerReleasePriority = 2;
    private const int PreparatoryMovePriority = 1;

    [SerializeField] private ShelfBoard _shelfBoard;

    public bool TryGetSuggestion(out MoveSuggestion suggestion)
    {
        suggestion = null;
        int bestPriority = 0;
        int bestGroupSize = 0;
        bool bestMoveBreaksPair = true;

        foreach (Shelf sourceShelf in _shelfBoard.Shelves)
        {
            if (!sourceShelf.HasActiveLayer)
                continue;

            foreach (ShelfSlot sourceSlot in sourceShelf.ActiveLayer.Slots)
            {
                if (sourceSlot.IsEmpty)
                    continue;

                bool sourceMoveBreaksPair = WouldBreakSourcePair(sourceSlot, sourceShelf.ActiveLayer);

                foreach (Shelf targetShelf in _shelfBoard.Shelves)
                {
                    if (!targetShelf.HasActiveLayer || targetShelf == sourceShelf)
                        continue;

                    foreach (ShelfSlot targetSlot in targetShelf.ActiveLayer.Slots)
                    {
                        if (!_shelfBoard.CanMove(sourceSlot, targetSlot))
                            continue;

                        List<ShelfItem> targetMatchingItems = GetTargetMatchingItems(targetShelf.ActiveLayer, sourceSlot.Item.Type);
                        bool createsMatch = CreatesMatch(targetShelf.ActiveLayer, sourceSlot.Item.Type);
                        int priority = GetSuggestionPriority(
                            createsMatch,
                            targetMatchingItems.Count,
                            WouldReleaseLayer(sourceShelf.ActiveLayer, sourceShelf.HasNextLayer));
                        int groupSize = createsMatch ? targetShelf.Capacity : targetMatchingItems.Count + 1;

                        if (!IsBetterSuggestion(priority, groupSize, sourceMoveBreaksPair, bestPriority, bestGroupSize, bestMoveBreaksPair))
                            continue;

                        suggestion = new MoveSuggestion(sourceSlot, targetSlot, targetMatchingItems);
                        bestPriority = priority;
                        bestGroupSize = groupSize;
                        bestMoveBreaksPair = sourceMoveBreaksPair;
                    }
                }
            }
        }

        return suggestion != null;
    }

    private static List<ShelfItem> GetTargetMatchingItems(ShelfLayer targetLayer, ItemType sourceItemType)
    {
        List<ShelfItem> matchingItems = new List<ShelfItem>(targetLayer.Capacity - 1);

        foreach (ShelfSlot targetSlot in targetLayer.Slots)
        {
            if (!targetSlot.IsEmpty && targetSlot.Item.Type == sourceItemType)
                matchingItems.Add(targetSlot.Item);
        }

        return matchingItems;
    }

    private static bool WouldBreakSourcePair(ShelfSlot sourceSlot, ShelfLayer sourceLayer)
    {
        foreach (ShelfSlot otherSlot in sourceLayer.Slots)
        {
            if (otherSlot == sourceSlot || otherSlot.IsEmpty)
                continue;

            if (otherSlot.Item.Type == sourceSlot.Item.Type)
                return true;
        }

        return false;
    }

    private static bool CreatesMatch(ShelfLayer targetLayer, ItemType sourceItemType)
    {
        if (targetLayer.Capacity < Shelf.MinimumMatchCapacity)
            return false;

        int emptySlotCount = 0;

        foreach (ShelfSlot slot in targetLayer.Slots)
        {
            if (slot.IsEmpty)
            {
                emptySlotCount++;
                continue;
            }

            if (slot.Item.Type != sourceItemType)
                return false;
        }

        return emptySlotCount == 1;
    }

    private static bool WouldReleaseLayer(ShelfLayer sourceLayer, bool hasNextLayer)
    {
        if (!hasNextLayer)
            return false;

        int itemCount = 0;

        foreach (ShelfSlot slot in sourceLayer.Slots)
        {
            if (!slot.IsEmpty)
                itemCount++;
        }

        return itemCount == 1;
    }

    private static int GetSuggestionPriority(bool createsMatch, int targetMatchingItemCount, bool releasesLayer)
    {
        if (createsMatch)
            return MatchPriority;

        if (targetMatchingItemCount > 0)
            return GroupPriority;

        if (releasesLayer)
            return LayerReleasePriority;

        return PreparatoryMovePriority;
    }

    private static bool IsBetterSuggestion(
        int candidatePriority,
        int candidateGroupSize,
        bool candidateBreaksPair,
        int bestPriority,
        int bestGroupSize,
        bool bestMoveBreaksPair)
    {
        if (candidatePriority != bestPriority)
            return candidatePriority > bestPriority;

        if (candidateGroupSize != bestGroupSize)
            return candidateGroupSize > bestGroupSize;

        return bestMoveBreaksPair && !candidateBreaksPair;
    }
}
