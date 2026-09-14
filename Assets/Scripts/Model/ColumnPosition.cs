using System;

public readonly struct ColumnPosition : IEquatable<ColumnPosition>
{
    public int ShelfIndex { get; }
    public int ColumnIndex { get; }

    public ColumnPosition(int shelfIndex, int columnIndex)
    {
        ShelfIndex = shelfIndex;
        ColumnIndex = columnIndex;
    }

    public bool Equals(ColumnPosition other)
    {
        return ShelfIndex == other.ShelfIndex && ColumnIndex == other.ColumnIndex;
    }

    public override bool Equals(object obj) => obj is ColumnPosition other && Equals(other);
    public override int GetHashCode() => unchecked(ShelfIndex * 397 ^ ColumnIndex);
}
