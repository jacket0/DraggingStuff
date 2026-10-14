using System.Collections.Generic;

public static class CyclicShelfMoveFinder
{
    public static List<CyclicShelfBotMove> FindMoves(BoardStateSnapshot board)
    {
        List<ColumnPosition> sources = new List<ColumnPosition>();
        List<ColumnPosition> emptyTargets = new List<ColumnPosition>();

        for (int shelfIndex = 0; shelfIndex < board.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = board.Shelves[shelfIndex];

            if (!shelf.IsOpen)
                continue;

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                ColumnStateSnapshot column = shelf.Columns[columnIndex];

                if (column.FrontItem != null)
                    sources.Add(new ColumnPosition(shelfIndex, columnIndex));
                else if (column.IsEmpty)
                    emptyTargets.Add(new ColumnPosition(shelfIndex, columnIndex));
            }
        }

        List<CyclicShelfBotMove> moves = new List<CyclicShelfBotMove>();

        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            ColumnPosition source = sources[sourceIndex];
            ItemType type = FrontItem(board, source).Value;

            foreach (ColumnPosition target in emptyTargets)
            {
                if (source.ShelfIndex == target.ShelfIndex && board.Shelves[source.ShelfIndex].Columns[source.ColumnIndex].Count == 1)
                    continue;

                int sameFronts = CountOtherFronts(board, target, type);
                bool isMatch = sameFronts == board.Shelves[target.ShelfIndex].Capacity - 1;
                bool isSetup = !isMatch && sameFronts > 0;
                bool isEvacuation = !isMatch && IsClosingNext(board, source.ShelfIndex) && !IsClosingNext(board, target.ShelfIndex);
                moves.Add(new CyclicShelfBotMove(source, target, isMatch, isSetup, isEvacuation));
            }

            for (int targetIndex = sourceIndex + 1; targetIndex < sources.Count; targetIndex++)
            {
                ColumnPosition target = sources[targetIndex];
                ItemType targetType = FrontItem(board, target).Value;

                if (targetType == type)
                    continue;

                bool isMatch = CountOtherFronts(board, target, type) == board.Shelves[target.ShelfIndex].Capacity - 1
                    || CountOtherFronts(board, source, targetType) == board.Shelves[source.ShelfIndex].Capacity - 1;
                moves.Add(new CyclicShelfBotMove(source, target, isMatch, false, false));
            }
        }

        return moves;
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

    public static bool IsClosingNext(BoardStateSnapshot board, int shelfIndex)
    {
        ShelfStateSnapshot shelf = board.Shelves[shelfIndex];
        return shelf.IsCyclic && shelf.IsOpen && shelf.Cycle.MovesToSwitch == 1;
    }

    private static ItemType? FrontItem(BoardStateSnapshot board, ColumnPosition position) => board.Shelves[position.ShelfIndex].Columns[position.ColumnIndex].FrontItem;
}
