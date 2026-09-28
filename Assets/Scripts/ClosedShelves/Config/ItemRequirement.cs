using System;
using UnityEngine;

[Serializable]
public sealed class ItemRequirement
{
    [SerializeField] private ItemType _type;
    [SerializeField, Min(3)] private int _count = 3;

    public ItemType Type => _type;
    public int Count => _count;
}
