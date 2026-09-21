using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TimedLevelVariantGenerator
{
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const int RequiredVariantCount = 12;
    private const int CandidatePoolSize = 24;
    private const int MaximumSeedAttempts = 10000;

    [MenuItem("Tools/Timed Levels/Generate All Variants")]
    public static void GenerateAll()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null)
            throw new InvalidOperationException($"Level catalog was not found at {CatalogPath}.");

        SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (LevelEntry level in catalog.Levels)
                Generate(level);

            AssetDatabase.SaveAssets();
            Debug.Log($"Generated {RequiredVariantCount} verified variants for {catalog.Levels.Count} timed levels.");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    [MenuItem("Tools/Timed Levels/Generate Selected Definition")]
    public static void GenerateSelected()
    {
        TimedLevelDefinition definition = Selection.activeObject as TimedLevelDefinition;

        if (definition == null)
            throw new InvalidOperationException("Select a TimedLevelDefinition asset.");

        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        LevelEntry level = catalog.Levels.FirstOrDefault(entry => entry != null && entry.Definition == definition);

        if (level == null)
            throw new InvalidOperationException($"{definition.name} is not referenced by the main level catalog.");

        SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Generate(level);
            AssetDatabase.SaveAssets();
            Debug.Log($"Generated {RequiredVariantCount} verified variants for level {level.Number}.");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    private static void Generate(LevelEntry level)
    {
        if (level == null || level.Definition == null)
            throw new InvalidOperationException("The level catalog contains an incomplete entry.");

        string scenePath = $"Assets/Scenes/{level.SceneName}.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        LevelSession session = FindComponent<LevelSession>(scene);
        SerializedProperty boardProperty = new SerializedObject(session).FindProperty("_shelfBoard");
        ShelfBoard board = boardProperty?.objectReferenceValue as ShelfBoard;

        if (board == null)
            throw new InvalidOperationException($"{scenePath}: LevelSession does not reference a ShelfBoard.");

        BoardStateSnapshot shape = CreateShape(board);
        TimedLevelValidator.ValidateForGeneration(level.Definition, shape);
        List<TimedLevelVariant> candidates = GenerateCandidates(level, shape);
        int medianMoveCount = GetMedianMoveCount(candidates);
        List<TimedLevelVariant> accepted = candidates
            .Where(variant => IsWithinDifficultyRange(variant.MoveCount, medianMoveCount))
            .Take(RequiredVariantCount)
            .ToList();

        if (accepted.Count < RequiredVariantCount)
        {
            throw new InvalidOperationException(
                $"Level {level.Number}: only {accepted.Count} variants are within 10% of median {medianMoveCount}.");
        }

        WriteVariants(level.Definition, accepted);
    }

    private static List<TimedLevelVariant> GenerateCandidates(LevelEntry level, BoardStateSnapshot shape)
    {
        TimedLevelLayoutGenerator generator = new TimedLevelLayoutGenerator();
        List<TimedLevelVariant> candidates = new List<TimedLevelVariant>();
        int seedOffset = 1;

        while (candidates.Count < CandidatePoolSize && seedOffset <= MaximumSeedAttempts)
        {
            int seed = checked(level.Number * 100000 + seedOffset);
            seedOffset++;
            TimedLevelGenerationResult generation;

            try
            {
                generation = generator.GenerateWithSolution(level.Definition, shape, seed);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            TimedLevelSolver solver = new TimedLevelSolver(
                generation.SolutionMoves.Count,
                generation.SolutionMoves.Count + 1,
                TimeSpan.FromSeconds(5));
            TimedLevelSolveResult solveResult = solver.Solve(generation.State, generation.SolutionMoves);

            if (solveResult.Status != TimedLevelSolveStatus.Solved)
                continue;

            candidates.Add(new TimedLevelVariant(
                seed,
                TimedLevelLayoutRules.GeneratorVersion,
                solveResult.MoveCount,
                BoardStateFingerprint.CreateHash(generation.State),
                generation.State,
                generation.SolutionMoves));
        }

        if (candidates.Count < CandidatePoolSize)
            throw new InvalidOperationException($"Level {level.Number}: only {candidates.Count} verified variants were generated.");

        return candidates;
    }

    private static BoardStateSnapshot CreateShape(ShelfBoard board)
    {
        ShelfStateSnapshot[] shelves = new ShelfStateSnapshot[board.Shelves.Count];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            ColumnStateSnapshot[] columns = new ColumnStateSnapshot[board.Shelves[shelfIndex].Capacity];

            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
                columns[columnIndex] = new ColumnStateSnapshot(Array.Empty<ItemType>());

            shelves[shelfIndex] = new ShelfStateSnapshot(columns);
        }

        return new BoardStateSnapshot(shelves);
    }

    private static int GetMedianMoveCount(IReadOnlyList<TimedLevelVariant> variants)
    {
        int[] moveCounts = variants.Select(variant => variant.MoveCount).OrderBy(value => value).ToArray();
        int middle = moveCounts.Length / 2;
        return moveCounts.Length % 2 == 0
            ? (moveCounts[middle - 1] + moveCounts[middle]) / 2
            : moveCounts[middle];
    }

    private static bool IsWithinDifficultyRange(int moveCount, int medianMoveCount)
    {
        return Math.Abs(moveCount - medianMoveCount) <= medianMoveCount * 0.1d;
    }

    private static void WriteVariants(TimedLevelDefinition definition, IReadOnlyList<TimedLevelVariant> variants)
    {
        SerializedObject serializedDefinition = new SerializedObject(definition);
        SerializedProperty variantsProperty = serializedDefinition.FindProperty("_variants");
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

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static void WriteLayout(SerializedProperty shelvesProperty, BoardStateSnapshot layout)
    {
        shelvesProperty.arraySize = layout.Shelves.Count;

        for (int shelfIndex = 0; shelfIndex < layout.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = layout.Shelves[shelfIndex];
            SerializedProperty columnsProperty = shelvesProperty.GetArrayElementAtIndex(shelfIndex).FindPropertyRelative("_columns");
            columnsProperty.arraySize = shelf.Columns.Count;

            for (int columnIndex = 0; columnIndex < shelf.Columns.Count; columnIndex++)
            {
                ColumnStateSnapshot column = shelf.Columns[columnIndex];
                SerializedProperty itemsProperty = columnsProperty.GetArrayElementAtIndex(columnIndex).FindPropertyRelative("_items");
                itemsProperty.arraySize = column.Items.Count;

                for (int itemIndex = 0; itemIndex < column.Items.Count; itemIndex++)
                    itemsProperty.GetArrayElementAtIndex(itemIndex).enumValueIndex = (int)column.Items[itemIndex];
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

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        throw new InvalidOperationException($"{scene.path}: {typeof(T).Name} was not found.");
    }
}
