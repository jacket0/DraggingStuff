using System;

public sealed class ColumnKeySnapshot
{
    public int ItemsInFront { get; }
    public int LockedShelfIndex { get; }

    public ColumnKeySnapshot(int itemsInFront, int lockedShelfIndex)
    {
        if (itemsInFront < 0)
            throw new ArgumentOutOfRangeException(nameof(itemsInFront));

        if (lockedShelfIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(lockedShelfIndex));

        ItemsInFront = itemsInFront;
        LockedShelfIndex = lockedShelfIndex;
    }

    public ColumnKeySnapshot WithItemsInFront(int itemsInFront) => new ColumnKeySnapshot(itemsInFront, LockedShelfIndex);
}
