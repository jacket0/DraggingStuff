using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class ReleaseReadinessTests
{
    private const string ButtonScriptGuid = "4e29b1a8efbd4b44bb3f3716e73f07ff";
    private const string ButtonAudioScriptGuid = "e17b6d3a42f84c0fb9d163ae7c5b2081";
    private const string FontAssetPath = "Assets/Fonts/Code New Roman SDF.asset";

    private static readonly int[] ExpectedItemCounts = { 42, 48, 54, 60, 66, 72 };
    private static readonly int[][] ExpectedMatchDistributions =
    {
        new[] { 4, 4, 3, 3 },
        new[] { 4, 4, 4, 4 },
        new[] { 5, 5, 4, 4 },
        new[] { 5, 5, 5, 5 },
        new[] { 6, 6, 5, 5 },
        new[] { 6, 6, 6, 6 }
    };

    [Test]
    public void ComboGrowsToTenAndStopsAtTen()
    {
        ComboSystem comboSystem = CreateComboSystem();

        try
        {
            for (int count = 1; count <= 10; count++)
                Assert.That(comboSystem.RegisterMatch(), Is.EqualTo(count));

            Assert.That(comboSystem.RegisterMatch(), Is.EqualTo(10));
            Assert.That(comboSystem.ComboState.Count, Is.EqualTo(10));
            Assert.That(comboSystem.ComboState.NormalizedTime, Is.EqualTo(1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(comboSystem.gameObject);
        }
    }

    [Test]
    public void ComboLosesOneStepAndRefillsItsTimer()
    {
        ComboSystem comboSystem = CreateComboSystem(5);

        try
        {
            AdvanceComboTimer(comboSystem, 3f);

            Assert.That(comboSystem.ComboState.Count, Is.EqualTo(4));
            Assert.That(comboSystem.ComboState.RemainingTime, Is.EqualTo(3f).Within(0.001f));
            Assert.That(comboSystem.ComboState.NormalizedTime, Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(comboSystem.gameObject);
        }
    }

    [Test]
    public void ComboLosesSeveralStepsDuringLongFrame()
    {
        ComboSystem comboSystem = CreateComboSystem(5);

        try
        {
            AdvanceComboTimer(comboSystem, 7f);

            Assert.That(comboSystem.ComboState.Count, Is.EqualTo(3));
            Assert.That(comboSystem.ComboState.RemainingTime, Is.EqualTo(2f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(comboSystem.gameObject);
        }
    }

    [Test]
    public void ComboFreezePausesAndResumesTimer()
    {
        ComboSystem comboSystem = CreateComboSystem(3);

        try
        {
            MultiplierModifierCollection modifiers = GetTimerModifiers(comboSystem);

            using (comboSystem.AddTimerSpeedMultiplier(0f))
            {
                AdvanceComboTimer(comboSystem, 2f * modifiers.CombinedMultiplier);
                Assert.That(comboSystem.ComboState.RemainingTime, Is.EqualTo(3f).Within(0.001f));
            }

            AdvanceComboTimer(comboSystem, 1f * modifiers.CombinedMultiplier);
            Assert.That(comboSystem.ComboState.RemainingTime, Is.EqualTo(2f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(comboSystem.gameObject);
        }
    }

    [Test]
    public void LevelsHaveExpectedSizesAndTypeDistribution()
    {
        for (int levelNumber = 1; levelNumber <= 6; levelNumber++)
        {
            LevelDefinition definition = LoadLevelDefinition(levelNumber);
            ShelfItem[] items = definition.Shelves
                .SelectMany(shelf => shelf.Layers)
                .SelectMany(layer => layer.ItemPrefabs)
                .Where(item => item != null)
                .ToArray();

            Assert.That(items.Length, Is.EqualTo(ExpectedItemCounts[levelNumber - 1]), $"Level {levelNumber}");

            int[] matchDistribution = items
                .GroupBy(item => item.Type)
                .Select(group => group.Count() / 3)
                .OrderByDescending(count => count)
                .ToArray();

            Assert.That(items.GroupBy(item => item.Type).All(group => group.Count() % 3 == 0), Is.True, $"Level {levelNumber}");
            Assert.That(matchDistribution, Is.EqualTo(ExpectedMatchDistributions[levelNumber - 1]), $"Level {levelNumber}");
        }
    }

    [Test]
    public void LevelsAreSolvableAndStartWithoutReadyMatches()
    {
        for (int levelNumber = 1; levelNumber <= 6; levelNumber++)
        {
            List<List<List<int>>> shelves = CreateSolveState(LoadLevelDefinition(levelNumber));
            List<List<int>> activeLayers = shelves.Select(shelf => shelf[0]).ToList();

            Assert.That(activeLayers.Any(layer => layer.Count < ShelfLayer.SlotCount), Is.True, $"Level {levelNumber} has no available start slot");
            Assert.That(activeLayers.Any(IsMatch), Is.False, $"Level {levelNumber} starts with a ready match");

            HashSet<string> visitedStates = new HashSet<string>();
            Assert.That(CanSolve(shelves, visitedStates, 0, 120), Is.True, $"Level {levelNumber} is not solvable after {visitedStates.Count} checked states");
        }
    }

    [Test]
    public void LevelEntriesUseSharedSceneAndAvailableDefinitions()
    {
        for (int levelNumber = 1; levelNumber <= 6; levelNumber++)
        {
            LevelEntry entry = AssetDatabase.LoadAssetAtPath<LevelEntry>($"Assets/Levels/Menu/LevelEntry_{levelNumber:00}.asset");

            Assert.That(entry, Is.Not.Null, $"Level entry {levelNumber}");
            Assert.That(entry.Number, Is.EqualTo(levelNumber));
            Assert.That(entry.SceneName, Is.EqualTo("SimpleLevel"));
            Assert.That(entry.Definition, Is.SameAs(LoadLevelDefinition(levelNumber)));
            Assert.That(entry.CanBeStarted, Is.True);
        }
    }

    [Test]
    public void IdealScoresMatchReleaseBalance()
    {
        int[] idealScores = ExpectedItemCounts.Select(itemCount => CalculateIdealScore(itemCount / 3)).ToArray();

        Assert.That(idealScores, Is.EqualTo(new[] { 9500, 11500, 13500, 15500, 17500, 19500 }));
        Assert.That(idealScores.Sum(), Is.EqualTo(87000));
    }

    [Test]
    public void EveryPrefabButtonHasClickAudio()
    {
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        foreach (string prefabPath in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            foreach (Button button in prefab.GetComponentsInChildren<Button>(true))
                Assert.That(button.GetComponent<ButtonClickAudio>(), Is.Not.Null, $"{prefabPath}: {button.name}");
        }
    }

    [Test]
    public void EverySceneButtonHasClickAudio()
    {
        string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        foreach (string scenePath in scenePaths)
        {
            HashSet<string> buttonObjects = CollectComponentGameObjects(scenePath, ButtonScriptGuid);
            HashSet<string> audioObjects = CollectComponentGameObjects(scenePath, ButtonAudioScriptGuid);

            Assert.That(buttonObjects.IsSubsetOf(audioObjects), Is.True, scenePath);
        }
    }

    [Test]
    public void ReleaseFontContainsSupportedLanguageCharacters()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        const string supportedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюяÇĞİÖŞÜçğıöşü0123456789%!?.,:;+-x()/ ";

        Assert.That(fontAsset, Is.Not.Null);
        Assert.That(fontAsset.HasCharacters(supportedCharacters, out List<char> missingCharacters), Is.True, $"Missing characters: {string.Join(string.Empty, missingCharacters)}");
    }

    [Test]
    public void AudioSettingsHaveMutedTextForEveryLanguage()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MainMenu/AudioSettingsPanel.prefab");
        AudioSettingsPanel panel = prefab.GetComponent<AudioSettingsPanel>();
        SerializedObject serializedPanel = new SerializedObject(panel);

        Assert.That(serializedPanel.FindProperty("_russianMutedValueText").stringValue, Is.Not.Empty);
        Assert.That(serializedPanel.FindProperty("_englishMutedValueText").stringValue, Is.Not.Empty);
        Assert.That(serializedPanel.FindProperty("_turkishMutedValueText").stringValue, Is.Not.Empty);
    }

    private static ComboSystem CreateComboSystem(int matchCount = 0)
    {
        ComboSystem comboSystem = new GameObject(nameof(ComboSystem)).AddComponent<ComboSystem>();

        for (int count = 0; count < matchCount; count++)
            comboSystem.RegisterMatch();

        return comboSystem;
    }

    private static void AdvanceComboTimer(ComboSystem comboSystem, float elapsedTime)
    {
        MethodInfo advanceTimer = typeof(ComboSystem).GetMethod("AdvanceTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        advanceTimer.Invoke(comboSystem, new object[] { elapsedTime });
    }

    private static MultiplierModifierCollection GetTimerModifiers(ComboSystem comboSystem)
    {
        FieldInfo modifiersField = typeof(ComboSystem).GetField("_timerSpeedModifiers", BindingFlags.Instance | BindingFlags.NonPublic);
        return (MultiplierModifierCollection)modifiersField.GetValue(comboSystem);
    }

    private static LevelDefinition LoadLevelDefinition(int levelNumber)
    {
        string assetPath = levelNumber == 1 ? "Assets/Levels/FirstLevel.asset" : $"Assets/Levels/Level_{levelNumber:00}.asset";
        LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetPath);
        Assert.That(definition, Is.Not.Null, $"Level definition {levelNumber}");
        return definition;
    }

    private static int CalculateIdealScore(int matchCount)
    {
        return Enumerable.Range(1, matchCount).Sum(matchNumber => Math.Min(matchNumber, 10) * 100);
    }

    private static List<List<List<int>>> CreateSolveState(LevelDefinition definition)
    {
        return definition.Shelves
            .Select(shelf => shelf.Layers
                .Select(layer => layer.ItemPrefabs
                    .Where(item => item != null)
                    .Select(item => (int)item.Type)
                    .ToList())
                .ToList())
            .ToList();
    }

    private static bool CanSolve(List<List<List<int>>> shelves, HashSet<string> visitedStates, int moveCount, int maxMoveCount)
    {
        RemoveClearedLayers(shelves);

        if (shelves.All(shelf => shelf.Count == 0))
            return true;

        if (moveCount >= maxMoveCount || !visitedStates.Add(CreateStateKey(shelves)))
            return false;

        List<SolveMove> moves = GetSolveMoves(shelves);

        foreach (SolveMove move in moves.OrderByDescending(candidate => candidate.Priority))
        {
            List<List<List<int>>> nextState = CloneSolveState(shelves);
            List<int> sourceLayer = nextState[move.SourceShelf][0];
            List<int> targetLayer = nextState[move.TargetShelf][0];

            sourceLayer.Remove(move.ItemType);
            targetLayer.Add(move.ItemType);

            if (IsMatch(targetLayer))
                targetLayer.Clear();

            if (CanSolve(nextState, visitedStates, moveCount + 1, maxMoveCount))
                return true;
        }

        return false;
    }

    private static List<SolveMove> GetSolveMoves(List<List<List<int>>> shelves)
    {
        List<SolveMove> moves = new List<SolveMove>();

        for (int sourceShelf = 0; sourceShelf < shelves.Count; sourceShelf++)
        {
            if (shelves[sourceShelf].Count == 0)
                continue;

            List<int> sourceLayer = shelves[sourceShelf][0];

            foreach (int itemType in sourceLayer.Distinct())
            {
                for (int targetShelf = 0; targetShelf < shelves.Count; targetShelf++)
                {
                    if (sourceShelf == targetShelf || shelves[targetShelf].Count == 0)
                        continue;

                    List<int> targetLayer = shelves[targetShelf][0];

                    if (targetLayer.Count >= ShelfLayer.SlotCount)
                        continue;

                    int matchingTargetItems = targetLayer.Count(targetType => targetType == itemType);
                    bool createsMatch = targetLayer.Count == ShelfLayer.SlotCount - 1 && matchingTargetItems == ShelfLayer.SlotCount - 1;
                    bool clearsSourceLayer = sourceLayer.Count == 1;
                    int priority = matchingTargetItems * 20 + (createsMatch ? 100 : 0) + (clearsSourceLayer ? 30 : 0) + targetLayer.Count;

                    moves.Add(new SolveMove(sourceShelf, targetShelf, itemType, priority));
                }
            }
        }

        return moves;
    }

    private static void RemoveClearedLayers(List<List<List<int>>> shelves)
    {
        foreach (List<List<int>> shelf in shelves)
        {
            while (shelf.Count > 0 && shelf[0].Count == 0)
                shelf.RemoveAt(0);
        }
    }

    private static string CreateStateKey(List<List<List<int>>> shelves)
    {
        return string.Join("|", shelves
            .Select(shelf => string.Join("/", shelf.Select(layer => string.Join(string.Empty, layer.OrderBy(type => type)))))
            .OrderBy(shelf => shelf));
    }

    private static List<List<List<int>>> CloneSolveState(List<List<List<int>>> shelves)
    {
        return shelves
            .Select(shelf => shelf.Select(layer => new List<int>(layer)).ToList())
            .ToList();
    }

    private static bool IsMatch(List<int> layer)
    {
        return layer.Count == ShelfLayer.SlotCount && layer.All(itemType => itemType == layer[0]);
    }

    private static HashSet<string> CollectComponentGameObjects(string assetPath, string scriptGuid)
    {
        string assetText = File.ReadAllText(assetPath);
        MatchCollection componentBlocks = Regex.Matches(assetText, @"--- !u!114 &.*?(?=--- !u!|\z)", RegexOptions.Singleline);
        HashSet<string> gameObjects = new HashSet<string>();

        foreach (Match componentBlock in componentBlocks)
        {
            if (!componentBlock.Value.Contains($"guid: {scriptGuid}"))
                continue;

            Match gameObjectReference = Regex.Match(componentBlock.Value, @"m_GameObject: \{fileID: (\d+)\}");
            if (gameObjectReference.Success && gameObjectReference.Groups[1].Value != "0")
                gameObjects.Add(gameObjectReference.Groups[1].Value);
        }

        return gameObjects;
    }

    private readonly struct SolveMove
    {
        public int SourceShelf { get; }
        public int TargetShelf { get; }
        public int ItemType { get; }
        public int Priority { get; }

        public SolveMove(int sourceShelf, int targetShelf, int itemType, int priority)
        {
            SourceShelf = sourceShelf;
            TargetShelf = targetShelf;
            ItemType = itemType;
            Priority = priority;
        }
    }
}
