using System;
using UnityEngine;

[Serializable]
public sealed class TimedLevelMoveData
{
    [SerializeField] private int _sourceShelfIndex;
    [SerializeField] private int _sourceColumnIndex;
    [SerializeField] private int _targetShelfIndex;
    [SerializeField] private int _targetColumnIndex;

    public TimedLevelMoveData(TimedLevelMove move)
    {
        _sourceShelfIndex = move.Source.ShelfIndex;
        _sourceColumnIndex = move.Source.ColumnIndex;
        _targetShelfIndex = move.Target.ShelfIndex;
        _targetColumnIndex = move.Target.ColumnIndex;
    }

    public TimedLevelMove CreateMove()
    {
        return new TimedLevelMove(
            new ColumnPosition(_sourceShelfIndex, _sourceColumnIndex),
            new ColumnPosition(_targetShelfIndex, _targetColumnIndex));
    }
}
