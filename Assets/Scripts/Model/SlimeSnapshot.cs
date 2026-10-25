using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SlimeSnapshot
{
    private readonly HashSet<ColumnPosition> _stickyColumns;

    public IReadOnlyList<ColumnPosition> StickyColumns { get; }
    public IReadOnlyList<SlimeColumnLink> Links { get; }
    public IReadOnlyList<ColumnPosition> PerimeterColumns { get; }
    public SlimeRules Rules { get; }
    public int GrowthPeriod { get; }
    public int MovesToGrowth { get; }
    public bool HasStickyColumns => StickyColumns.Count > 0;

    public SlimeSnapshot(
        IEnumerable<ColumnPosition> stickyColumns,
        IEnumerable<SlimeColumnLink> links,
        IEnumerable<ColumnPosition> perimeterColumns,
        int growthPeriod,
        int movesToGrowth,
        SlimeRules rules = null)
    {
        if (stickyColumns == null)
            throw new ArgumentNullException(nameof(stickyColumns));

        if (links == null)
            throw new ArgumentNullException(nameof(links));

        if (perimeterColumns == null)
            throw new ArgumentNullException(nameof(perimeterColumns));

        if (growthPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(growthPeriod));

        Rules = rules ?? SlimeRules.Default;

        if (movesToGrowth < 1 || movesToGrowth > growthPeriod + Rules.GraceMoves)
            throw new ArgumentOutOfRangeException(nameof(movesToGrowth));

        ColumnPosition[] sortedColumns = stickyColumns.OrderBy(column => column.ShelfIndex).ThenBy(column => column.ColumnIndex).ToArray();
        _stickyColumns = new HashSet<ColumnPosition>(sortedColumns);

        if (_stickyColumns.Count != sortedColumns.Length)
            throw new ArgumentException("A column cannot be sticky twice.", nameof(stickyColumns));

        SlimeColumnLink[] linkCopy = links.ToArray();

        if (linkCopy.Distinct().Count() != linkCopy.Length)
            throw new ArgumentException("Two slime links cannot connect the same columns.", nameof(links));

        ColumnPosition[] perimeterCopy = perimeterColumns.ToArray();

        if (perimeterCopy.Distinct().Count() != perimeterCopy.Length)
            throw new ArgumentException("A perimeter column cannot repeat.", nameof(perimeterColumns));

        StickyColumns = Array.AsReadOnly(sortedColumns);
        Links = Array.AsReadOnly(linkCopy);
        PerimeterColumns = Array.AsReadOnly(perimeterCopy);
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
        return new SlimeSnapshot(stickyColumns, Links, PerimeterColumns, GrowthPeriod, movesToGrowth, Rules);
    }
}
