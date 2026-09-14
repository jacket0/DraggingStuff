using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ShelfColumnDefinition
{
    [SerializeField] private List<ShelfItem> _itemPrefabs = new List<ShelfItem>();

    public IReadOnlyList<ShelfItem> ItemPrefabs => _itemPrefabs;
}
