using System.Collections.Generic;
using UnityEngine;

public class ShelfBoard : MonoBehaviour
{
    [SerializeField] private List<Shelf> _shelves;

    public IReadOnlyList<Shelf> Shelves => _shelves;

    public bool TryMove(ShelfSlot source, ShelfSlot target)
    {
        if (source.IsEmpty || target.IsEmpty == false)
            return false;

        if (source == target)
            return false;

        var sourceShelf = source.GetComponentInParent<Shelf>();
        var targetShelf = target.GetComponentInParent<Shelf>();

        var item = source.TakeItem();
        target.PlaceItem(item);

        sourceShelf.TryResolveMatch();

        if (sourceShelf != targetShelf)
            targetShelf.TryResolveMatch();

        return true;
    }
}
