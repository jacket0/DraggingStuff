using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TimedLevelShelfLayout
{
    [SerializeField] private List<TimedLevelColumnLayout> _columns = new List<TimedLevelColumnLayout>();
    [SerializeField] private bool _isClosed;
    [SerializeField] private bool _isConveyor;
    [SerializeField] private List<ItemType> _acceptedTypes = new List<ItemType>();

    public IReadOnlyList<TimedLevelColumnLayout> Columns => _columns;
    public bool IsOpen => !_isClosed;
    public bool IsConveyor => _isConveyor;
    public IReadOnlyList<ItemType> AcceptedTypes => _acceptedTypes;
    public bool IsFiltered => _acceptedTypes.Count > 0;

    public TimedLevelShelfLayout(IReadOnlyList<ColumnStateSnapshot> columns) : this(columns, true)
    {
    }

    public TimedLevelShelfLayout(IReadOnlyList<ColumnStateSnapshot> columns, bool isOpen) : this(columns, isOpen, false)
    {
    }

    public TimedLevelShelfLayout(IReadOnlyList<ColumnStateSnapshot> columns, bool isOpen, bool isConveyor)
        : this(columns, isOpen, isConveyor, Array.Empty<ItemType>())
    {
    }

    public TimedLevelShelfLayout(IReadOnlyList<ColumnStateSnapshot> columns, bool isOpen, bool isConveyor, IReadOnlyList<ItemType> acceptedTypes)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        if (acceptedTypes == null)
            throw new ArgumentNullException(nameof(acceptedTypes));

        foreach (ColumnStateSnapshot column in columns)
            _columns.Add(new TimedLevelColumnLayout(column.Items));

        _isClosed = !isOpen;
        _isConveyor = isConveyor;
        _acceptedTypes.AddRange(acceptedTypes);
    }
}
