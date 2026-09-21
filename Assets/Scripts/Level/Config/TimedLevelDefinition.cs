using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TimedLevelDefinition", menuName = "Game/Level/Timed Level Definition")]
public sealed class TimedLevelDefinition : ScriptableObject
{
    [SerializeField, Min(1)] private int _timeLimitSeconds = 120;
    [SerializeField, Min(1)] private int _twoStarTimeSeconds = 90;
    [SerializeField, Min(1)] private int _threeStarTimeSeconds = 70;
    [SerializeField, Min(1)] private int _emptyColumnCount = 2;
    [SerializeField] private List<TimedLevelItemGroup> _itemGroups = new List<TimedLevelItemGroup>();
    [SerializeField] private List<TimedLevelVariant> _variants = new List<TimedLevelVariant>();
    [SerializeField] private ShelfItemCatalog _itemCatalog;

    public int TimeLimitSeconds => _timeLimitSeconds;
    public int TwoStarTimeSeconds => _twoStarTimeSeconds;
    public int ThreeStarTimeSeconds => _threeStarTimeSeconds;
    public int EmptyColumnCount => _emptyColumnCount;
    public IReadOnlyList<TimedLevelItemGroup> ItemGroups => _itemGroups;
    public IReadOnlyList<TimedLevelVariant> Variants => _variants;
    public ShelfItemCatalog ItemCatalog => _itemCatalog;
}
