using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShelfLayer : MonoBehaviour
{
    private const int CellsCount = 3;

    [SerializeField] private List<ShelfSlot> _slots;

    public IReadOnlyList<ShelfSlot> Slots => _slots;
    public bool IsEmpty => _slots.All(slot => slot.IsEmpty);

    public bool HasMatch()
    {
        if (_slots.Count != CellsCount)
            throw new InvalidOperationException(nameof(_slots));

        if (IsEmpty)
            return false;

        if (_slots.Any(slot => slot.IsEmpty))
            return false;

        ItemType itemType = _slots[0].Item.Type;
        return _slots.All(slot => slot.Item.Type == itemType);
    }

    public void ReleaseItems()
    {
        if (!HasMatch())
            throw new InvalidOperationException();

        foreach (var slot in Slots)
        {
            var item = slot.TakeItem();
            item.Delete();
        }
    }
}
