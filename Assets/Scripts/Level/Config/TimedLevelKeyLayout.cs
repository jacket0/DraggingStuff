using System;
using UnityEngine;

[Serializable]
public sealed class TimedLevelKeyLayout
{
    [SerializeField, Min(0)] private int _itemsInFront;
    [SerializeField, Min(0)] private int _lockedShelfIndex;

    public int ItemsInFront => _itemsInFront;
    public int LockedShelfIndex => _lockedShelfIndex;

    public TimedLevelKeyLayout(ColumnKeySnapshot key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        _itemsInFront = key.ItemsInFront;
        _lockedShelfIndex = key.LockedShelfIndex;
    }

    public ColumnKeySnapshot CreateSnapshot() => new ColumnKeySnapshot(_itemsInFront, _lockedShelfIndex);
}
