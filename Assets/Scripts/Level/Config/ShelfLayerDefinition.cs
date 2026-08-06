using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShelfLayerDefinition
{
    [SerializeField] private List<ShelfItem> _itemPrefabs;

    public IReadOnlyList<ShelfItem> ItemPrefabs => _itemPrefabs;
}