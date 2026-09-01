using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShelfLayer : MonoBehaviour
{
    [SerializeField] private List<ShelfSlot> _slots;

    public IReadOnlyList<ShelfSlot> Slots => _slots;
    public int Capacity => _slots?.Count ?? 0;
    public bool IsEmpty => _slots.All(slot => slot.IsEmpty);

    public bool HasMatch()
    {
        ValidateSlots();

        if (Capacity < Shelf.MinimumMatchCapacity)
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

    public void ValidateCapacity(int expectedCapacity)
    {
        if (!Shelf.IsValidCapacity(expectedCapacity))
            throw new ArgumentOutOfRangeException(nameof(expectedCapacity));

        ValidateSlots();

        if (Capacity != expectedCapacity)
            throw new InvalidOperationException(nameof(expectedCapacity));
    }

    public bool IsContainsSlot(ShelfSlot slot)
    {
        return slot != null && _slots.Contains(slot);
    }

    private void ValidateSlots()
    {
        if (_slots == null || !Shelf.IsValidCapacity(_slots.Count))
            throw new InvalidOperationException(nameof(_slots));

        HashSet<ShelfSlot> uniqueSlots = new HashSet<ShelfSlot>();

        foreach (ShelfSlot slot in _slots)
        {
            if (slot == null || !slot.HasItemAnchor || !uniqueSlots.Add(slot))
                throw new InvalidOperationException(nameof(_slots));
        }
    }
}
