using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class MoveSuggestionProvider : MonoBehaviour, IMoveSuggestionProvider
{
    [SerializeField] private ShelfBoard _shelfBoard;

    public bool TryGetSuggestion(out MoveSuggestion suggestion)
    {
        suggestion = null;

        if (_shelfBoard.HasLockedShelves || _shelfBoard.ItemAnimations.HasAnimations)
            return false;

        int bestMatches = -1;
        int bestGroupGain = -1;
        bool bestBreaksPair = true;

        foreach (Shelf sourceShelf in _shelfBoard.Shelves)
        {
            foreach (ShelfColumnView source in sourceShelf.ColumnViews)
            {
                if (!_shelfBoard.CanPickUp(source.Column))
                    continue;

                ItemType type = source.FrontItem.Type;
                bool breaksPair = sourceShelf.Columns.Count(column => column.FrontItem != null && column.FrontItem.Type == type) > 1;

                foreach (Shelf targetShelf in _shelfBoard.Shelves)
                {
                    foreach (ShelfColumnView target in targetShelf.ColumnViews)
                    {
                        if (!_shelfBoard.TrySimulateMove(source.Column, target.Column, out BoardMoveSimulation simulation))
                            continue;

                        List<ShelfItem> matchingItems = targetShelf.Columns
                            .Where(column => column != source.Column && column.FrontItem != null && column.FrontItem.Type == type)
                            .Select(column => column.FrontItem).ToList();
                        int groupGain = sourceShelf == targetShelf ? 0 : matchingItems.Count;

                        if (simulation.MatchCount < bestMatches
                            || simulation.MatchCount == bestMatches && groupGain < bestGroupGain
                            || simulation.MatchCount == bestMatches && groupGain == bestGroupGain && (!bestBreaksPair || breaksPair))
                            continue;

                        bestMatches = simulation.MatchCount;
                        bestGroupGain = groupGain;
                        bestBreaksPair = breaksPair;
                        suggestion = new MoveSuggestion(source, target, matchingItems);
                    }
                }
            }
        }

        return suggestion != null;
    }
}
