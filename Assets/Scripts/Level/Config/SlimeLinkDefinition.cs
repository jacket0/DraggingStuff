using System;
using UnityEngine;

[Serializable]
public sealed class SlimeLinkDefinition
{
    [SerializeField] private SlimeColumnDefinition _first;
    [SerializeField] private SlimeColumnDefinition _second;

    public SlimeColumnDefinition First => _first;
    public SlimeColumnDefinition Second => _second;
}
