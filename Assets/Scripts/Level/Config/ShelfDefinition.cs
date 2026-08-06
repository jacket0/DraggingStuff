using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShelfDefinition
{
    [SerializeField] private List<ShelfLayerDefinition> _layers;

    public IReadOnlyList<ShelfLayerDefinition> Layers => _layers;
}