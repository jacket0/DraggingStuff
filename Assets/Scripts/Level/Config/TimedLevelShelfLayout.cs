using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TimedLevelShelfLayout
{
    [SerializeField] private List<TimedLevelColumnLayout> _columns = new List<TimedLevelColumnLayout>();

    public IReadOnlyList<TimedLevelColumnLayout> Columns => _columns;

    public TimedLevelShelfLayout(IReadOnlyList<ColumnStateSnapshot> columns)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        foreach (ColumnStateSnapshot column in columns)
            _columns.Add(new TimedLevelColumnLayout(column.Items));
    }
}
