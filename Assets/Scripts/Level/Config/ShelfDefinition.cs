using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ShelfDefinition
{
    [SerializeField] private List<ShelfColumnDefinition> _columns = new List<ShelfColumnDefinition>();

    public IReadOnlyList<ShelfColumnDefinition> Columns => _columns;
}
