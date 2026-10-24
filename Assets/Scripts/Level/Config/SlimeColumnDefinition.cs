using System;
using UnityEngine;

[Serializable]
public sealed class SlimeColumnDefinition
{
    [SerializeField, Min(0)] private int _shelfIndex;
    [SerializeField, Min(0)] private int _columnIndex;

    public int ShelfIndex => _shelfIndex;
    public int ColumnIndex => _columnIndex;

    public ColumnPosition ToPosition() => new ColumnPosition(_shelfIndex, _columnIndex);
}
