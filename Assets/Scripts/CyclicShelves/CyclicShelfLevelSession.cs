using System;
using UnityEngine;

public sealed class CyclicShelfLevelSession : LevelSession
{
    [SerializeField] private CyclicShelvesController _cyclicShelves;

    private void Awake()
    {
        if (_cyclicShelves == null)
            throw new InvalidOperationException(nameof(_cyclicShelves));
    }

    protected override void SettleBoard(Action completed)
    {
        if (State == LevelState.Playing)
            _cyclicShelves.RescueIfStuck();

        completed?.Invoke();
    }
}
