using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class ShelfFilterGenerationPreview
{
    public const int PreviewSeedLevelNumber = 9;
    public const int RequiredVariantCount = 12;

    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string FilterSceneName = "ThirdLevel";
    private const int BoardShelfCount = 11;
    private const int ShelfCapacity = 3;
    private const int FilteredShelfIndex = 5;

    [MenuItem("Tools/Timed Levels/Filter Generation Preview")]
    public static void Run()
    {
        TimedLevelDefinition definition = CreatePreviewDefinition();

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Dictionary<TimedLevelVariantGenerator.CandidateRejection, int> rejections = new Dictionary<TimedLevelVariantGenerator.CandidateRejection, int>();
            List<TimedLevelVariant> candidates = TimedLevelVariantGenerator.GenerateCandidates(definition, PreviewSeedLevelNumber, CreateBoardShape(), rejections);
            stopwatch.Stop();

            foreach (TimedLevelVariant candidate in candidates)
                ValidateFilteredPlacements(candidate);

            int medianMoveCount = TimedLevelVariantGenerator.GetMedian(candidates.Select(candidate => candidate.MoveCount));
            int acceptedCount = candidates.Count(candidate => Math.Abs(candidate.MoveCount - medianMoveCount) <= medianMoveCount * 0.1d);
            int[] filteredMoveCounts = candidates.Select(TimedLevelVariantGenerator.CountFilteredMoves).ToArray();
            int[] filteredEmptyColumnCounts = candidates.Select(candidate => TimedLevelLayoutRules.CountFilteredEmptyColumns(candidate.CreateLayout())).ToArray();
            int junkLayoutCount = candidates.Count(candidate => HasJunkOnFilters(candidate.CreateLayout()));

            Debug.Log(
                $"SHELF_FILTER_GENERATION_PREVIEW: {candidates.Count} candidates in {stopwatch.Elapsed.TotalSeconds:F1} s, " +
                $"{acceptedCount} within 10% of median {medianMoveCount} moves, " +
                $"filtered moves median {TimedLevelVariantGenerator.GetMedian(filteredMoveCounts)} (min {filteredMoveCounts.Min()}, max {filteredMoveCounts.Max()}, " +
                $"at minimum {filteredMoveCounts.Count(count => count == definition.MinimumFilteredMoveCount)}), " +
                $"filtered empty columns at start {FormatDistribution(filteredEmptyColumnCounts)}, " +
                $"junk on filters in {junkLayoutCount}/{candidates.Count} layouts, " +
                $"rejected: {TimedLevelVariantGenerator.FormatRejections(rejections)}.");

            if (acceptedCount < RequiredVariantCount)
                throw new InvalidOperationException($"Filter preview: only {acceptedCount} variants are within 10% of median {medianMoveCount}.");

            Debug.Log("SHELF_FILTER_GENERATION_PREVIEW_PASS");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    public static TimedLevelDefinition CreatePreviewDefinition()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        ShelfItemCatalog itemCatalog = catalog.Levels.First(level => level.SceneName == FilterSceneName).Definition.ItemCatalog;
        TimedLevelDefinition definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
        definition.name = "ShelfFilterPreview";
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = 75;
        serializedDefinition.FindProperty("_twoStarTimeSeconds").intValue = 60;
        serializedDefinition.FindProperty("_threeStarTimeSeconds").intValue = 40;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = 3;
        serializedDefinition.FindProperty("_itemCatalog").objectReferenceValue = itemCatalog;
        SetItemGroups(
            serializedDefinition.FindProperty("_itemGroups"),
            (ItemType.Ball, 3),
            (ItemType.Bear, 3),
            (ItemType.Plant, 3),
            (ItemType.Lamp, 3),
            (ItemType.MapBall, 2),
            (ItemType.Beauty, 2));
        SerializedProperty filtersProperty = serializedDefinition.FindProperty("_shelfFilters");
        filtersProperty.arraySize = 1;
        SerializedProperty filterProperty = filtersProperty.GetArrayElementAtIndex(0);
        filterProperty.FindPropertyRelative("_shelfIndex").intValue = FilteredShelfIndex;
        SerializedProperty acceptedTypesProperty = filterProperty.FindPropertyRelative("_acceptedTypes");
        acceptedTypesProperty.arraySize = 1;
        acceptedTypesProperty.GetArrayElementAtIndex(0).enumValueIndex = (int)ItemType.Ball;
        serializedDefinition.FindProperty("_filteredEmptyColumnCount").intValue = 1;
        serializedDefinition.FindProperty("_minimumFilteredMoveCount").intValue = 2;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    public static BoardStateSnapshot CreateBoardShape()
    {
        return new BoardStateSnapshot(Enumerable.Range(0, BoardShelfCount)
            .Select(_ => new ShelfStateSnapshot(Enumerable.Range(0, ShelfCapacity).Select(__ => new ColumnStateSnapshot(Array.Empty<ItemType>())).ToArray()))
            .ToArray());
    }

    public static bool HasJunkOnFilters(BoardStateSnapshot layout)
    {
        return layout.Shelves
            .Where(shelf => shelf.IsFiltered)
            .Any(shelf => shelf.Columns.SelectMany(column => column.Items).Any(type => !shelf.AcceptedTypes.Contains(type)));
    }

    private static void ValidateFilteredPlacements(TimedLevelVariant variant)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = variant.CreateLayout();

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            ShelfStateSnapshot sourceShelf = state.Shelves[move.Source.ShelfIndex];
            ShelfStateSnapshot targetShelf = state.Shelves[move.Target.ShelfIndex];
            ItemType movedType = sourceShelf.Columns[move.Source.ColumnIndex].Items[0];
            ColumnStateSnapshot targetColumn = targetShelf.Columns[move.Target.ColumnIndex];

            if (targetShelf.IsFiltered && !targetShelf.AcceptedTypes.Contains(movedType))
                throw new InvalidOperationException($"Filter preview: seed {variant.Seed} places {movedType} on filtered shelf {move.Target.ShelfIndex}.");

            if (!targetColumn.IsEmpty && sourceShelf.IsFiltered && !sourceShelf.AcceptedTypes.Contains(targetColumn.Items[0]))
                throw new InvalidOperationException($"Filter preview: seed {variant.Seed} swaps {targetColumn.Items[0]} onto filtered shelf {move.Source.ShelfIndex}.");

            if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation))
                throw new InvalidOperationException($"Filter preview: seed {variant.Seed} solution does not replay.");

            state = simulation.State;
        }

        if (!state.IsCleared)
            throw new InvalidOperationException($"Filter preview: seed {variant.Seed} solution does not clear the board.");
    }

    private static void SetItemGroups(SerializedProperty groupsProperty, params (ItemType Type, int Count)[] groups)
    {
        groupsProperty.arraySize = groups.Length;

        for (int index = 0; index < groups.Length; index++)
        {
            SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(index);
            groupProperty.FindPropertyRelative("_type").enumValueIndex = (int)groups[index].Type;
            groupProperty.FindPropertyRelative("_groupCount").intValue = groups[index].Count;
        }
    }

    private static string FormatDistribution(IEnumerable<int> values)
    {
        return string.Join(" ", values.GroupBy(value => value).OrderBy(group => group.Key).Select(group => $"{group.Key}:{group.Count()}"));
    }
}
