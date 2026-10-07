using System.Linq;

public static class TimedLevelLayoutRules
{
    public const int GeneratorVersion = 2;
    public const int MaximumColumnDepth = 8;
    public const int MaximumConveyorColumnDepth = 3;
    public const int ConveyorShelfCapacity = 3;
    public const int MaximumAcceptedTypeCount = ShelfStateSnapshot.MaximumAcceptedTypeCount;

    public static bool IsConveyorStartFilled(ShelfStateSnapshot shelf)
    {
        bool hasQueue = false;

        foreach (ColumnStateSnapshot column in shelf.Columns)
        {
            if (column.IsEmpty)
                return false;

            hasQueue |= column.Count >= 2;
        }

        return hasQueue;
    }

    public static int CountFilteredEmptyColumns(BoardStateSnapshot board)
    {
        return board.Shelves
            .Where(shelf => shelf.IsFiltered)
            .Sum(shelf => shelf.Columns.Count(column => column.IsEmpty));
    }
}
