using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShelfBoard : MonoBehaviour
{
    [SerializeField] private List<Shelf> _shelves;

    public IReadOnlyList<Shelf> Shelves => _shelves;
    public bool IsCleared => _shelves.All(shelf => shelf.IsCleared);

    public MoveOutcome TryMove(ShelfSlot source, ShelfSlot target, Action layerTransitionsCompleted)
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
        bool hasMatch = targetShelf.TryResolveMatch();

        List<Shelf> advancingShelves = new List<Shelf>(2);

        if (sourceShelf.CanRevealNextLayer)
            advancingShelves.Add(sourceShelf);

        if (targetShelf != sourceShelf && targetShelf.CanRevealNextLayer)
            advancingShelves.Add(targetShelf);

        bool hasLayerTransition = advancingShelves.Count > 0;

        if (hasLayerTransition)
            StartLayerTransitions(advancingShelves, layerTransitionsCompleted);

        return MoveOutcome.Successful(hasMatch, IsCleared, hasLayerTransition);
    }

    private void StartLayerTransitions(IReadOnlyList<Shelf> shelves, Action completed)
    {
        int remainingTransitions = shelves.Count;

        void HandleTransitionCompleted()
        {
            remainingTransitions--;

            if (remainingTransitions == 0)
                completed?.Invoke();
        }

        foreach (Shelf shelf in shelves)
        {
            shelf.RevealNextLayer(
                HandleTransitionCompleted);
        }
    }
}
