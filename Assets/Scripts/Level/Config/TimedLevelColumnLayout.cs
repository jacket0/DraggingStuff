using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TimedLevelColumnLayout
{
    [SerializeField] private List<ItemType> _items = new List<ItemType>();

    public IReadOnlyList<ItemType> Items => _items;

    public TimedLevelColumnLayout(IReadOnlyList<ItemType> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        _items.AddRange(items);
    }
}
