using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class TimedLevelColumnLayout
{
    [SerializeField] private List<ItemType> _items = new List<ItemType>();
    [SerializeField] private List<TimedLevelKeyLayout> _keys = new List<TimedLevelKeyLayout>();

    public IReadOnlyList<ItemType> Items => _items;
    public IReadOnlyList<TimedLevelKeyLayout> Keys => _keys;

    public TimedLevelColumnLayout(ColumnStateSnapshot column)
    {
        if (column == null)
            throw new ArgumentNullException(nameof(column));

        _items.AddRange(column.Items);
        _keys.AddRange(column.Keys.Select(key => new TimedLevelKeyLayout(key)));
    }

    public ColumnStateSnapshot CreateSnapshot() => new ColumnStateSnapshot(_items.ToArray(), _keys.Select(key => key.CreateSnapshot()).ToArray());
}
