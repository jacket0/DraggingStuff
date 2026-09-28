using System;
using UnityEngine;

public sealed class ClosedShelfLevelSession : LevelSession
{
    [SerializeField] private ClosedShelvesController _closedShelves;

    protected override void PrepareBoardAfterMove(Action completed)
    {
        if (State == LevelState.Playing)
            _closedShelves.PrepareBoard();

        completed?.Invoke();
    }
}
