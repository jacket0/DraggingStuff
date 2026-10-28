using System;
using UnityEngine;

public sealed class SlimeLevelSession : LevelSession
{
    [SerializeField] private SlimeController _slime;

    private void Awake()
    {
        if (_slime == null)
            throw new InvalidOperationException(nameof(_slime));
    }

    protected override void HandleBoardSettled(bool isBoardCleared)
    {
        base.HandleBoardSettled(isBoardCleared);

        if (State != LevelState.Playing)
            return;

        Shelf lostShelf = ShelfBoard.FindShelfLostToSlime();

        if (lostShelf != null)
        {
            _slime.ReportShelfLost(lostShelf);
            FinishRun(false, LevelDefeatReason.ShelfLost);
            return;
        }

        if (!ShelfBoard.IsStuckInSlime())
            return;

        _slime.ReportDeadlock();
        FinishRun(false, LevelDefeatReason.NoMoves);
    }
}
