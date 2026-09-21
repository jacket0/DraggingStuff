using System;
using UnityEngine;

public sealed class TimedLevelBuilder : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;

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

        Materialize(definition.ItemCatalog, layout);
        _shelfBoard.InitializeViews();

#if DEVELOPMENT_BUILD
        Debug.Log($"Timed level {levelNumber}: seed={variant.Seed}, variant={variantIndex + 1}, hash={layoutHash}");
#endif

        return new TimedLevelBuildResult(variant.Seed, variantIndex, layoutHash);
    }

    private void Materialize(ShelfItemCatalog catalog, BoardStateSnapshot layout)
    {
        for (int shelfIndex = 0; shelfIndex < layout.Shelves.Count; shelfIndex++)
        {
            Shelf shelf = _shelfBoard.Shelves[shelfIndex];

            for (int columnIndex = 0; columnIndex < layout.Shelves[shelfIndex].Capacity; columnIndex++)
            {
                foreach (ItemType type in layout.Shelves[shelfIndex].Columns[columnIndex].Items)
                {
                    ShelfItem prefab = catalog.GetPrefab(type);

                    if (prefab == null || prefab.GetComponent<ShelfItemPresentation>() == null)
                        throw new InvalidOperationException($"Shelf {shelfIndex}, column {columnIndex}: {type} prefab is invalid.");

                    shelf.Columns[columnIndex].Append(Instantiate(prefab));
                }
            }
        }
    }
}
