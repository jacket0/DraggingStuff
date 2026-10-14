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

        return !HasMatchableType(board.Shelves.Where(shelf => shelf.IsOpen));
    }

    public static bool IsStuckAfterNextClosing(BoardStateSnapshot board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (!board.HasCycles || board.IsCleared)
            return false;

        return !HasMatchableType(board.Shelves.Where(shelf => shelf.IsOpen && !(shelf.IsCyclic && shelf.Cycle.MovesToSwitch == 1)));
    }

    private static bool HasMatchableType(IEnumerable<ShelfStateSnapshot> shelves)
    {
        Dictionary<ItemType, int> itemCounts = new Dictionary<ItemType, int>();

        foreach (ItemType type in shelves.SelectMany(shelf => shelf.Columns).SelectMany(column => column.Items))
        {
            itemCounts.TryGetValue(type, out int count);
            itemCounts[type] = ++count;

            if (count >= ShelfStateSnapshot.MinimumMatchCapacity)
                return true;
        }

        return false;
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
