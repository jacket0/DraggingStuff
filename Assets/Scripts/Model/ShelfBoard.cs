using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShelfBoard : MonoBehaviour
{
    [SerializeField] private List<Shelf> _shelves;

    public IReadOnlyList<Shelf> Shelves => _shelves;
    public bool IsCleared => _shelves.All(shelf => shelf.IsCleared);

    public MoveOutcome TryMove(ShelfSlot source, ShelfSlot target)
    {
        if (source == null || target == null)
            return MoveOutcome.Rejected();

        if (source.IsEmpty || target.IsEmpty == false)
            return MoveOutcome.Rejected();

        if (source == target)
            return MoveOutcome.Rejected();

        var sourceShelf = source.GetComponentInParent<Shelf>();
        var targetShelf = target.GetComponentInParent<Shelf>();

        var item = source.TakeItem();
        target.PlaceItem(item);

        sourceShelf.TryRevealNextLayer();

        bool hasMatch = targetShelf.TryResolveMatch();

        if (hasMatch)
            targetShelf.TryRevealNextLayer();

        return MoveOutcome.Successful(hasMatch, IsCleared);
    }
}
