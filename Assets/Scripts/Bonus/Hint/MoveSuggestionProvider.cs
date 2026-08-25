using System.Collections.Generic;
using UnityEngine;

public sealed class MoveSuggestionProvider : MonoBehaviour, IMoveSuggestionProvider
{
    private const int MatchPriority = 3;
    private const int PairPriority = 2;
    private const int RegularMovePriority = 1;

    [SerializeField] private ShelfBoard _shelfBoard;

    public bool TryGetSuggestion(out MoveSuggestion suggestion)
    {
        suggestion = null;
        int bestPriority = 0;
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
                        int priority = GetSuggestionPriority(targetMatchingItems.Count);

                        if (!IsBetterSuggestion(priority, sourceMoveBreaksPair, bestPriority, bestMoveBreaksPair))
                            continue;

                        suggestion = new MoveSuggestion(sourceSlot, targetSlot, targetMatchingItems);
                        bestPriority = priority;
                        bestMoveBreaksPair = sourceMoveBreaksPair;
                    }
                }
            }
        }

        return suggestion != null;
    }

    private static List<ShelfItem> GetTargetMatchingItems(ShelfLayer targetLayer, ItemType sourceItemType)
    {
        List<ShelfItem> matchingItems = new List<ShelfItem>(ShelfLayer.SlotCount - 1);

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

    private static int GetSuggestionPriority(int targetMatchingItemCount)
    {
        if (targetMatchingItemCount == ShelfLayer.SlotCount - 1)
            return MatchPriority;

        if (targetMatchingItemCount == 1)
            return PairPriority;

        return RegularMovePriority;
    }

    private static bool IsBetterSuggestion(int candidatePriority, bool candidateBreaksPair, int bestPriority, bool bestMoveBreaksPair)
    {
        if (candidatePriority != bestPriority)
            return candidatePriority > bestPriority;

        return bestMoveBreaksPair && !candidateBreaksPair;
    }
}