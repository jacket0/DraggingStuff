using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShelfBoard : MonoBehaviour
{
    [SerializeField] private List<Shelf> _shelves;

    public IReadOnlyList<Shelf> Shelves => _shelves;
    public bool IsCleared => _shelves.All(shelf => shelf.IsCleared);

    public void InitializeViews()
    {
        foreach (var shelf in _shelves)
            shelf.InitializeView();
    }

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
        targetShelf.TryResolveMatch(out MatchResolution match);

        List<Shelf> advancingShelves = new List<Shelf>(2);

        if (sourceShelf.CanRevealNextLayer)
            advancingShelves.Add(sourceShelf);

        if (targetShelf != sourceShelf && targetShelf.CanRevealNextLayer)
            advancingShelves.Add(targetShelf);

        return MoveOutcome.Successful(match, IsCleared, advancingShelves);
    }

    public void AdvanceLayers(IReadOnlyList<Shelf> shelves, Action completed)
    {
        if (shelves == null)
            throw new ArgumentNullException(nameof(shelves));

        if (shelves.Count == 0)
        {
            completed?.Invoke();
            return;
        }

        int remainingTransitions = shelves.Count;

        void HandleTransitionCompleted()
        {
            remainingTransitions--;

            if (remainingTransitions == 0)
                completed?.Invoke();
        }

        foreach (Shelf shelf in shelves)
        {
            shelf.RevealNextLayer(HandleTransitionCompleted);
        }
    }
}
