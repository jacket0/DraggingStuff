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
        foreach (Shelf shelf in _shelves)
            shelf.InitializeView();
    }

    public bool CanMove(ShelfSlot source, ShelfSlot target)
    {
        return TryGetValidMoveShelves(source, target, out _, out _);
    }

    public MoveOutcome TryMove(ShelfSlot source, ShelfSlot target)
    {
        if (!TryGetValidMoveShelves(source, target, out Shelf sourceShelf, out Shelf targetShelf))
            return MoveOutcome.Rejected();

        ShelfItem item = source.TakeItem();
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
            shelf.RevealNextLayer(HandleTransitionCompleted);
    }

    public void HideActiveLayers(IReadOnlyList<Shelf> shelves)
    {
        if (shelves == null)
            throw new ArgumentNullException(nameof(shelves));

        foreach (Shelf shelf in shelves)
        {
            if (shelf == null)
                throw new InvalidOperationException(nameof(shelf));

            shelf.HideActiveLayer();
        }
    }

    private bool TryGetValidMoveShelves(ShelfSlot source, ShelfSlot target, out Shelf sourceShelf, out Shelf targetShelf)
    {
        sourceShelf = null;
        targetShelf = null;

        if (source == null || target == null || source == target)
            return false;

        if (source.IsEmpty || !target.IsEmpty)
            return false;

        if (!TryGetOwningShelf(source, out sourceShelf))
            return false;

        if (!TryGetOwningShelf(target, out targetShelf))
            return false;

        if (!sourceShelf.IsContainsActiveSlot(source))
            return false;

        if (!targetShelf.IsContainsActiveSlot(target))
            return false;

        return true;
    }

    private bool TryGetOwningShelf(ShelfSlot slot, out Shelf shelf)
    {
        shelf = slot != null ? slot.GetComponentInParent<Shelf>() : null;
        return shelf != null && _shelves.Contains(shelf);
    }
}