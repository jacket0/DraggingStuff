using System;

public readonly struct SlimeColumnLink : IEquatable<SlimeColumnLink>
{
    public ColumnPosition First { get; }
    public ColumnPosition Second { get; }

    public SlimeColumnLink(ColumnPosition first, ColumnPosition second)
    {
        if (first.ShelfIndex == second.ShelfIndex)
            throw new ArgumentException("A slime link must connect columns of two different shelves.", nameof(second));

        First = first;
        Second = second;
    }

    public bool Connects(int firstShelfIndex, int secondShelfIndex)
    {
        return First.ShelfIndex == firstShelfIndex && Second.ShelfIndex == secondShelfIndex
            || First.ShelfIndex == secondShelfIndex && Second.ShelfIndex == firstShelfIndex;
    }

    public bool TryGetOtherEnd(ColumnPosition position, out ColumnPosition otherEnd)
    {
        otherEnd = position.Equals(First) ? Second : First;
        return position.Equals(First) || position.Equals(Second);
    }

    public bool Equals(SlimeColumnLink other)
    {
        return First.Equals(other.First) && Second.Equals(other.Second) || First.Equals(other.Second) && Second.Equals(other.First);
    }

    public override bool Equals(object obj) => obj is SlimeColumnLink other && Equals(other);
    public override int GetHashCode() => First.GetHashCode() ^ Second.GetHashCode();
}
