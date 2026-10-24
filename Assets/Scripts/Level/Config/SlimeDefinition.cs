using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class SlimeDefinition
{
    [SerializeField] private SlimeNeighborhood _neighborhood;
    [SerializeField] private List<SlimeColumnDefinition> _stickyColumns = new List<SlimeColumnDefinition>();
    [SerializeField, Min(1)] private int _growthPeriod = 2;

    public SlimeNeighborhood Neighborhood => _neighborhood;
    public IReadOnlyList<SlimeColumnDefinition> StickyColumns => _stickyColumns;
    public int GrowthPeriod => _growthPeriod;
    public bool IsConfigured => _stickyColumns.Count > 0;

    public SlimeSnapshot CreateSlime()
    {
        if (_neighborhood == null)
            throw new InvalidOperationException("Slime requires a neighborhood.");

        if (_stickyColumns.Any(column => column == null))
            throw new InvalidOperationException("A sticky column is missing.");

        return new SlimeSnapshot(_stickyColumns.Select(column => column.ToPosition()), _neighborhood.CreateLinks(), _growthPeriod, _growthPeriod);
    }

    public BoardStateSnapshot AddSlime(BoardStateSnapshot layout)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        return IsConfigured ? layout.WithSlime(CreateSlime()) : layout;
    }
}
