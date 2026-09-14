using System;
using UnityEngine;

public sealed class LevelBuilder : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;

    public void Build(LevelDefinition levelDefinition)
    {
        if (levelDefinition == null)
            throw new ArgumentNullException(nameof(levelDefinition));

        _shelfBoard.Initialize();

        if (_shelfBoard.Shelves.Count != levelDefinition.Shelves.Count)
            throw new InvalidOperationException("Scene and level shelf counts do not match.");

        for (int shelfIndex = 0; shelfIndex < _shelfBoard.Shelves.Count; shelfIndex++)
        {
            Shelf shelf = _shelfBoard.Shelves[shelfIndex];
            ShelfDefinition definition = levelDefinition.Shelves[shelfIndex];

            if (definition == null || definition.Columns.Count != shelf.Capacity)
                throw new InvalidOperationException($"Shelf {shelfIndex}: scene and level column counts do not match.");

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                ShelfColumnDefinition column = definition.Columns[columnIndex];

                if (column == null || !shelf.Columns[columnIndex].IsEmpty)
                    throw new InvalidOperationException($"Shelf {shelfIndex}, column {columnIndex}: invalid or occupied column.");

                foreach (ShelfItem prefab in column.ItemPrefabs)
                {
                    if (prefab == null || prefab.GetComponent<ShelfItemPresentation>() == null)
                        throw new InvalidOperationException($"Shelf {shelfIndex}, column {columnIndex}: missing item prefab or presentation.");
                }
            }
        }

        for (int shelfIndex = 0; shelfIndex < _shelfBoard.Shelves.Count; shelfIndex++)
        {
            Shelf shelf = _shelfBoard.Shelves[shelfIndex];

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                foreach (ShelfItem prefab in levelDefinition.Shelves[shelfIndex].Columns[columnIndex].ItemPrefabs)
                    shelf.Columns[columnIndex].Append(Instantiate(prefab));
            }
        }

        _shelfBoard.InitializeViews();
    }
}
