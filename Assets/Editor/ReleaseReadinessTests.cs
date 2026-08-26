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
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ReleaseReadinessTests
{
    private const string ButtonScriptGuid = "4e29b1a8efbd4b44bb3f3716e73f07ff";
    private const string ButtonAudioScriptGuid = "e17b6d3a42f84c0fb9d163ae7c5b2081";
    private const string ReleaseFontGuid = "02fea1fb776e0ad43a5e684a1ab0ce9e";
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

    private static readonly int[][] VisualShelfLines =
    {
        new[] { 0, 1, 2, 3 },
        new[] { 4, 5, 6, 7 },
        new[] { 8, 9, 10, 11 },
        new[] { 0, 4, 8 },
        new[] { 1, 5, 9 },
        new[] { 2, 6, 10 },
        new[] { 3, 7, 11 }
    };

    private static readonly int[][] VisualShelfRuns =
    {
        new[] { 0, 1, 2 },
        new[] { 1, 2, 3 },
        new[] { 4, 5, 6 },
        new[] { 5, 6, 7 },
        new[] { 8, 9, 10 },
        new[] { 9, 10, 11 },
        new[] { 0, 4, 8 },
        new[] { 1, 5, 9 },
        new[] { 2, 6, 10 },
        new[] { 3, 7, 11 }
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
            int maximumMoveCount = Mathf.FloorToInt(ExpectedItemCounts[levelNumber - 1] / 3 * 2.75f);
            Assert.That(CanSolve(shelves, visitedStates, 0, maximumMoveCount), Is.True, $"Level {levelNumber} is not solvable in {maximumMoveCount} moves after {visitedStates.Count} checked states");
        }
    }

    [Test]
    public void ReleaseLevelsUseVariedThreeLayerLayouts()
    {
        for (int levelNumber = 2; levelNumber <= 6; levelNumber++)
        {
            LevelDefinition definition = LoadLevelDefinition(levelNumber);
            int[] occupancies = definition.Shelves
                .SelectMany(shelf => shelf.Layers)
                .Select(layer => layer.ItemPrefabs.Count(item => item != null))
                .ToArray();

            Assert.That(definition.Shelves.All(shelf => shelf.Layers.Count == 3), Is.True, $"Level {levelNumber}");
            Assert.That(occupancies, Does.Contain(1), $"Level {levelNumber}");
            Assert.That(occupancies, Does.Contain(2), $"Level {levelNumber}");
            Assert.That(occupancies, Does.Contain(3), $"Level {levelNumber}");
            Assert.That(definition.Shelves.SelectMany(shelf => shelf.Layers).Any(layer => HasDifferentItems(layer.ItemPrefabs)), Is.True, $"Level {levelNumber}");
        }
    }

    [Test]
    public void ShelfBoardResolvesMatchOnActiveLayer()
    {
        GameObject boardObject = new GameObject(nameof(ShelfBoard));

        try
        {
            ShelfBoard board = boardObject.AddComponent<ShelfBoard>();
            Shelf shelf = CreateShelfWithMatch(boardObject.transform);
            SetField(board, "_shelves", new List<Shelf> { shelf });

            Assert.That(board.TryResolveActiveMatch(out Shelf matchedShelf, out MatchResolution match), Is.True);
            Assert.That(matchedShelf, Is.SameAs(shelf));
            Assert.That(match.Items.Count, Is.EqualTo(ShelfLayer.SlotCount));
            Assert.That(shelf.ActiveLayer.IsEmpty, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(boardObject);
        }
    }

    [Test]
    public void ReleaseLevelsAvoidRepeatedVisiblePatterns()
    {
        for (int levelNumber = 2; levelNumber <= 6; levelNumber++)
        {
            LevelDefinition definition = LoadLevelDefinition(levelNumber);

            foreach (ShelfDefinition shelf in definition.Shelves)
            {
                ShelfItem[] activeItems = shelf.Layers[0].ItemPrefabs.Where(item => item != null).ToArray();
                Assert.That(activeItems.Select(item => item.Type).Distinct().Count(), Is.EqualTo(activeItems.Length), $"Level {levelNumber} active shelf");

                foreach (ItemType itemType in Enum.GetValues(typeof(ItemType)))
                {
                    int occupiedLayers = shelf.Layers.Count(layer => layer.ItemPrefabs.Any(item => item != null && item.Type == itemType));
                    Assert.That(occupiedLayers, Is.LessThan(3), $"Level {levelNumber} shelf depth {itemType}");
                }
            }

            foreach (int[] shelfLine in VisualShelfLines)
            {
                for (int layerIndex = 0; layerIndex < 3; layerIndex++)
                {
                    string[] repeatedPatterns = shelfLine
                        .Select(shelfIndex => CreateLayerPattern(definition.Shelves[shelfIndex].Layers[layerIndex]))
                        .Where(pattern => pattern.Length >= 2)
                        .GroupBy(pattern => pattern)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key)
                        .ToArray();

                    Assert.That(repeatedPatterns, Is.Empty, $"Level {levelNumber} layer {layerIndex + 1}");
                }
            }

            foreach (int[] shelfRun in VisualShelfRuns)
            {
                for (int layerIndex = 0; layerIndex < 3; layerIndex++)
                {
                    foreach (ItemType itemType in Enum.GetValues(typeof(ItemType)))
                    {
                        bool repeatedAcrossRun = shelfRun.All(shelfIndex => definition.Shelves[shelfIndex].Layers[layerIndex].ItemPrefabs.Any(item => item != null && item.Type == itemType));
                        Assert.That(repeatedAcrossRun, Is.False, $"Level {levelNumber} layer {layerIndex + 1} {itemType}");
                    }
                }
            }
        }
    }

    [Test]
    public void MoveResolutionBlocksInteractionWithoutStoppingLevelTime()
    {
        LevelSession session = new GameObject(nameof(LevelSession)).AddComponent<LevelSession>();

        try
        {
            session.StartLevel();
            SetField(session, "_isResolvingMove", true);

            Assert.That(session.IsPlaying, Is.True);
            Assert.That(session.CanInteract, Is.False);
        }
        finally
        {
            Time.timeScale = 1f;
            UnityEngine.Object.DestroyImmediate(session.gameObject);
        }
    }

    [Test]
    public void SecondaryMouseButtonDoesNotReplaceActiveDrag()
    {
        GameObject controllerObject = new GameObject(nameof(ShelfItemDragController));
        GameObject itemObject = new GameObject(nameof(ShelfItemDragHandler));
        GameObject eventSystemObject = new GameObject(nameof(EventSystem));

        try
        {
            ShelfItemDragController controller = controllerObject.AddComponent<ShelfItemDragController>();
            itemObject.transform.SetParent(controllerObject.transform);
            ShelfItemDragHandler handler = itemObject.AddComponent<ShelfItemDragHandler>();
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();

            SetField(handler, "_dragController", controller);
            SetField(handler, "_isDragging", true);
            SetField(handler, "_activePointerId", -1);

            PointerEventData rightPointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Right,
                pointerId = -2
            };

            Assert.DoesNotThrow(() => handler.OnBeginDrag(rightPointer));
            Assert.DoesNotThrow(() => handler.OnDrag(rightPointer));
            Assert.DoesNotThrow(() => handler.OnEndDrag(rightPointer));
            Assert.That(GetField<bool>(handler, "_isDragging"), Is.True);
            Assert.That(GetField<ShelfItemDragController>(handler, "_dragController"), Is.SameAs(controller));
            Assert.That(GetField<int>(handler, "_activePointerId"), Is.EqualTo(-1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(eventSystemObject);
            UnityEngine.Object.DestroyImmediate(itemObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
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
    public void SharedLevelSceneSupportsThreeLayersPerShelf()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/SimpleLevel.unity");
        string shelfScriptGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Model/Shelf.cs");
        MatchCollection componentBlocks = Regex.Matches(sceneText, @"--- !u!114 &.*?(?=--- !u!|\z)", RegexOptions.Singleline);
        Match[] shelfComponents = componentBlocks.Cast<Match>()
            .Where(component => component.Value.Contains($"guid: {shelfScriptGuid}"))
            .ToArray();

        Assert.That(shelfComponents.Length, Is.EqualTo(12));

        foreach (Match shelfComponent in shelfComponents)
        {
            Match layerList = Regex.Match(shelfComponent.Value, @"_shelfLayers:\s*((?:- \{fileID: \d+\}\s*)+)");
            Assert.That(layerList.Success, Is.True);
            Assert.That(Regex.Matches(layerList.Groups[1].Value, @"fileID:").Count, Is.EqualTo(3));
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
            {
                ButtonClickAudio clickAudio = button.GetComponent<ButtonClickAudio>();
                Assert.That(clickAudio, Is.Not.Null, $"{prefabPath}: {button.name}");

                SerializedObject serializedAudio = new SerializedObject(clickAudio);
                Assert.That(serializedAudio.FindProperty("_clickClip").objectReferenceValue, Is.Not.Null, $"{prefabPath}: {button.name} clip");
                Assert.That(serializedAudio.FindProperty("_outputAudioMixerGroup").objectReferenceValue, Is.Not.Null, $"{prefabPath}: {button.name} mixer");
            }
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

            foreach (Match audioComponent in CollectComponentBlocks(scenePath, ButtonAudioScriptGuid))
            {
                Assert.That(audioComponent.Value, Does.Match(@"_clickClip: \{fileID: -?[1-9]\d*, guid: [0-9a-f]+, type: \d+\}"), $"{scenePath}: click clip");
                Assert.That(audioComponent.Value, Does.Match(@"_outputAudioMixerGroup: \{fileID: -?[1-9]\d*, guid: [0-9a-f]+, type: \d+\}"), $"{scenePath}: mixer group");
            }
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
    public void ReleaseTextsUseReleaseFont()
    {
        string[] assetPaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .ToArray();

        foreach (string assetPath in assetPaths)
        {
            string assetText = File.ReadAllText(assetPath);
            MatchCollection fontReferences = Regex.Matches(assetText, @"m_fontAsset: \{fileID: \d+, guid: ([0-9a-f]+), type: \d+\}");

            foreach (Match fontReference in fontReferences)
                Assert.That(fontReference.Groups[1].Value, Is.EqualTo(ReleaseFontGuid), assetPath);
        }
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
            while (shelf.Count > 0)
            {
                if (IsMatch(shelf[0]))
                    shelf[0].Clear();

                if (shelf[0].Count > 0)
                    break;

                shelf.RemoveAt(0);
            }
        }
    }

    private static bool HasDifferentItems(IReadOnlyList<ShelfItem> items)
    {
        ShelfItem[] presentItems = items.Where(item => item != null).ToArray();
        return presentItems.Length == ShelfLayer.SlotCount && presentItems.Select(item => item.Type).Distinct().Count() > 1;
    }

    private static string CreateLayerPattern(ShelfLayerDefinition layer)
    {
        return string.Concat(layer.ItemPrefabs.Where(item => item != null).Select(item => (int)item.Type).OrderBy(itemType => itemType));
    }

    private static Shelf CreateShelfWithMatch(Transform parent)
    {
        GameObject shelfObject = new GameObject(nameof(Shelf));
        shelfObject.transform.SetParent(parent);
        Shelf shelf = shelfObject.AddComponent<Shelf>();

        GameObject layerObject = new GameObject(nameof(ShelfLayer));
        layerObject.transform.SetParent(shelfObject.transform);
        ShelfLayer layer = layerObject.AddComponent<ShelfLayer>();
        List<ShelfSlot> slots = new List<ShelfSlot>();

        for (int index = 0; index < ShelfLayer.SlotCount; index++)
        {
            GameObject slotObject = new GameObject($"Slot{index}");
            slotObject.transform.SetParent(layerObject.transform);
            ShelfSlot slot = slotObject.AddComponent<ShelfSlot>();

            GameObject anchorObject = new GameObject("ItemAnchor");
            anchorObject.transform.SetParent(slotObject.transform);
            SetField(slot, "_itemAnchor", anchorObject.transform);

            GameObject itemObject = new GameObject($"Item{index}");
            ShelfItem item = itemObject.AddComponent<ShelfItem>();
            SetField(item, "_type", ItemType.Ball);
            slot.PlaceItem(item);
            slots.Add(slot);
        }

        SetField(layer, "_slots", slots);
        SetField(shelf, "_shelfLayers", new List<ShelfLayer> { layer });
        return shelf;
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
        return (T)field.GetValue(target);
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
        HashSet<string> gameObjects = new HashSet<string>();

        foreach (Match componentBlock in CollectComponentBlocks(assetPath, scriptGuid))
        {
            Match gameObjectReference = Regex.Match(componentBlock.Value, @"m_GameObject: \{fileID: (\d+)\}");
            if (gameObjectReference.Success && gameObjectReference.Groups[1].Value != "0")
                gameObjects.Add(gameObjectReference.Groups[1].Value);
        }

        return gameObjects;
    }

    private static IEnumerable<Match> CollectComponentBlocks(string assetPath, string scriptGuid)
    {
        string assetText = File.ReadAllText(assetPath);
        MatchCollection componentBlocks = Regex.Matches(assetText, @"--- !u!114 &.*?(?=--- !u!|\z)", RegexOptions.Singleline);
        return componentBlocks.Cast<Match>().Where(componentBlock => componentBlock.Value.Contains($"guid: {scriptGuid}"));
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
