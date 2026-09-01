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
        return TryGetValidMoveShelves(source, target, out Shelf sourceShelf, out Shelf targetShelf)
            && IsMoveSafe(source, target, sourceShelf, targetShelf);
    }

    public MoveOutcome TryMove(ShelfSlot source, ShelfSlot target)
    {
        if (!TryGetValidMoveShelves(source, target, out Shelf sourceShelf, out Shelf targetShelf))
            return MoveOutcome.Rejected();

        if (!IsMoveSafe(source, target, sourceShelf, targetShelf))
            return MoveOutcome.Rejected();

        ShelfItem item = source.TakeItem();
        target.PlaceItem(item);

        targetShelf.TryResolveMatch(out MatchResolution match);

        List<Shelf> advancingShelves = new List<Shelf>(2);

        if (sourceShelf.CanRevealNextLayer)
            advancingShelves.Add(sourceShelf);

        if (targetShelf != sourceShelf && targetShelf.CanRevealNextLayer)
            advancingShelves.Add(targetShelf);

        return MoveOutcome.Successful(match, advancingShelves);
    }

    public bool TryResolveActiveMatch(out Shelf matchedShelf, out MatchResolution match)
    {
        foreach (Shelf shelf in _shelves)
        {
            if (!shelf.TryResolveMatch(out match))
                continue;

            matchedShelf = shelf;
            return true;
        }

        matchedShelf = null;
        match = null;
        return false;
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

    public IReadOnlyList<Shelf> GetShelvesReadyToAdvance()
    {
        return _shelves.Where(shelf => shelf.CanRevealNextLayer).ToArray();
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

    private bool IsMoveSafe(ShelfSlot source, ShelfSlot target, Shelf sourceShelf, Shelf targetShelf)
    {
        List<List<ItemType?[]>> projectedShelves = CreateProjection();
        int sourceShelfIndex = _shelves.IndexOf(sourceShelf);
        int targetShelfIndex = _shelves.IndexOf(targetShelf);
        int sourceSlotIndex = GetSlotIndex(sourceShelf.ActiveLayer, source);
        int targetSlotIndex = GetSlotIndex(targetShelf.ActiveLayer, target);
        ItemType?[] sourceLayer = projectedShelves[sourceShelfIndex][0];
        ItemType?[] targetLayer = projectedShelves[targetShelfIndex][0];

        targetLayer[targetSlotIndex] = sourceLayer[sourceSlotIndex];
        sourceLayer[sourceSlotIndex] = null;

        bool createdMatch = ResolveProjection(projectedShelves);

        if (createdMatch)
            return true;

        return CountActiveEmptySlots(projectedShelves) > 0;
    }

    private List<List<ItemType?[]>> CreateProjection()
    {
        List<List<ItemType?[]>> projectedShelves = new List<List<ItemType?[]>>(_shelves.Count);

        foreach (Shelf shelf in _shelves)
        {
            List<ItemType?[]> layers = new List<ItemType?[]>(shelf.Layers.Count);

            foreach (ShelfLayer layer in shelf.Layers)
            {
                ItemType?[] items = new ItemType?[layer.Capacity];

                for (int slotIndex = 0; slotIndex < layer.Slots.Count; slotIndex++)
                {
                    ShelfSlot slot = layer.Slots[slotIndex];
                    items[slotIndex] = slot.IsEmpty ? null : slot.Item.Type;
                }

                layers.Add(items);
            }

            projectedShelves.Add(layers);
        }

        return projectedShelves;
    }

    private static bool ResolveProjection(List<List<ItemType?[]>> shelves)
    {
        bool createdMatch = false;
        bool changed;

        do
        {
            changed = false;

            foreach (List<ItemType?[]> shelf in shelves)
            {
                while (shelf.Count > 1 && IsEmpty(shelf[0]))
                {
                    shelf.RemoveAt(0);
                    changed = true;
                }

                if (shelf.Count == 0 || !HasMatch(shelf[0]))
                    continue;

                Array.Clear(shelf[0], 0, shelf[0].Length);
                createdMatch = true;
                changed = true;
            }
        }
        while (changed);

        return createdMatch;
    }

    private static int CountActiveEmptySlots(IEnumerable<List<ItemType?[]>> shelves)
    {
        int emptySlotCount = 0;

        foreach (List<ItemType?[]> shelf in shelves)
        {
            if (shelf.Count == 0)
                continue;

            emptySlotCount += shelf[0].Count(item => !item.HasValue);
        }

        return emptySlotCount;
    }

    private static bool HasMatch(ItemType?[] layer)
    {
        if (layer.Length < Shelf.MinimumMatchCapacity || !layer[0].HasValue)
            return false;

        ItemType itemType = layer[0].Value;
        return layer.All(item => item.HasValue && item.Value == itemType);
    }

    private static bool IsEmpty(ItemType?[] layer)
    {
        return layer.All(item => !item.HasValue);
    }

    private static int GetSlotIndex(ShelfLayer layer, ShelfSlot slot)
    {
        for (int index = 0; index < layer.Slots.Count; index++)
        {
            if (layer.Slots[index] == slot)
                return index;
        }

        throw new InvalidOperationException(nameof(slot));
    }
}
