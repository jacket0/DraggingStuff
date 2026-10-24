using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SlimeSnapshot
{
    private readonly HashSet<ColumnPosition> _stickyColumns;

    public IReadOnlyList<ColumnPosition> StickyColumns { get; }
    public IReadOnlyList<SlimeColumnLink> Links { get; }
    public int GrowthPeriod { get; }
    public int MovesToGrowth { get; }
    public bool HasStickyColumns => StickyColumns.Count > 0;

    public SlimeSnapshot(IEnumerable<ColumnPosition> stickyColumns, IEnumerable<SlimeColumnLink> links, int growthPeriod, int movesToGrowth)
    {
        if (stickyColumns == null)
            throw new ArgumentNullException(nameof(stickyColumns));

        if (links == null)
            throw new ArgumentNullException(nameof(links));

        if (growthPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(growthPeriod));

        if (movesToGrowth < 1 || movesToGrowth > growthPeriod)
            throw new ArgumentOutOfRangeException(nameof(movesToGrowth));

        ColumnPosition[] sortedColumns = stickyColumns.OrderBy(column => column.ShelfIndex).ThenBy(column => column.ColumnIndex).ToArray();
        _stickyColumns = new HashSet<ColumnPosition>(sortedColumns);

        if (_stickyColumns.Count != sortedColumns.Length)
            throw new ArgumentException("A column cannot be sticky twice.", nameof(stickyColumns));

        SlimeColumnLink[] linkCopy = links.ToArray();

        if (linkCopy.Distinct().Count() != linkCopy.Length)
            throw new ArgumentException("Two slime links cannot connect the same columns.", nameof(links));

        StickyColumns = Array.AsReadOnly(sortedColumns);
        Links = Array.AsReadOnly(linkCopy);
        GrowthPeriod = growthPeriod;
        MovesToGrowth = movesToGrowth;
    }

    public bool IsSticky(ColumnPosition position) => _stickyColumns.Contains(position);

    public bool AreNeighborShelves(int firstShelfIndex, int secondShelfIndex)
    {
        return firstShelfIndex != secondShelfIndex && Links.Any(link => link.Connects(firstShelfIndex, secondShelfIndex));
    }

    public SlimeSnapshot WithStickyColumns(IEnumerable<ColumnPosition> stickyColumns, int movesToGrowth)
    {
        return new SlimeSnapshot(stickyColumns, Links, GrowthPeriod, movesToGrowth);
    }
}
