using System;
using UnityEngine;

public class LevelBuilder : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;

    public void Build(LevelDefinition levelDefinition)
    {
        if (levelDefinition == null)
            throw new ArgumentNullException(nameof(levelDefinition));

        ValidateConfig(levelDefinition);
        FillBoard(levelDefinition);
    }

    private void ValidateConfig(LevelDefinition levelDefinition)
    {
        if (_shelfBoard == null)
            throw new InvalidOperationException();

        if (levelDefinition == null)
            throw new InvalidOperationException();

        if (_shelfBoard.Shelves.Count != levelDefinition.Shelves.Count)
            throw new InvalidOperationException($"Количество полок не совпадает. В сцене: {_shelfBoard.Shelves.Count}, в конфиге: {levelDefinition.Shelves.Count}.");

        for (int i = 0; i < _shelfBoard.Shelves.Count; i++)
            ValidateShelf(_shelfBoard.Shelves[i], levelDefinition.Shelves[i], i);
    }

    private void ValidateShelf(Shelf shelf, ShelfDefinition definition, int shelfIndex)
    {
        if (shelf.Layers.Count != definition.Layers.Count)
            throw new InvalidOperationException($"Полка {shelfIndex}: в сцене {shelf.Layers.Count} слоёв, в конфиге {definition.Layers.Count}.");

        for (int layerIndex = 0; layerIndex < shelf.Layers.Count; layerIndex++)
            ValidateLayer(shelf.Layers[layerIndex], definition.Layers[layerIndex], shelfIndex, layerIndex);
    }

    private void ValidateLayer(ShelfLayer layer, ShelfLayerDefinition definition, int shelfIndex, int layerIndex)
    {
        if (layer.Slots.Count != definition.ItemPrefabs.Count)
            throw new InvalidOperationException($"Полка {shelfIndex}, слой {layerIndex}: в сцене {layer.Slots.Count} слотов, в конфиге {definition.ItemPrefabs.Count}.");

        if (layer.Slots.Count != ShelfLayer.SlotCount)
            throw new InvalidOperationException($"Полка {shelfIndex}, слой {layerIndex}: требуется {ShelfLayer.SlotCount} слота, найдено {layer.Slots.Count}.");

        for (int slotIndex = 0; slotIndex < layer.Slots.Count; slotIndex++)
            ValidateSlot(layer.Slots[slotIndex], shelfIndex, layerIndex, slotIndex);
    }

    private void ValidateSlot(ShelfSlot slot, int shelfIndex, int layerIndex, int slotIndex)
    {
        if (slot == null)
            throw new InvalidOperationException($"В шкафу {shelfIndex}, в слое {layerIndex}, слот {slotIndex} пуст.");

        if (!slot.IsEmpty)
            throw new InvalidOperationException($"В шкафу {shelfIndex}, в слое {layerIndex}, слот {slotIndex} уже содержит предмет.");
    }

    private void FillBoard(LevelDefinition levelDefinition)
    {
        for (int i = 0; i < _shelfBoard.Shelves.Count; i++)
        {
            FillShelf(_shelfBoard.Shelves[i], levelDefinition.Shelves[i]);
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
