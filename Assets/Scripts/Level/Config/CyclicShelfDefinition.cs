using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CyclicShelfDefinition
{
    [SerializeField, Min(0)] private int _shelfIndex;
    [SerializeField] private bool _startsOpen = true;
    [SerializeField, Min(1)] private int _openMoves = 3;
    [SerializeField, Min(1)] private int _closedMoves = 3;
    [SerializeField, Min(1)] private int _movesToFirstSwitch = 3;

    public int ShelfIndex => _shelfIndex;
    public bool StartsOpen => _startsOpen;
    public int OpenMoves => _openMoves;
    public int ClosedMoves => _closedMoves;
    public int MovesToFirstSwitch => _movesToFirstSwitch;
    public int StartDuration => _startsOpen ? _openMoves : _closedMoves;

    public ShelfCycleSnapshot CreateCycle() => new ShelfCycleSnapshot(_openMoves, _closedMoves, _movesToFirstSwitch);

    public static BoardStateSnapshot AddCycles(BoardStateSnapshot layout, IReadOnlyCollection<CyclicShelfDefinition> cyclicShelves)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        if (cyclicShelves == null)
            throw new ArgumentNullException(nameof(cyclicShelves));

        if (cyclicShelves.Count == 0)
            return layout;

        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[layout.Shelves.Count];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
            shelves[shelfIndex] = layout.Shelves[shelfIndex];

        foreach (CyclicShelfDefinition cyclicShelf in cyclicShelves)
            shelves[cyclicShelf.ShelfIndex] = shelves[cyclicShelf.ShelfIndex].WithCycle(cyclicShelf.CreateCycle(), shelves[cyclicShelf.ShelfIndex].IsOpen);

        return new BoardStateSnapshot(shelves, layout.FilteredTypes);
    }
}
