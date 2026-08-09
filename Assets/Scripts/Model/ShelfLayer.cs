using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShelfLayer : MonoBehaviour
{
    public const int SlotCount = 3;

    [SerializeField] private List<ShelfSlot> _slots;

    public IReadOnlyList<ShelfSlot> Slots => _slots;
    public bool IsEmpty => _slots.All(slot => slot.IsEmpty);

    public bool HasMatch()
    {
        if (_slots.Count != SlotCount)
            throw new InvalidOperationException(nameof(_slots));

        if (IsEmpty)
            return false;

        if (_slots.Any(slot => slot.IsEmpty))
            return false;

        ItemType itemType = _slots[0].Item.Type;
        return _slots.All(slot => slot.Item.Type == itemType);
    }

    public MatchResolution TakeMatch()
    {
        if (!HasMatch())
            throw new InvalidOperationException();

        ShelfItem[] items = _slots.Select(slot => slot.TakeItem()).ToArray(); 

        return new MatchResolution(items);
    }
}
