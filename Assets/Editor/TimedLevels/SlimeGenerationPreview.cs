using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class SlimeGenerationPreview
{
    private const int StartSeedLevelNumber = 96;
    private const int SpreadSeedLevelNumber = 97;
    private const int RequiredVariantCount = 12;
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string ItemCatalogSceneName = "SixthLevel";
    public const int BoardShelfCount = 10;
    private const int TierShelfCount = 4;
    private const int ShelfCapacity = 3;
    private const int EmptyColumnCount = 3;
    private const int GrowthPeriod = 2;

    private static readonly ColumnPosition[] StartStickyColumns = { new ColumnPosition(5, 1) };
    private static readonly ColumnPosition[] SpreadStickyColumns = { new ColumnPosition(1, 0), new ColumnPosition(6, 2), new ColumnPosition(8, 1) };

    private static readonly (ItemType Type, int Count)[] Groups =
    {
        (ItemType.Ball, 3), (ItemType.Bear, 2), (ItemType.Plant, 2), (ItemType.Lamp, 2), (ItemType.MapBall, 2), (ItemType.Beauty, 2), (ItemType.Toy, 1)
    };

    [MenuItem("Tools/Timed Levels/Slime Generation Preview/Start")]
    public static void RunStart() => Run("start", StartSeedLevelNumber, StartStickyColumns);

    [MenuItem("Tools/Timed Levels/Slime Generation Preview/Spread")]
    public static void RunSpread() => Run("spread", SpreadSeedLevelNumber, SpreadStickyColumns);

    public static IReadOnlyList<ColumnPosition[]> CreatePreviewLinks()
    {
        List<ColumnPosition[]> links = new List<ColumnPosition[]>();

        for (int tierStart = 0; tierStart < TierShelfCount * 2; tierStart += TierShelfCount)
        {
            for (int shelfIndex = tierStart; shelfIndex < tierStart + TierShelfCount - 1; shelfIndex++)
                links.Add(new[] { new ColumnPosition(shelfIndex, ShelfCapacity - 1), new ColumnPosition(shelfIndex + 1, 0) });
        }

        for (int shelfIndex = 0; shelfIndex < TierShelfCount; shelfIndex++)
        {
            for (int columnIndex = 0; columnIndex < ShelfCapacity; columnIndex++)
                links.Add(new[] { new ColumnPosition(shelfIndex, columnIndex), new ColumnPosition(shelfIndex + TierShelfCount, columnIndex) });
        }

        links.Add(new[] { new ColumnPosition(8, 1), new ColumnPosition(1, 1) });
        links.Add(new[] { new ColumnPosition(9, 1), new ColumnPosition(2, 1) });
        return links;
    }

    private static void Run(string formName, int seedLevelNumber, IReadOnlyList<ColumnPosition> stickyColumns)
    {
        List<UnityEngine.Object> temporaryAssets = new List<UnityEngine.Object>();
        TimedLevelDefinition definition = CreatePreviewDefinition(formName, stickyColumns, temporaryAssets);
        string settings = $"{formName}, sticky {stickyColumns.Count}, period {GrowthPeriod}";

        try
        {
            BoardStateSnapshot shape = CreateBoardShape();
            Stopwatch stopwatch = Stopwatch.StartNew();
            Dictionary<TimedLevelVariantGenerator.CandidateRejection, int> rejections = new Dictionary<TimedLevelVariantGenerator.CandidateRejection, int>();
            List<TimedLevelVariant> candidates;

            try
            {
                candidates = TimedLevelVariantGenerator.GenerateCandidates(definition, seedLevelNumber, shape, rejections);
            }
            catch (InvalidOperationException)
            {
                Debug.Log($"SLIME_GENERATION_PREVIEW ({settings}) failed after {stopwatch.Elapsed.TotalSeconds:F1} s, rejected: {TimedLevelVariantGenerator.FormatRejections(rejections)}.");
                throw;
            }

            stopwatch.Stop();
            List<int> cleanedColumns = new List<int>();
            List<int> growths = new List<int>();
            List<int> maximumStickyColumns = new List<int>();
            List<int> stickyColumnsAtEnd = new List<int>();

            foreach (TimedLevelVariant candidate in candidates)
            {
                SlimeLayoutMetrics metrics = SlimeLayoutMetrics.Measure(definition.AddSlime(candidate.CreateLayout()), candidate.CreateSolution());

                if (!metrics.IsReplayed || metrics.HasDeadlockOnPath)
                    throw new InvalidOperationException($"Slime preview: seed {candidate.Seed} does not replay with slime.");

                cleanedColumns.Add(metrics.CleanedColumnCount);
                growths.Add(metrics.GrowthCount);
                maximumStickyColumns.Add(metrics.MaximumStickyColumnCount);
                stickyColumnsAtEnd.Add(metrics.StickyColumnsAtEnd);
            }

            int medianMoveCount = TimedLevelVariantGenerator.GetMedian(candidates.Select(candidate => candidate.MoveCount));
            List<TimedLevelVariant> accepted = candidates.Where(candidate => Math.Abs(candidate.MoveCount - medianMoveCount) <= medianMoveCount * 0.1d).ToList();

            if (accepted.Count >= RequiredVariantCount)
            {
                TimedLevelVariantGenerator.WriteVariants(definition, accepted.Take(RequiredVariantCount).ToArray());
                TimedLevelValidator.ValidateForRuntime(definition, shape, TimedLevelLayoutRules.GeneratorVersion);
            }

            Debug.Log(
                $"SLIME_GENERATION_PREVIEW ({settings}): {candidates.Count} candidates in {stopwatch.Elapsed.TotalSeconds:F1} s, " +
                $"{accepted.Count} within 10% of median {medianMoveCount} moves, " +
                $"cleaned columns {FormatDistribution(cleanedColumns)}, " +
                $"growths {FormatDistribution(growths)}, " +
                $"max sticky {FormatDistribution(maximumStickyColumns)}, " +
                $"sticky at end {FormatDistribution(stickyColumnsAtEnd)}, " +
                $"rejected: {TimedLevelVariantGenerator.FormatRejections(rejections)}.");

            if (accepted.Count < RequiredVariantCount)
                throw new InvalidOperationException($"Slime preview: only {accepted.Count} variants are within 10% of median {medianMoveCount}.");

            Debug.Log("SLIME_GENERATION_PREVIEW_PASS");
        }
        finally
        {
            foreach (UnityEngine.Object temporaryAsset in temporaryAssets)
                UnityEngine.Object.DestroyImmediate(temporaryAsset);
        }
    }

    private static TimedLevelDefinition CreatePreviewDefinition(string formName, IReadOnlyList<ColumnPosition> stickyColumns, List<UnityEngine.Object> temporaryAssets)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        ShelfItemCatalog itemCatalog = catalog.Levels.First(level => level.SceneName == ItemCatalogSceneName).Definition.ItemCatalog;
        TimedLevelDefinition definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
        definition.name = $"SlimePreview_{formName}";
        definition.hideFlags = HideFlags.HideAndDontSave;
        temporaryAssets.Add(definition);
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = 160;
        serializedDefinition.FindProperty("_twoStarTimeSeconds").intValue = 120;
        serializedDefinition.FindProperty("_threeStarTimeSeconds").intValue = 90;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = EmptyColumnCount;
        serializedDefinition.FindProperty("_itemCatalog").objectReferenceValue = itemCatalog;

        SerializedProperty groupsProperty = serializedDefinition.FindProperty("_itemGroups");
        groupsProperty.arraySize = Groups.Length;

        for (int index = 0; index < Groups.Length; index++)
        {
            SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(index);
            groupProperty.FindPropertyRelative("_type").enumValueIndex = (int)Groups[index].Type;
            groupProperty.FindPropertyRelative("_groupCount").intValue = Groups[index].Count;
        }

        SerializedProperty slime = serializedDefinition.FindProperty("_slime");
        slime.FindPropertyRelative("_neighborhood").objectReferenceValue = CreateNeighborhood(CreatePreviewLinks(), temporaryAssets);
        slime.FindPropertyRelative("_growthPeriod").intValue = GrowthPeriod;
        SerializedProperty stickyProperty = slime.FindPropertyRelative("_stickyColumns");
        stickyProperty.arraySize = stickyColumns.Count;

        for (int index = 0; index < stickyColumns.Count; index++)
            WriteColumn(stickyProperty.GetArrayElementAtIndex(index), stickyColumns[index]);

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static SlimeNeighborhood CreateNeighborhood(IReadOnlyList<ColumnPosition[]> links, List<UnityEngine.Object> temporaryAssets)
    {
        SlimeNeighborhood neighborhood = ScriptableObject.CreateInstance<SlimeNeighborhood>();
        neighborhood.name = "SlimePreviewNeighborhood";
        neighborhood.hideFlags = HideFlags.HideAndDontSave;
        temporaryAssets.Add(neighborhood);
        SerializedObject serializedNeighborhood = new SerializedObject(neighborhood);
        SerializedProperty linksProperty = serializedNeighborhood.FindProperty("_links");
        linksProperty.arraySize = links.Count;

        for (int index = 0; index < links.Count; index++)
        {
            WriteColumn(linksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_first"), links[index][0]);
            WriteColumn(linksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_second"), links[index][1]);
        }

        serializedNeighborhood.ApplyModifiedPropertiesWithoutUndo();
        return neighborhood;
    }

    private static void WriteColumn(SerializedProperty property, ColumnPosition position)
    {
        property.FindPropertyRelative("_shelfIndex").intValue = position.ShelfIndex;
        property.FindPropertyRelative("_columnIndex").intValue = position.ColumnIndex;
    }

    private static BoardStateSnapshot CreateBoardShape()
    {
        return new BoardStateSnapshot(Enumerable.Range(0, BoardShelfCount)
            .Select(_ => new ShelfStateSnapshot(Enumerable.Range(0, ShelfCapacity).Select(__ => new ColumnStateSnapshot(Array.Empty<ItemType>())).ToArray()))
            .ToArray());
    }

    private static string FormatDistribution(IEnumerable<int> values)
    {
        return string.Join(" ", values.GroupBy(value => value).OrderBy(group => group.Key).Select(group => $"{group.Key}:{group.Count()}"));
    }
}
