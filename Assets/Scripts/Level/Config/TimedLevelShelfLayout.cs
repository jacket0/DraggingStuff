using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TimedLevelShelfLayout
{
    [SerializeField] private List<TimedLevelColumnLayout> _columns = new List<TimedLevelColumnLayout>();
    [SerializeField] private bool _isClosed;

    public IReadOnlyList<TimedLevelColumnLayout> Columns => _columns;
    public bool IsOpen => !_isClosed;

    public TimedLevelShelfLayout(IReadOnlyList<ColumnStateSnapshot> columns) : this(columns, true)
    {
    }

    public TimedLevelShelfLayout(IReadOnlyList<ColumnStateSnapshot> columns, bool isOpen)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        foreach (ColumnStateSnapshot column in columns)
            _columns.Add(new TimedLevelColumnLayout(column.Items));

        _isClosed = !isOpen;
    }
}
