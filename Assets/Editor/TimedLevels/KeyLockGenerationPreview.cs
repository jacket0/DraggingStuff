using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class KeyLockGenerationPreview
{
    private const int PreviewSeedLevelNumber = 90;
    private const int RequiredVariantCount = 12;
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string BoardSceneName = "ThirdLevel";
    private const int BoardShelfCount = 11;
    private const int ShelfCapacity = 3;
    private const int DefaultMinimumKeyDepth = 2;
    private const int DefaultMinimumMovesToFirstKey = 2;
    private static readonly int[] KeyLockedShelfIndices = { 0, 2 };

    [MenuItem("Tools/Timed Levels/Key Lock Generation Preview")]
    public static void Run() => Run(DefaultMinimumKeyDepth, DefaultMinimumMovesToFirstKey);

    public static void Run(int minimumKeyDepth, int minimumMovesToFirstKey)
    {
        TimedLevelDefinition definition = CreatePreviewDefinition(minimumKeyDepth, minimumMovesToFirstKey);

        try
        {
            BoardStateSnapshot shape = CreateBoardShape();
            Stopwatch stopwatch = Stopwatch.StartNew();
            Dictionary<TimedLevelVariantGenerator.CandidateRejection, int> rejections = new Dictionary<TimedLevelVariantGenerator.CandidateRejection, int>();
            List<TimedLevelVariant> candidates;

            try
            {
                candidates = TimedLevelVariantGenerator.GenerateCandidates(definition, PreviewSeedLevelNumber, shape, rejections);
            }
            catch (InvalidOperationException)
            {
                Debug.Log($"KEY_LOCK_GENERATION_PREVIEW (depth {minimumKeyDepth}, moves {minimumMovesToFirstKey}) failed after {stopwatch.Elapsed.TotalSeconds:F1} s, rejected: {TimedLevelVariantGenerator.FormatRejections(rejections)}.");
                throw;
            }

            stopwatch.Stop();

            List<int> keyDepths = new List<int>();
            List<int> openingPercents = new List<int>();

            foreach (TimedLevelVariant candidate in candidates)
                ValidateCandidate(candidate, keyDepths, openingPercents);

            int medianMoveCount = TimedLevelVariantGenerator.GetMedian(candidates.Select(candidate => candidate.MoveCount));
            List<TimedLevelVariant> accepted = candidates.Where(candidate => Math.Abs(candidate.MoveCount - medianMoveCount) <= medianMoveCount * 0.1d).ToList();

            if (accepted.Count >= RequiredVariantCount)
            {
                TimedLevelVariantGenerator.WriteVariants(definition, accepted.Take(RequiredVariantCount).ToArray());
                TimedLevelValidator.ValidateForRuntime(definition, shape, TimedLevelLayoutRules.GeneratorVersion);
            }

            Debug.Log(
                $"KEY_LOCK_GENERATION_PREVIEW (depth {minimumKeyDepth}, moves {minimumMovesToFirstKey}): {candidates.Count} candidates in {stopwatch.Elapsed.TotalSeconds:F1} s, " +
                $"{accepted.Count} within 10% of median {medianMoveCount} moves, " +
                $"key depths {FormatDistribution(keyDepths)}, " +
                $"safe opened at % of solution: median {TimedLevelVariantGenerator.GetMedian(openingPercents)} (min {openingPercents.Min()}, max {openingPercents.Max()}), " +
                $"rejected: {TimedLevelVariantGenerator.FormatRejections(rejections)}.");

            if (accepted.Count < RequiredVariantCount)
                throw new InvalidOperationException($"Key lock preview: only {accepted.Count} variants are within 10% of median {medianMoveCount}.");

            Debug.Log("KEY_LOCK_GENERATION_PREVIEW_PASS");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void ValidateCandidate(TimedLevelVariant variant, List<int> keyDepths, List<int> openingPercents)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = variant.CreateLayout();
        IReadOnlyList<TimedLevelMove> solution = variant.CreateSolution();

        keyDepths.AddRange(state.Shelves.SelectMany(shelf => shelf.Columns).SelectMany(column => column.Keys).Select(key => key.ItemsInFront));

        if (state.KeyCount != KeyLockedShelfIndices.Length || KeyLockedShelfIndices.Any(shelfIndex => state.Shelves[shelfIndex].IsOpen))
            throw new InvalidOperationException($"Key lock preview: seed {variant.Seed} does not lock its shelves with one key each.");

        for (int step = 0; step < solution.Count; step++)
        {
            if (!simulator.TrySimulate(state, solution[step].Source, solution[step].Target, out BoardMoveSimulation simulation)
                || !simulation.IsAllowed
                || simulation.MatchCount != (simulation.IsSwap ? 0 : 1))
            {
                throw new InvalidOperationException($"Key lock preview: seed {variant.Seed} does not replay with one match per placement at step {step + 1}.");
            }

            foreach (int _ in simulation.OpenedShelfIndexes)
                openingPercents.Add((step + 1) * 100 / solution.Count);

            state = simulation.State;
        }

        if (!state.IsCleared || state.HasKeys)
            throw new InvalidOperationException($"Key lock preview: seed {variant.Seed} does not clear the board.");
    }

    private static TimedLevelDefinition CreatePreviewDefinition(int minimumKeyDepth, int minimumMovesToFirstKey)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        ShelfItemCatalog itemCatalog = catalog.Levels.First(level => level.SceneName == BoardSceneName).Definition.ItemCatalog;
        TimedLevelDefinition definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
        definition.name = "KeyLockPreview";
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = 160;
        serializedDefinition.FindProperty("_twoStarTimeSeconds").intValue = 120;
        serializedDefinition.FindProperty("_threeStarTimeSeconds").intValue = 90;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = 3;
        serializedDefinition.FindProperty("_itemCatalog").objectReferenceValue = itemCatalog;
        serializedDefinition.FindProperty("_minimumKeyDepth").intValue = minimumKeyDepth;
        serializedDefinition.FindProperty("_minimumMovesToFirstKey").intValue = minimumMovesToFirstKey;
        SerializedProperty lockedShelvesProperty = serializedDefinition.FindProperty("_keyLockedShelfIndices");
        lockedShelvesProperty.arraySize = KeyLockedShelfIndices.Length;

        for (int index = 0; index < KeyLockedShelfIndices.Length; index++)
            lockedShelvesProperty.GetArrayElementAtIndex(index).intValue = KeyLockedShelfIndices[index];

        SetItemGroups(
            serializedDefinition.FindProperty("_itemGroups"),
            (ItemType.Ball, 3),
            (ItemType.Bear, 3),
            (ItemType.Plant, 2),
            (ItemType.Lamp, 2),
            (ItemType.MapBall, 2),
            (ItemType.Beauty, 2),
            (ItemType.Toy, 1));
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static BoardStateSnapshot CreateBoardShape()
    {
        return new BoardStateSnapshot(Enumerable.Range(0, BoardShelfCount)
            .Select(_ => new ShelfStateSnapshot(Enumerable.Range(0, ShelfCapacity).Select(__ => new ColumnStateSnapshot(Array.Empty<ItemType>())).ToArray()))
            .ToArray());
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
