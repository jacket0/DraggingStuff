using System;
using UnityEngine;

public class LevelBuilder : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private LevelDefinition _levelDefinition;

    public void Build()
    {
        ValidateConfig();
        FillBoard();
    }

    private void ValidateConfig()
    {
        if (_shelfBoard == null)
            throw new InvalidOperationException();

        if (_levelDefinition == null)
            throw new InvalidOperationException();

        if (_shelfBoard.Shelves.Count != _levelDefinition.Shelves.Count)
            throw new InvalidOperationException();

        for (int shelfIndex = 0;
             shelfIndex < _shelfBoard.Shelves.Count;
             shelfIndex++)
        {
            ValidateShelf(shelfIndex);
        }
    }

    private void ValidateShelf(int shelfIndex)
    {
        Shelf shelf = _shelfBoard.Shelves[shelfIndex];
        ShelfDefinition definition = _levelDefinition.Shelves[shelfIndex];

        if (shelf.Layers.Count != definition.Layers.Count)
            throw new InvalidOperationException();

        for (int layerIndex = 0; layerIndex < shelf.Layers.Count; layerIndex++)
        {
            ValidateLayer(shelfIndex, layerIndex);
        }
    }

    private void ValidateLayer(int shelfIndex, int layerIndex)
    {
        ShelfLayer layer = _shelfBoard.Shelves[shelfIndex].Layers[layerIndex];
        ShelfLayerDefinition definition = _levelDefinition.Shelves[shelfIndex].Layers[layerIndex];

        if (layer.Slots.Count != definition.ItemPrefabs.Count)
            throw new InvalidOperationException();

        if (layer.Slots.Count != ShelfLayer.SlotCount)
            throw new InvalidOperationException();

        for (int slotIndex = 0; slotIndex < layer.Slots.Count; slotIndex++)
        {
            ValidateSlot(layer.Slots[slotIndex], shelfIndex, layerIndex, slotIndex);
        }
    }

    private void ValidateSlot(ShelfSlot slot, int shelfIndex, int layerIndex, int slotIndex)
    {
        if (slot == null)
            throw new InvalidOperationException($"В шкафу {shelfIndex}, в слое {layerIndex}, слот {slotIndex} пуст.");

        if (!slot.IsEmpty)
            throw new InvalidOperationException($"В шкафу {shelfIndex}, в слое {layerIndex}, слот {slotIndex} уже содержит предмет.");
    }

    private void FillBoard()
    {
        for (int i = 0; i < _shelfBoard.Shelves.Count; i++)
        {
            FillShelf(_shelfBoard.Shelves[i], _levelDefinition.Shelves[i]);
        }
    }

    private void FillShelf(Shelf shelf, ShelfDefinition definition)
    {
        for (int i = 0; i < shelf.Layers.Count; i++)
        {
            FillLayer(shelf.Layers[i], definition.Layers[i]);
        }
    }

    private void FillLayer(ShelfLayer layer, ShelfLayerDefinition definition)
    {
        for (int i = 0; i < layer.Slots.Count; i++)
        {
            FillSlot(layer.Slots[i], definition.ItemPrefabs[i]);
        }
    }

    private void FillSlot(ShelfSlot slot, ShelfItem itemPrefab)
    {
        if (itemPrefab == null)
            return;

        ShelfItem item = Instantiate(itemPrefab);
        slot.PlaceItem(item);
    }
}
