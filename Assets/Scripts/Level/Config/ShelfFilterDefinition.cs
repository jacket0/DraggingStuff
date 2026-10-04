using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ShelfFilterDefinition
{
    [SerializeField, Min(0)] private int _shelfIndex;
    [SerializeField] private List<ItemType> _acceptedTypes = new List<ItemType>();

    public int ShelfIndex => _shelfIndex;
    public IReadOnlyList<ItemType> AcceptedTypes => _acceptedTypes;
}
