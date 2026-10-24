using System;
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
    [SerializeField] private List<ClosedShelfDefinition> _closedShelves = new List<ClosedShelfDefinition>();
    [SerializeField] private ShelfRefillSettings _refillSettings;
    [SerializeField] private List<int> _conveyorShelfIndices = new List<int>();
    [SerializeField, Min(0)] private int _shuffleSwapCount;
    [SerializeField] private List<ShelfFilterDefinition> _shelfFilters = new List<ShelfFilterDefinition>();
    [SerializeField, Min(0)] private int _filteredEmptyColumnCount;
    [SerializeField] private List<int> _keyLockedShelfIndices = new List<int>();
    [SerializeField, Min(1)] private int _minimumKeyDepth = 1;
    [SerializeField, Min(0)] private int _minimumMovesToFirstKey;
    [SerializeField] private List<CyclicShelfDefinition> _cyclicShelves = new List<CyclicShelfDefinition>();
    [SerializeField, Min(0)] private int _minimumItemsBehindShutter;
    [SerializeField, Min(0)] private int _minimumPreparationMoves;
    [SerializeField] private SlimeDefinition _slime = new SlimeDefinition();

    public int TimeLimitSeconds => _timeLimitSeconds;
    public int TwoStarTimeSeconds => _twoStarTimeSeconds;
    public int ThreeStarTimeSeconds => _threeStarTimeSeconds;
    public int EmptyColumnCount => _emptyColumnCount;
    public IReadOnlyList<TimedLevelItemGroup> ItemGroups => _itemGroups;
    public IReadOnlyList<TimedLevelVariant> Variants => _variants;
    public ShelfItemCatalog ItemCatalog => _itemCatalog;
    public IReadOnlyList<ClosedShelfDefinition> ClosedShelves => _closedShelves;
    public ShelfRefillSettings RefillSettings => _refillSettings;
    public bool HasClosedShelves => _closedShelves.Count > 0;
    public IReadOnlyList<int> ConveyorShelfIndices => _conveyorShelfIndices;
    public bool HasConveyors => _conveyorShelfIndices.Count > 0;
    public int ShuffleSwapCount => _shuffleSwapCount;
    public IReadOnlyList<ShelfFilterDefinition> ShelfFilters => _shelfFilters;
    public bool HasShelfFilters => _shelfFilters.Count > 0;
    public int FilteredEmptyColumnCount => _filteredEmptyColumnCount;
    public IReadOnlyList<int> KeyLockedShelfIndices => _keyLockedShelfIndices;
    public bool HasKeyLocks => _keyLockedShelfIndices.Count > 0;
    public int MinimumKeyDepth => _minimumKeyDepth;
    public int MinimumMovesToFirstKey => _minimumMovesToFirstKey;
    public IReadOnlyList<CyclicShelfDefinition> CyclicShelves => _cyclicShelves;
    public bool HasCyclicShelves => _cyclicShelves.Count > 0;
    public int MinimumItemsBehindShutter => _minimumItemsBehindShutter;
    public int MinimumPreparationMoves => _minimumPreparationMoves;
    public SlimeDefinition Slime => _slime;
    public bool HasSlime => _slime != null && _slime.IsConfigured;

    public int GetMechanicElementCount(LevelMechanic mechanic) => mechanic switch
    {
        LevelMechanic.Basics => 0,
        LevelMechanic.ShelfFilter => _shelfFilters.Count,
        LevelMechanic.ClosedShelves => _closedShelves.Count,
        LevelMechanic.Conveyor => _conveyorShelfIndices.Count,
        LevelMechanic.KeyLocks => _keyLockedShelfIndices.Count,
        LevelMechanic.CyclicShelves => _cyclicShelves.Count,
        LevelMechanic.Slime => HasSlime ? _slime.StickyColumns.Count : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(mechanic))
    };

    public BoardStateSnapshot AddCyclicShelves(BoardStateSnapshot layout) => CyclicShelfDefinition.AddCycles(layout, _cyclicShelves);

    public BoardStateSnapshot AddSlime(BoardStateSnapshot layout) => HasSlime ? _slime.AddSlime(layout) : layout;
}
