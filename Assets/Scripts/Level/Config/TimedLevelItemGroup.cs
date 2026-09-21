using System;
using UnityEngine;

[Serializable]
public sealed class TimedLevelItemGroup
{
    [SerializeField] private ItemType _type;
    [SerializeField, Min(1)] private int _groupCount = 1;

    public ItemType Type => _type;
    public int GroupCount => _groupCount;
}
