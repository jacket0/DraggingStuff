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
    public const int BoardShelfCount = 11;
    private const int ShelfCapacity = 3;
    private const int EmptyColumnCount = 3;
    private const int GrowthPeriod = 2;
    private const int BotRunsPerPolicy = 10;

    private static readonly ColumnPosition[] StartStickyColumns = { new ColumnPosition(4, 1) };
    private static readonly ColumnPosition[] SpreadStickyColumns = { new ColumnPosition(1, 0), new ColumnPosition(4, 2), new ColumnPosition(8, 1) };

    private static readonly (ItemType Type, int Count)[] Groups =
    {
        (ItemType.Ball, 3), (ItemType.Bear, 2), (ItemType.Plant, 2), (ItemType.Lamp, 2), (ItemType.MapBall, 2), (ItemType.Beauty, 2), (ItemType.Toy, 1)
    };

    [MenuItem("Tools/Timed Levels/Slime Generation Preview/Start")]
    public static void RunStart() => Run("start", StartSeedLevelNumber, StartStickyColumns);

    [MenuItem("Tools/Timed Levels/Slime Generation Preview/Spread")]
    public static void RunSpread() => Run("spread", SpreadSeedLevelNumber, SpreadStickyColumns);

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
                Debug.Log(SlimeBalanceBot.Run(definition, settings, BotRunsPerPolicy));
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

    public static TimedLevelDefinition CreatePreviewDefinition(string formName, IReadOnlyList<ColumnPosition> stickyColumns, List<UnityEngine.Object> temporaryAssets)
    {
        TimedLevelDefinition definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
        definition.name = $"SlimePreview_{formName}";
        definition.hideFlags = HideFlags.HideAndDontSave;
        temporaryAssets.Add(definition);
        ConfigureDefinition(definition, stickyColumns, GrowthPeriod, true, 160, 120, 90);
        return definition;
    }

    public static void ConfigureDefinition(
        TimedLevelDefinition definition,
        IReadOnlyList<ColumnPosition> stickyColumns,
        int growthPeriod,
        bool cleansOwnShelfOnly,
        int timeLimitSeconds,
        int twoStarTimeSeconds,
        int threeStarTimeSeconds)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        ShelfItemCatalog itemCatalog = catalog.Levels.First(level => level.SceneName == ItemCatalogSceneName).Definition.ItemCatalog;
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = timeLimitSeconds;
        serializedDefinition.FindProperty("_twoStarTimeSeconds").intValue = twoStarTimeSeconds;
        serializedDefinition.FindProperty("_threeStarTimeSeconds").intValue = threeStarTimeSeconds;
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
        slime.FindPropertyRelative("_neighborhood").objectReferenceValue = LoadNeighborhood();
        slime.FindPropertyRelative("_growthPeriod").intValue = growthPeriod;
        slime.FindPropertyRelative("_cleansOwnShelfOnly").boolValue = cleansOwnShelfOnly;
        SerializedProperty stickyProperty = slime.FindPropertyRelative("_stickyColumns");
        stickyProperty.arraySize = stickyColumns.Count;

        for (int index = 0; index < stickyColumns.Count; index++)
            WriteColumn(stickyProperty.GetArrayElementAtIndex(index), stickyColumns[index]);

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SlimeNeighborhood LoadNeighborhood()
    {
        SlimeNeighborhood neighborhood = AssetDatabase.LoadAssetAtPath<SlimeNeighborhood>(SlimeNeighborhoodGeometry.NeighborhoodPath);
        return neighborhood != null ? neighborhood : throw new InvalidOperationException($"Slime neighborhood was not found at {SlimeNeighborhoodGeometry.NeighborhoodPath}.");
    }

    private static void WriteColumn(SerializedProperty property, ColumnPosition position)
    {
        property.FindPropertyRelative("_shelfIndex").intValue = position.ShelfIndex;
        property.FindPropertyRelative("_columnIndex").intValue = position.ColumnIndex;
    }

    public static BoardStateSnapshot CreateBoardShape()
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
