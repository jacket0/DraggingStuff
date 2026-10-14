using System;
using System.Collections.Generic;
using System.Linq;

public static class CycleDeadlockRule
{
    public static bool IsStuck(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (!board.HasCycles || board.IsCleared)
            return false;

        Dictionary<ItemType, int> openItemCounts = new Dictionary<ItemType, int>();

        foreach (ShelfStateSnapshot shelf in board.Shelves.Where(shelf => shelf.IsOpen))
        {
            foreach (ItemType type in shelf.Columns.SelectMany(column => column.Items))
            {
                openItemCounts.TryGetValue(type, out int count);
                openItemCounts[type] = ++count;

                if (count >= ShelfStateSnapshot.MinimumMatchCapacity)
                    return false;
            }
        }

        return true;
    }

    public static BoardStateSnapshot ApplyRescue(BoardStateSnapshot board, bool isRepeated, out IReadOnlyList<int> switchedShelfIndexes)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (!board.HasCycles)
            throw new InvalidOperationException("Only a board with cycles can be rescued.");

        ShelfStateSnapshot[] shelves = board.Shelves.ToArray();
        List<int> switchedIndexes = new List<int>();
        int movesToNextSwitch = board.Shelves.Where(shelf => shelf.IsCyclic).Min(shelf => shelf.Cycle.MovesToSwitch);

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            ShelfStateSnapshot shelf = shelves[shelfIndex];

            if (!shelf.IsCyclic)
                continue;

            if (isRepeated)
            {
                shelves[shelfIndex] = shelf.WithCycle(null, true);

                if (!shelf.IsOpen)
                    switchedIndexes.Add(shelfIndex);
            }
            else
            {
                shelves[shelfIndex] = shelf.AdvanceCycle(movesToNextSwitch, out bool switched);

                if (switched)
                    switchedIndexes.Add(shelfIndex);
            }
        }

        switchedShelfIndexes = switchedIndexes.AsReadOnly();
        return new BoardStateSnapshot(shelves, board.FilteredTypes);
    }
}
