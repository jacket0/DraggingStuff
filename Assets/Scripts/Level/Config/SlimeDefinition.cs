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
    [SerializeField, Min(1)] private int _columnsPerGrowth = 1;
    [SerializeField] private bool _cleansOwnShelfOnly;
    [SerializeField, Min(0)] private int _graceMoves;

    public SlimeNeighborhood Neighborhood => _neighborhood;
    public IReadOnlyList<SlimeColumnDefinition> StickyColumns => _stickyColumns;
    public int GrowthPeriod => _growthPeriod;
    public int ColumnsPerGrowth => Mathf.Max(1, _columnsPerGrowth);
    public bool CleansOwnShelfOnly => _cleansOwnShelfOnly;
    public int GraceMoves => Mathf.Max(0, _graceMoves);
    public bool IsConfigured => _neighborhood != null;

    public SlimeSnapshot CreateSlime()
    {
        if (_neighborhood == null)
            throw new InvalidOperationException("Slime requires a neighborhood.");

        if (_stickyColumns.Any(column => column == null))
            throw new InvalidOperationException("A sticky column is missing.");

        return new SlimeSnapshot(
            _stickyColumns.Select(column => column.ToPosition()),
            _neighborhood.CreateLinks(),
            _neighborhood.CreatePerimeter(),
            _growthPeriod,
            _growthPeriod + GraceMoves,
            new SlimeRules(ColumnsPerGrowth, !_cleansOwnShelfOnly, GraceMoves));
    }

    public BoardStateSnapshot AddSlime(BoardStateSnapshot layout)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        return IsConfigured ? layout.WithSlime(CreateSlime()) : layout;
    }
}
