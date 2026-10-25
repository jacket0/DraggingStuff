using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SlimeNeighborhood", menuName = "Game/Level/Slime Neighborhood")]
public sealed class SlimeNeighborhood : ScriptableObject
{
    [SerializeField] private List<SlimeLinkDefinition> _links = new List<SlimeLinkDefinition>();
    [SerializeField] private List<SlimeColumnDefinition> _perimeterColumns = new List<SlimeColumnDefinition>();

    public IReadOnlyList<SlimeLinkDefinition> Links => _links;
    public IReadOnlyList<SlimeColumnDefinition> PerimeterColumns => _perimeterColumns;

    public IReadOnlyList<SlimeColumnLink> CreateLinks()
    {
        if (_links.Any(link => link == null || link.First == null || link.Second == null))
            throw new InvalidOperationException($"{name}: a slime link is missing.");

        return _links.Select(link => new SlimeColumnLink(link.First.ToPosition(), link.Second.ToPosition())).ToArray();
    }

    public IReadOnlyList<ColumnPosition> CreatePerimeter()
    {
        if (_perimeterColumns.Any(column => column == null))
            throw new InvalidOperationException($"{name}: a perimeter column is missing.");

        return _perimeterColumns.Select(column => column.ToPosition()).ToArray();
    }
}
