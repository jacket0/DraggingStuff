using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class CyclicShelfGenerationPreview
{
    private const int SwingSeedLevelNumber = 93;
    private const int CarouselSeedLevelNumber = 94;
    private const int RotorSeedLevelNumber = 95;
    private const int RequiredVariantCount = 12;
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string ItemCatalogSceneName = "SixthLevel";
    private const int BoardShelfCount = 11;
    private const int ShelfCapacity = 3;
    private const int EmptyColumnCount = 3;
    private const int DefaultMinimumItemsBehindShutter = 3;
    private const int DefaultMinimumPreparationMoves = 2;
    private const int BotRunsPerPolicy = 10;

    private static readonly CyclicShelfDefinitionData[] SwingShelves =
    {
        new CyclicShelfDefinitionData(0, true, 3, 3, 3),
        new CyclicShelfDefinitionData(2, false, 3, 3, 3)
    };

    private static readonly CyclicShelfDefinitionData[] CarouselShelves =
    {
        new CyclicShelfDefinitionData(0, true, 4, 8, 4),
        new CyclicShelfDefinitionData(1, false, 4, 8, 4),
        new CyclicShelfDefinitionData(2, false, 4, 8, 8)
    };

    private static readonly CyclicShelfDefinitionData[] RotorShelves =
    {
        new CyclicShelfDefinitionData(0, true, 6, 3, 6),
        new CyclicShelfDefinitionData(1, false, 6, 3, 3),
        new CyclicShelfDefinitionData(2, true, 6, 3, 3)
    };

    private static readonly (ItemType Type, int Count)[] SwingGroups =
    {
        (ItemType.Ball, 3), (ItemType.Bear, 2), (ItemType.Plant, 2), (ItemType.Lamp, 2), (ItemType.MapBall, 2), (ItemType.Beauty, 2), (ItemType.Toy, 1)
    };

    private static readonly (ItemType Type, int Count)[] CarouselGroups =
    {
        (ItemType.Ball, 3), (ItemType.Bear, 2), (ItemType.Plant, 2), (ItemType.Lamp, 2), (ItemType.MapBall, 2), (ItemType.Beauty, 2), (ItemType.Toy, 2)
    };

    [MenuItem("Tools/Timed Levels/Cyclic Shelf Generation Preview/Swing")]
    public static void RunSwing() => Run("swing", SwingSeedLevelNumber, SwingShelves, SwingGroups, DefaultMinimumItemsBehindShutter, DefaultMinimumPreparationMoves);

    [MenuItem("Tools/Timed Levels/Cyclic Shelf Generation Preview/Carousel")]
    public static void RunCarousel() => Run("carousel", CarouselSeedLevelNumber, CarouselShelves, CarouselGroups, DefaultMinimumItemsBehindShutter, DefaultMinimumPreparationMoves);

    [MenuItem("Tools/Timed Levels/Cyclic Shelf Generation Preview/Rotor")]
    public static void RunRotor() => Run("rotor", RotorSeedLevelNumber, RotorShelves, CarouselGroups, DefaultMinimumItemsBehindShutter, DefaultMinimumPreparationMoves);

    public static void RunSwing(int minimumItemsBehindShutter, int minimumPreparationMoves)
    {
        Run("swing", SwingSeedLevelNumber, SwingShelves, SwingGroups, minimumItemsBehindShutter, minimumPreparationMoves);
    }

    public static void RunCarousel(int minimumItemsBehindShutter, int minimumPreparationMoves)
    {
        Run("carousel", CarouselSeedLevelNumber, CarouselShelves, CarouselGroups, minimumItemsBehindShutter, minimumPreparationMoves);
    }

    private static void Run(
        string formName,
        int seedLevelNumber,
        IReadOnlyList<CyclicShelfDefinitionData> cyclicShelves,
        IReadOnlyList<(ItemType Type, int Count)> groups,
        int minimumItemsBehindShutter,
        int minimumPreparationMoves)
    {
        TimedLevelDefinition definition = CreatePreviewDefinition(formName, cyclicShelves, groups, minimumItemsBehindShutter, minimumPreparationMoves);
        string settings = $"{formName}, shutter {minimumItemsBehindShutter}, preparation {minimumPreparationMoves}";

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
                Debug.Log($"CYCLIC_SHELF_GENERATION_PREVIEW ({settings}) failed after {stopwatch.Elapsed.TotalSeconds:F1} s, rejected: {TimedLevelVariantGenerator.FormatRejections(rejections)}.");
                throw;
            }

            stopwatch.Stop();
            List<int> itemsAtClosings = new List<int>();
            List<int> preparationMoves = new List<int>();
            List<int> switches = new List<int>();

            foreach (TimedLevelVariant candidate in candidates)
            {
                CyclicShelfLayoutMetrics metrics = CyclicShelfLayoutMetrics.Measure(definition.AddCyclicShelves(candidate.CreateLayout()), candidate.CreateSolution());

                if (!metrics.IsReplayed || metrics.HasDeadlockOnPath)
                    throw new InvalidOperationException($"Cyclic shelf preview: seed {candidate.Seed} does not replay with cycles.");

                itemsAtClosings.AddRange(metrics.ItemsAtClosings);
                preparationMoves.Add(metrics.PreparationMoveCount);
                switches.Add(metrics.SwitchCount);
            }

            int medianMoveCount = TimedLevelVariantGenerator.GetMedian(candidates.Select(candidate => candidate.MoveCount));
            List<TimedLevelVariant> accepted = candidates.Where(candidate => Math.Abs(candidate.MoveCount - medianMoveCount) <= medianMoveCount * 0.1d).ToList();

            if (accepted.Count >= RequiredVariantCount)
            {
                TimedLevelVariantGenerator.WriteVariants(definition, accepted.Take(RequiredVariantCount).ToArray());
                TimedLevelValidator.ValidateForRuntime(definition, shape, TimedLevelLayoutRules.GeneratorVersion);
                Debug.Log(CyclicShelfBalanceBot.Run(definition, settings, BotRunsPerPolicy));
            }

            Debug.Log(
                $"CYCLIC_SHELF_GENERATION_PREVIEW ({settings}): {candidates.Count} candidates in {stopwatch.Elapsed.TotalSeconds:F1} s, " +
                $"{accepted.Count} within 10% of median {medianMoveCount} moves, " +
                $"items at closings {FormatDistribution(itemsAtClosings)}, " +
                $"preparation moves {FormatDistribution(preparationMoves)}, " +
                $"switches {FormatDistribution(switches)}, " +
                $"rejected: {TimedLevelVariantGenerator.FormatRejections(rejections)}.");

            if (accepted.Count < RequiredVariantCount)
                throw new InvalidOperationException($"Cyclic shelf preview: only {accepted.Count} variants are within 10% of median {medianMoveCount}.");

            Debug.Log("CYCLIC_SHELF_GENERATION_PREVIEW_PASS");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static TimedLevelDefinition CreatePreviewDefinition(
        string formName,
        IReadOnlyList<CyclicShelfDefinitionData> cyclicShelves,
        IReadOnlyList<(ItemType Type, int Count)> groups,
        int minimumItemsBehindShutter,
        int minimumPreparationMoves)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        ShelfItemCatalog itemCatalog = catalog.Levels.First(level => level.SceneName == ItemCatalogSceneName).Definition.ItemCatalog;
        TimedLevelDefinition definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
        definition.name = $"CyclicShelfPreview_{formName}";
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = 160;
        serializedDefinition.FindProperty("_twoStarTimeSeconds").intValue = 120;
        serializedDefinition.FindProperty("_threeStarTimeSeconds").intValue = 90;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = EmptyColumnCount;
        serializedDefinition.FindProperty("_itemCatalog").objectReferenceValue = itemCatalog;
        serializedDefinition.FindProperty("_minimumItemsBehindShutter").intValue = minimumItemsBehindShutter;
        serializedDefinition.FindProperty("_minimumPreparationMoves").intValue = minimumPreparationMoves;
        CyclicShelfDefinitionData.WriteAll(serializedDefinition.FindProperty("_cyclicShelves"), cyclicShelves);

        SerializedProperty groupsProperty = serializedDefinition.FindProperty("_itemGroups");
        groupsProperty.arraySize = groups.Count;

        for (int index = 0; index < groups.Count; index++)
        {
            SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(index);
            groupProperty.FindPropertyRelative("_type").enumValueIndex = (int)groups[index].Type;
            groupProperty.FindPropertyRelative("_groupCount").intValue = groups[index].Count;
        }

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
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
