using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ShelfRefillSettings", menuName = "Game/Level/Shelf Refill Settings")]
public sealed class ShelfRefillSettings : ScriptableObject
{
    [SerializeField, Min(1)] private int _hiddenItemThreshold = 18;
    [SerializeField, Min(1)] private int _groupsPerRefill = 4;
    [SerializeField, Min(1f)] private float _conditionTypeWeight = 1.6f;
    [SerializeField, Min(1)] private int _maximumTriplePerShelf = 2;

    public int HiddenItemThreshold => _hiddenItemThreshold;
    public int GroupsPerRefill => _groupsPerRefill;
    public float ConditionTypeWeight => _conditionTypeWeight;
    public int MaximumTriplePerShelf => _maximumTriplePerShelf;

    public void Validate()
    {
        if (_hiddenItemThreshold <= 0 || _groupsPerRefill <= 0 || _conditionTypeWeight < 1f || _maximumTriplePerShelf <= 0)
            throw new InvalidOperationException(nameof(ShelfRefillSettings));
    }
}
