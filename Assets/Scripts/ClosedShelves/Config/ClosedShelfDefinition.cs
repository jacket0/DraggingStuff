using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ClosedShelfDefinition
{
    [SerializeField, Min(0)] private int _shelfIndex;
    [SerializeField] private ShelfUnlockConditionKind _condition;
    [SerializeField] private List<ItemRequirement> _requiredItems = new List<ItemRequirement>();
    [SerializeField, Min(0)] private int _targetShelfIndex;
    [SerializeField, Min(1)] private int _requiredCount = 1;
    [SerializeField, Min(3)] private int _revealedGroupCount = 3;

    public int ShelfIndex => _shelfIndex;
    public ShelfUnlockConditionKind Condition => _condition;
    public IReadOnlyList<ItemRequirement> RequiredItems => _requiredItems;
    public int TargetShelfIndex => _targetShelfIndex;
    public int RequiredCount => _requiredCount;
    public int RevealedGroupCount => _revealedGroupCount;
}
