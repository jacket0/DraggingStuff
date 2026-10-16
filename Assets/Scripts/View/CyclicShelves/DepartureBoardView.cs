using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DepartureBoardView : MonoBehaviour
{
    [SerializeField] private List<DepartureBoardSlotView> _slots = new List<DepartureBoardSlotView>();

    private void Awake()
    {
        if (_slots.Count == 0 || _slots.Contains(null) || _slots.Select(slot => slot.Shelf).Distinct().Count() != _slots.Count)
            throw new InvalidOperationException($"{name}: every departure board slot must belong to its own shelf.");
    }

    public bool HasSlot(Shelf shelf) => _slots.Any(slot => slot.Shelf == shelf);

    public void ShowLevel(ShelfBoard board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        foreach (DepartureBoardSlotView slot in _slots)
        {
            if (board.IsCyclic(slot.Shelf))
                slot.Show(board.GetCycle(slot.Shelf), board.IsCyclePhaseOpen(slot.Shelf), false);
            else
                slot.HideForLevel();
        }
    }

    public bool Refresh(ShelfBoard board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        bool isChanged = false;

        foreach (DepartureBoardSlotView slot in _slots.Where(slot => board.IsCyclic(slot.Shelf)))
            isChanged |= slot.Show(board.GetCycle(slot.Shelf), board.IsCyclePhaseOpen(slot.Shelf), true);

        return isChanged;
    }

    public string GetText(Shelf shelf) => _slots.First(slot => slot.Shelf == shelf).Text;
}
