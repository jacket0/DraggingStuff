using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TimedLevelLayoutMaterializer
{
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const int RequiredVariantCount = 12;

    [MenuItem("Tools/Timed Levels/Materialize Existing Layouts")]
    public static void Run()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null)
            throw new InvalidOperationException(CatalogPath);

        int variantCount = 0;

        foreach (LevelEntry level in catalog.Levels)
        {
            List<TimedLevelVariant> variants = RecolorVariants(level);
            WriteVariants(level.Definition, variants);
            variantCount += variants.Count;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"TIMED_LEVEL_LAYOUT_MATERIALIZATION_PASS: {variantCount}");
    }

    private static List<TimedLevelVariant> RecolorVariants(LevelEntry level)
    {
        if (level.Definition == null)
            throw new InvalidOperationException($"Level {level.Number}: definition is missing.");

        if (level.Definition.Variants.Count != RequiredVariantCount)
            throw new InvalidOperationException($"Level {level.Number}: expected {RequiredVariantCount} stored variants.");

        TimedLevelVariantRecolorer recolorer = new TimedLevelVariantRecolorer();
        List<TimedLevelVariant> variants = level.Definition.Variants
            .Select(variant => recolorer.Recolor(level.Definition, variant))
            .ToList();

        return variants;
    }

    private static void WriteVariants(TimedLevelDefinition definition, IReadOnlyList<TimedLevelVariant> variants)
    {
        SerializedObject data = new SerializedObject(definition);
        SerializedProperty variantsProperty = data.FindProperty("_variants");
        variantsProperty.arraySize = variants.Count;

        for (int index = 0; index < variants.Count; index++)
        {
            TimedLevelVariant variant = variants[index];
            SerializedProperty property = variantsProperty.GetArrayElementAtIndex(index);
            property.FindPropertyRelative("_seed").intValue = variant.Seed;
            property.FindPropertyRelative("_generatorVersion").intValue = variant.GeneratorVersion;
            property.FindPropertyRelative("_moveCount").intValue = variant.MoveCount;
            property.FindPropertyRelative("_layoutHash").stringValue = variant.LayoutHash;
            WriteLayout(property.FindPropertyRelative("_shelves"), variant.CreateLayout());
            WriteSolution(property.FindPropertyRelative("_solutionMoves"), variant.CreateSolution());
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static void WriteLayout(SerializedProperty shelvesProperty, BoardStateSnapshot layout)
    {
        shelvesProperty.arraySize = layout.Shelves.Count;

        for (int shelfIndex = 0; shelfIndex < layout.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = layout.Shelves[shelfIndex];
            SerializedProperty columns = shelvesProperty.GetArrayElementAtIndex(shelfIndex).FindPropertyRelative("_columns");
            columns.arraySize = shelf.Columns.Count;

            for (int columnIndex = 0; columnIndex < shelf.Columns.Count; columnIndex++)
            {
                ColumnStateSnapshot column = shelf.Columns[columnIndex];
                SerializedProperty items = columns.GetArrayElementAtIndex(columnIndex).FindPropertyRelative("_items");
                items.arraySize = column.Items.Count;

                for (int itemIndex = 0; itemIndex < column.Items.Count; itemIndex++)
                    items.GetArrayElementAtIndex(itemIndex).enumValueIndex = (int)column.Items[itemIndex];
            }
        }
    }

    private static void WriteSolution(SerializedProperty movesProperty, IReadOnlyList<TimedLevelMove> moves)
    {
        movesProperty.arraySize = moves.Count;

        for (int index = 0; index < moves.Count; index++)
        {
            TimedLevelMove move = moves[index];
            SerializedProperty property = movesProperty.GetArrayElementAtIndex(index);
            property.FindPropertyRelative("_sourceShelfIndex").intValue = move.Source.ShelfIndex;
            property.FindPropertyRelative("_sourceColumnIndex").intValue = move.Source.ColumnIndex;
            property.FindPropertyRelative("_targetShelfIndex").intValue = move.Target.ShelfIndex;
            property.FindPropertyRelative("_targetColumnIndex").intValue = move.Target.ColumnIndex;
        }
    }

}
