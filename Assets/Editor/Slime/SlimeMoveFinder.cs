using System.Collections.Generic;
using System.Linq;

public static class SlimeMoveFinder
{
    public static List<SlimeBotMove> FindMoves(BoardStateSnapshot board)
    {
        List<ColumnPosition> sources = new List<ColumnPosition>();
        List<ColumnPosition> emptyTargets = new List<ColumnPosition>();

        for (int shelfIndex = 0; shelfIndex < board.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = board.Shelves[shelfIndex];

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                ColumnPosition position = new ColumnPosition(shelfIndex, columnIndex);
                ColumnStateSnapshot column = shelf.Columns[columnIndex];

                if (column.IsEmpty)
                    emptyTargets.Add(position);
                else if (column.FrontItem != null && !board.IsSticky(position))
                    sources.Add(position);
            }
        }

        ColumnPosition? growthTarget = board.HasSlime && board.Slime.MovesToGrowth == 1 ? SlimeRule.FindGrowthTarget(board) : null;
        List<SlimeBotMove> moves = new List<SlimeBotMove>();

        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            ColumnPosition source = sources[sourceIndex];
            ItemType type = FrontItem(board, source).Value;
            bool isEvacuation = growthTarget.HasValue && growthTarget.Value.Equals(source);

            foreach (ColumnPosition target in emptyTargets)
            {
                if (source.ShelfIndex == target.ShelfIndex && board.Shelves[source.ShelfIndex].Columns[source.ColumnIndex].Count == 1)
                    continue;

                int sameFronts = CountOtherFronts(board, target, type);
                bool isMatch = sameFronts == board.Shelves[target.ShelfIndex].Capacity - 1;
                bool isSetup = !isMatch && sameFronts > 0;
                bool sticksItem = !isMatch && board.IsSticky(target);
                moves.Add(new SlimeBotMove(source, target, isMatch, isMatch && CleansSlime(board, target.ShelfIndex), isSetup, isEvacuation, sticksItem));
            }

            for (int targetIndex = sourceIndex + 1; targetIndex < sources.Count; targetIndex++)
            {
                ColumnPosition target = sources[targetIndex];
                ItemType targetType = FrontItem(board, target).Value;

                if (targetType == type)
                    continue;

                bool matchesTarget = CountOtherFronts(board, target, type) == board.Shelves[target.ShelfIndex].Capacity - 1;
                bool matchesSource = CountOtherFronts(board, source, targetType) == board.Shelves[source.ShelfIndex].Capacity - 1;
                bool isCleaning = matchesTarget && CleansSlime(board, target.ShelfIndex) || matchesSource && CleansSlime(board, source.ShelfIndex);
                moves.Add(new SlimeBotMove(source, target, matchesTarget || matchesSource, isCleaning, false, false, false));
            }
        }

        return moves;
    }

    private static bool CleansSlime(BoardStateSnapshot board, int matchedShelfIndex)
    {
        return board.HasSlime && board.Slime.StickyColumns.Any(column =>
            column.ShelfIndex == matchedShelfIndex || board.Slime.AreNeighborShelves(matchedShelfIndex, column.ShelfIndex));
    }

    private static int CountOtherFronts(BoardStateSnapshot board, ColumnPosition position, ItemType type)
    {
        ShelfStateSnapshot shelf = board.Shelves[position.ShelfIndex];
        int count = 0;

        for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
        {
            if (columnIndex != position.ColumnIndex && shelf.Columns[columnIndex].FrontItem == type)
                count++;
        }

        return count;
    }

    private static ItemType? FrontItem(BoardStateSnapshot board, ColumnPosition position) => board.Shelves[position.ShelfIndex].Columns[position.ColumnIndex].FrontItem;
}
