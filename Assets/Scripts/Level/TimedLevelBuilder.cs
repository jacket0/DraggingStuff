using System;
using System.Linq;
using UnityEngine;

public sealed class TimedLevelBuilder : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private ShelfKey _keyPrefab;

    public TimedLevelBuildResult Build(int levelNumber, TimedLevelDefinition definition)
    {
        if (levelNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelNumber));

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        _shelfBoard.Initialize();
        BoardStateSnapshot boardShape = _shelfBoard.CreateSnapshot();

        if (!boardShape.IsCleared)
            throw new InvalidOperationException($"Level {levelNumber}: the scene board must be empty before building.");

        TimedLevelValidator.ValidateForRuntime(definition, boardShape, TimedLevelLayoutRules.GeneratorVersion);
        int variantIndex = TimedLevelVariantSelector.Select(levelNumber, definition.Variants.Count);
        TimedLevelVariant variant = definition.Variants[variantIndex];
        BoardStateSnapshot layout = variant.CreateLayout();
        string layoutHash = BoardStateFingerprint.CreateHash(layout);

        if (!string.Equals(layoutHash, variant.LayoutHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Level {levelNumber}, variant {variantIndex + 1}: layout hash {layoutHash} does not match {variant.LayoutHash}.");
        }

        Materialize(definition, layout);
        _shelfBoard.InitializeViews();

#if DEVELOPMENT_BUILD
        Debug.Log($"Timed level {levelNumber}: seed={variant.Seed}, variant={variantIndex + 1}, hash={layoutHash}");
#endif

        return new TimedLevelBuildResult(variant.Seed, variantIndex, layoutHash);
    }

    private void Materialize(TimedLevelDefinition definition, BoardStateSnapshot layout)
    {
        if (layout.HasKeys && _keyPrefab == null)
            throw new InvalidOperationException($"{gameObject.scene.name}: {nameof(_keyPrefab)} is required for a level with keys.");

        for (int shelfIndex = 0; shelfIndex < layout.Shelves.Count; shelfIndex++)
        {
            Shelf shelf = _shelfBoard.Shelves[shelfIndex];

            for (int columnIndex = 0; columnIndex < layout.Shelves[shelfIndex].Capacity; columnIndex++)
            {
                ColumnStateSnapshot columnLayout = layout.Shelves[shelfIndex].Columns[columnIndex];
                ShelfColumn column = shelf.Columns[columnIndex];

                foreach (ItemType type in columnLayout.Items)
                {
                    ShelfItem prefab = definition.ItemCatalog.GetPrefab(type);

                    if (prefab == null || prefab.GetComponent<ShelfItemPresentation>() == null)
                        throw new InvalidOperationException($"Shelf {shelfIndex}, column {columnIndex}: {type} prefab is invalid.");

                    column.Append(Instantiate(prefab));
                }

                foreach (ColumnKeySnapshot key in columnLayout.Keys)
                    column.AddKey(CreateKey(key.LockedShelfIndex), key.ItemsInFront);
            }

            if (definition.KeyLockedShelfIndices.Contains(shelfIndex))
                _shelfBoard.CloseKeyLockedShelf(shelf);
            else if (!layout.Shelves[shelfIndex].IsOpen)
                _shelfBoard.CloseShelf(shelf);

            if (layout.Shelves[shelfIndex].IsConveyor)
                _shelfBoard.MarkConveyor(shelf);

            if (layout.Shelves[shelfIndex].IsFiltered)
                _shelfBoard.SetFilter(shelf, layout.Shelves[shelfIndex].AcceptedTypes);
        }
    }

    private ShelfKey CreateKey(int lockedShelfIndex)
    {
        ShelfKey key = Instantiate(_keyPrefab);
        key.LockedShelf = _shelfBoard.Shelves[lockedShelfIndex];
        return key;
    }
}
