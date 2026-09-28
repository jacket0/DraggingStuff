using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TimedLevelGeneratorRegression
{
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string BaselinePath = "Assets/Editor/TimedLevels/Regression/generator-baseline.json";
    private const int SeedsPerLevel = 3;
    private const string FailedGeneration = "failed";

    [MenuItem("Tools/Timed Levels/Record Generator Baseline")]
    public static void Record()
    {
        GeneratorBaseline baseline = new GeneratorBaseline(GenerateCatalogSamples());
        Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath));
        File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true));
        AssetDatabase.ImportAsset(BaselinePath);
        Debug.Log($"GENERATOR_BASELINE_RECORDED: {baseline.Samples.Count} samples.");
    }

    [MenuItem("Tools/Timed Levels/Check Generator Baseline")]
    public static void Check()
    {
        if (!File.Exists(BaselinePath))
            throw new InvalidOperationException($"{BaselinePath} was not found. Record the baseline first.");

        GeneratorBaseline baseline = JsonUtility.FromJson<GeneratorBaseline>(File.ReadAllText(BaselinePath));
        HashSet<int> recordedLevels = new HashSet<int>(baseline.Samples.Select(sample => sample.LevelNumber));
        Dictionary<int, GeneratorSample> actualSamples = GenerateCatalogSamples(recordedLevels).ToDictionary(sample => sample.Seed);
        List<string> mismatches = new List<string>();

        foreach (GeneratorSample expected in baseline.Samples)
        {
            if (!actualSamples.TryGetValue(expected.Seed, out GeneratorSample actual))
                mismatches.Add($"level {expected.LevelNumber}, seed {expected.Seed}: level is missing from the catalog");
            else if (!actual.Matches(expected))
                mismatches.Add($"level {expected.LevelNumber}, seed {expected.Seed}: layout {expected.LayoutHash} -> {actual.LayoutHash}, solution {expected.Solution} -> {actual.Solution}");
        }

        if (mismatches.Count > 0)
            throw new InvalidOperationException($"Generator output changed:\n{string.Join("\n", mismatches)}");

        Debug.Log($"GENERATOR_BASELINE_PASS: {baseline.Samples.Count} samples.");
    }

    private static List<GeneratorSample> GenerateCatalogSamples(ISet<int> levelNumbers = null)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null)
            throw new InvalidOperationException($"Level catalog was not found at {CatalogPath}.");

        LevelReference[] levels = catalog.Levels
            .Where(level => levelNumbers == null || levelNumbers.Contains(level.Number))
            .Select(level => new LevelReference(level.Number, level.SceneName, AssetDatabase.GetAssetPath(level.Definition)))
            .ToArray();
        SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            List<GeneratorSample> samples = new List<GeneratorSample>();

            foreach (IGrouping<string, LevelReference> sceneLevels in levels.GroupBy(level => level.SceneName))
            {
                BoardStateSnapshot boardShape = LoadBoardShape(sceneLevels.Key);

                foreach (LevelReference level in sceneLevels)
                    samples.AddRange(GenerateLevelSamples(level, boardShape));
            }

            return samples.OrderBy(sample => sample.Seed).ToList();
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    private static BoardStateSnapshot LoadBoardShape(string sceneName)
    {
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        LevelSession session = scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<LevelSession>(true))
            .FirstOrDefault(component => component != null);

        if (session == null)
            throw new InvalidOperationException($"{scenePath}: {nameof(LevelSession)} was not found.");

        ShelfBoard board = new SerializedObject(session).FindProperty("_shelfBoard")?.objectReferenceValue as ShelfBoard;

        if (board == null)
            throw new InvalidOperationException($"{scenePath}: {nameof(LevelSession)} does not reference a {nameof(ShelfBoard)}.");

        board.Initialize();
        return board.CreateSnapshot();
    }

    private static IEnumerable<GeneratorSample> GenerateLevelSamples(LevelReference level, BoardStateSnapshot boardShape)
    {
        TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(level.DefinitionPath);

        if (definition == null)
            throw new InvalidOperationException($"Level {level.Number}: definition was not found at {level.DefinitionPath}.");

        TimedLevelLayoutGenerator generator = new TimedLevelLayoutGenerator();

        for (int seedOffset = 1; seedOffset <= SeedsPerLevel; seedOffset++)
        {
            int seed = checked(level.Number * 100000 + seedOffset);
            yield return GenerateSample(generator, definition, boardShape, level.Number, seed);
        }
    }

    private static GeneratorSample GenerateSample(
        TimedLevelLayoutGenerator generator,
        TimedLevelDefinition definition,
        BoardStateSnapshot boardShape,
        int levelNumber,
        int seed)
    {
        try
        {
            TimedLevelGenerationResult result = generator.GenerateWithSolution(definition, boardShape, seed);
            return new GeneratorSample(levelNumber, seed, BoardStateFingerprint.CreateHash(result.State), FormatSolution(result.SolutionMoves));
        }
        catch (InvalidOperationException)
        {
            return new GeneratorSample(levelNumber, seed, FailedGeneration, FailedGeneration);
        }
    }

    private static string FormatSolution(IReadOnlyList<TimedLevelMove> moves)
    {
        StringBuilder solution = new StringBuilder();

        foreach (TimedLevelMove move in moves)
        {
            solution
                .Append(move.Source.ShelfIndex).Append(':').Append(move.Source.ColumnIndex)
                .Append('>')
                .Append(move.Target.ShelfIndex).Append(':').Append(move.Target.ColumnIndex)
                .Append(' ');
        }

        return solution.ToString().TrimEnd();
    }

    private readonly struct LevelReference
    {
        public int Number { get; }
        public string SceneName { get; }
        public string DefinitionPath { get; }

        public LevelReference(int number, string sceneName, string definitionPath)
        {
            Number = number;
            SceneName = sceneName;
            DefinitionPath = definitionPath;
        }
    }

    [Serializable]
    private sealed class GeneratorBaseline
    {
        [SerializeField] private List<GeneratorSample> _samples = new List<GeneratorSample>();

        public IReadOnlyList<GeneratorSample> Samples => _samples;

        public GeneratorBaseline(IEnumerable<GeneratorSample> samples)
        {
            _samples.AddRange(samples);
        }
    }

    [Serializable]
    private sealed class GeneratorSample
    {
        [SerializeField] private int _levelNumber;
        [SerializeField] private int _seed;
        [SerializeField] private string _layoutHash;
        [SerializeField] private string _solution;

        public int LevelNumber => _levelNumber;
        public int Seed => _seed;
        public string LayoutHash => _layoutHash;
        public string Solution => _solution;

        public GeneratorSample(int levelNumber, int seed, string layoutHash, string solution)
        {
            _levelNumber = levelNumber;
            _seed = seed;
            _layoutHash = layoutHash;
            _solution = solution;
        }

        public bool Matches(GeneratorSample other)
        {
            return _layoutHash == other.LayoutHash && _solution == other.Solution;
        }
    }
}
