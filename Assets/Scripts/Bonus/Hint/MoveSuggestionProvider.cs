using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class MoveSuggestionProvider : MonoBehaviour, IMoveSuggestionProvider
{
    [SerializeField] private ShelfBoard _shelfBoard;

    private readonly BoardMoveSimulator _simulator = new BoardMoveSimulator();

    public bool TryGetSuggestion(out MoveSuggestion suggestion) => TryGetSuggestion(true, out suggestion);

    public bool TryGetSuggestion(bool includesSwaps, out MoveSuggestion suggestion)
    {
        suggestion = null;

        if (_shelfBoard.HasLockedShelves || _shelfBoard.ItemAnimations.HasAnimations)
            return false;

        SuggestionRank bestRank = default;

        foreach (Shelf sourceShelf in _shelfBoard.Shelves)
        {
            foreach (ShelfColumnView source in sourceShelf.ColumnViews)
            {
                if (!_shelfBoard.CanPickUp(source.Column))
                    continue;

                ItemType type = source.FrontItem.Type;
                bool breaksPair = _shelfBoard.CanMatchType(sourceShelf, type)
                    && sourceShelf.Columns.Count(column => column.FrontItem != null && column.FrontItem.Type == type) > 1;

                foreach (Shelf targetShelf in _shelfBoard.Shelves)
                {
                    foreach (ShelfColumnView target in targetShelf.ColumnViews)
                    {
                        bool isSwap = !target.IsEmpty;

                        if (target == source || isSwap && (!includesSwaps || sourceShelf == targetShelf))
                            continue;

                        if (!_shelfBoard.TrySimulateMove(source.Column, target.Column, out BoardMoveSimulation simulation))
                            continue;

                        List<ShelfItem> matchingItems = _shelfBoard.CanMatchType(targetShelf, type)
                            ? targetShelf.Columns
                                .Where(column => column != source.Column && column != target.Column
                                    && column.FrontItem != null && column.FrontItem.Type == type)
                                .Select(column => column.FrontItem).ToList()
                            : new List<ShelfItem>();
                        int groupGain = sourceShelf == targetShelf ? 0 : matchingItems.Count;

                        if (isSwap && !IsUsefulSwap(simulation, groupGain, sourceShelf, source, target.FrontItem.Type))
                            continue;

                        SuggestionRank rank = new SuggestionRank(
                            CycleDeadlockRule.IsStuckAfterNextClosing(simulation.State) || LeadsToSlimeLoss(simulation.State),
                            simulation.MatchCount,
                            simulation.OpenedShelfCount,
                            sourceShelf != targetShelf && IsClosingOnNextMatchingMove(sourceShelf),
                            groupGain,
                            isSwap,
                            breaksPair);

                        if (suggestion != null && !rank.IsBetterThan(bestRank))
                            continue;

                        bestRank = rank;
                        suggestion = new MoveSuggestion(source, target, matchingItems);
                    }
                }
            }
        }

        return suggestion != null;
    }

    private bool IsUsefulSwap(BoardMoveSimulation simulation, int groupGain, Shelf sourceShelf, ShelfColumnView source, ItemType displacedType)
    {
        if (simulation.MatchCount > 0 || simulation.OpenedShelfCount > 0 || groupGain > 0)
            return true;

        return _shelfBoard.CanMatchType(sourceShelf, displacedType)
            && sourceShelf.Columns.Any(column => column != source.Column && column.FrontItem != null && column.FrontItem.Type == displacedType);
    }

    private bool LeadsToSlimeLoss(BoardStateSnapshot state)
    {
        return state.HasSlime && (SlimeRule.HasHopelessShelf(state) || SlimeRule.IsStuck(state, _simulator));
    }

    private bool IsClosingOnNextMatchingMove(Shelf shelf)
    {
        ShelfCycleSnapshot cycle = _shelfBoard.GetCycle(shelf);
        return cycle != null && cycle.MovesToSwitch == 1 && _shelfBoard.IsCyclePhaseOpen(shelf);
    }

    private readonly struct SuggestionRank
    {
        private readonly bool _leadsToDeadlock;
        private readonly int _matchCount;
        private readonly int _openedShelfCount;
        private readonly bool _evacuatesClosingShelf;
        private readonly int _groupGain;
        private readonly bool _isSwap;
        private readonly bool _breaksPair;

        public SuggestionRank(bool leadsToDeadlock, int matchCount, int openedShelfCount, bool evacuatesClosingShelf, int groupGain,
            bool isSwap, bool breaksPair)
        {
            _leadsToDeadlock = leadsToDeadlock;
            _matchCount = matchCount;
            _openedShelfCount = openedShelfCount;
            _evacuatesClosingShelf = evacuatesClosingShelf;
            _groupGain = groupGain;
            _isSwap = isSwap;
            _breaksPair = breaksPair;
        }

        public bool IsBetterThan(SuggestionRank other)
        {
            if (_leadsToDeadlock != other._leadsToDeadlock)
                return !_leadsToDeadlock;

            if (_matchCount != other._matchCount)
                return _matchCount > other._matchCount;

            if (_openedShelfCount != other._openedShelfCount)
                return _openedShelfCount > other._openedShelfCount;

            if (_evacuatesClosingShelf != other._evacuatesClosingShelf)
                return _evacuatesClosingShelf;

            if (_groupGain != other._groupGain)
                return _groupGain > other._groupGain;

            if (_isSwap != other._isSwap)
                return !_isSwap;

            return other._breaksPair && !_breaksPair;
        }
    }
}
