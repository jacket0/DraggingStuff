using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CyclicShelfPrototypeLauncher
{
    private const string PrototypeNumberKey = "CyclicShelfPrototypeLauncher.PrototypeNumber";
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/SeventhLevel.unity";
    private const string SceneName = "SeventhLevel";
    private const string ItemCatalogSceneName = "SixthLevel";
    private const string PrototypeDefinitionPathFormat = "Assets/Levels/Prototype/TimedLevel_CyclicPrototype_{0}.asset";
    private const int PrototypeCount = 4;
    private const int DisplayedLevelNumber = 26;
    private const int SeedLevelNumberBase = 95;
    private const int TimeLimitSeconds = 160;
    private const int TwoStarTimeSeconds = 120;
    private const int ThreeStarTimeSeconds = 90;
    private const int EmptyColumnCount = 3;
    private const int MinimumItemsBehindShutter = 3;
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

    private static readonly PrototypeSettings[] Prototypes =
    {
        new PrototypeSettings(SwingShelves, SwingGroups, 0),
        new PrototypeSettings(SwingShelves, SwingGroups, 2),
        new PrototypeSettings(CarouselShelves, CarouselGroups, 2),
        new PrototypeSettings(RotorShelves, CarouselGroups, 2)
    };

    private static LevelEntry _prototypeLevel;

    public static int CurrentPrototypeNumber => SessionState.GetInt(PrototypeNumberKey, 0);

    static CyclicShelfPrototypeLauncher()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Timed Levels/Play Cyclic Prototype/1 Simple Swing")]
    public static void PlayFirst() => Play(1);

    [MenuItem("Tools/Timed Levels/Play Cyclic Prototype/2 Swing With Choice")]
    public static void PlaySecond() => Play(2);

    [MenuItem("Tools/Timed Levels/Play Cyclic Prototype/3 Carousel")]
    public static void PlayThird() => Play(3);

    [MenuItem("Tools/Timed Levels/Play Cyclic Prototype/4 Rotor")]
    public static void PlayFourth() => Play(4);

    [MenuItem("Tools/Timed Levels/Generate Cyclic Prototypes")]
    public static void GenerateAll()
    {
        EditorApplication.update -= GenerateOnNextUpdate;
        EditorApplication.update += GenerateOnNextUpdate;
    }

    [MenuItem("Tools/Timed Levels/Cyclic Shelves/Run Prototype Bot")]
    public static void RunBot()
    {
        EditorApplication.update -= RunBotOnNextUpdate;
        EditorApplication.update += RunBotOnNextUpdate;
    }

    private static void RunBotOnNextUpdate()
    {
        EditorApplication.update -= RunBotOnNextUpdate;

        for (int prototypeNumber = 1; prototypeNumber <= PrototypeCount; prototypeNumber++)
            Debug.Log(CyclicShelfBalanceBot.Run(LoadDefinition(prototypeNumber), $"cyclic prototype {prototypeNumber}", BotRunsPerPolicy));

        Debug.Log("CYCLIC_PROTOTYPE_BOT_PASS");
    }

    private static void GenerateOnNextUpdate()
    {
        EditorApplication.update -= GenerateOnNextUpdate;
        SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            for (int prototypeNumber = 1; prototypeNumber <= PrototypeCount; prototypeNumber++)
            {
                WriteDefinition(prototypeNumber, LoadItemCatalog());
                TimedLevelVariantGenerator.GenerateForDefinition(LoadDefinition(prototypeNumber), SceneName, SeedLevelNumberBase + prototypeNumber);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("CYCLIC_PROTOTYPES_GENERATED_PASS");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    private static void Play(int prototypeNumber)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before starting a cyclic prototype.");

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (LoadDefinition(prototypeNumber).Variants.Count == 0)
            throw new InvalidOperationException($"Cyclic prototype {prototypeNumber} has no variants. Run Tools/Timed Levels/Generate Cyclic Prototypes.");

        SessionState.SetInt(PrototypeNumberKey, prototypeNumber);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static ShelfItemCatalog LoadItemCatalog()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null)
            throw new InvalidOperationException($"{CatalogPath} was not found.");

        return catalog.Levels.First(level => level.SceneName == ItemCatalogSceneName).Definition.ItemCatalog;
    }

    private static void WriteDefinition(int prototypeNumber, ShelfItemCatalog itemCatalog)
    {
        string path = string.Format(PrototypeDefinitionPathFormat, prototypeNumber);
        TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(path);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        PrototypeSettings settings = Prototypes[prototypeNumber - 1];
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = TimeLimitSeconds;
        serializedDefinition.FindProperty("_twoStarTimeSeconds").intValue = TwoStarTimeSeconds;
        serializedDefinition.FindProperty("_threeStarTimeSeconds").intValue = ThreeStarTimeSeconds;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = EmptyColumnCount;
        serializedDefinition.FindProperty("_itemCatalog").objectReferenceValue = itemCatalog;
        serializedDefinition.FindProperty("_minimumItemsBehindShutter").intValue = MinimumItemsBehindShutter;
        serializedDefinition.FindProperty("_minimumPreparationMoves").intValue = settings.MinimumPreparationMoves;
        CyclicShelfDefinitionData.WriteAll(serializedDefinition.FindProperty("_cyclicShelves"), settings.CyclicShelves);

        SerializedProperty groupsProperty = serializedDefinition.FindProperty("_itemGroups");
        groupsProperty.arraySize = settings.Groups.Count;

        for (int index = 0; index < settings.Groups.Count; index++)
        {
            SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(index);
            groupProperty.FindPropertyRelative("_type").enumValueIndex = (int)settings.Groups[index].Type;
            groupProperty.FindPropertyRelative("_groupCount").intValue = settings.Groups[index].Count;
        }

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssets();
    }

    private static TimedLevelDefinition LoadDefinition(int prototypeNumber)
    {
        string path = string.Format(PrototypeDefinitionPathFormat, prototypeNumber);
        TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(path);

        if (definition == null)
            throw new InvalidOperationException($"Cyclic prototype definition was not found at {path}. Run Tools/Timed Levels/Generate Cyclic Prototypes.");

        return definition;
    }

    private static LevelEntry CreatePrototypeLevel(TimedLevelDefinition definition, int prototypeNumber)
    {
        LevelEntry level = ScriptableObject.CreateInstance<LevelEntry>();
        level.name = $"CyclicPrototypeLevel_{prototypeNumber}";
        level.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedLevel = new SerializedObject(level);
        serializedLevel.FindProperty("_number").intValue = DisplayedLevelNumber;
        serializedLevel.FindProperty("_sceneName").stringValue = SceneName;
        serializedLevel.FindProperty("_definition").objectReferenceValue = definition;
        serializedLevel.FindProperty("_available").boolValue = true;
        serializedLevel.ApplyModifiedPropertiesWithoutUndo();
        return level;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        int prototypeNumber = SessionState.GetInt(PrototypeNumberKey, 0);

        if (prototypeNumber == 0 || scene.name != SceneName)
            return;

        LevelSelectionState selection = AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath);

        if (selection == null)
            throw new InvalidOperationException($"Level selection was not found at {SelectionPath}.");

        _prototypeLevel ??= CreatePrototypeLevel(LoadDefinition(prototypeNumber), prototypeNumber);
        selection.Select(_prototypeLevel);

        foreach (LevelResultRecorder recorder in UnityEngine.Object.FindObjectsOfType<LevelResultRecorder>(true))
            recorder.enabled = false;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetInt(PrototypeNumberKey, 0);
            _prototypeLevel = null;
        }
    }

    private sealed class PrototypeSettings
    {
        public IReadOnlyList<CyclicShelfDefinitionData> CyclicShelves { get; }
        public IReadOnlyList<(ItemType Type, int Count)> Groups { get; }
        public int MinimumPreparationMoves { get; }

        public PrototypeSettings(IReadOnlyList<CyclicShelfDefinitionData> cyclicShelves, IReadOnlyList<(ItemType Type, int Count)> groups, int minimumPreparationMoves)
        {
            CyclicShelves = cyclicShelves;
            Groups = groups;
            MinimumPreparationMoves = minimumPreparationMoves;
        }
    }
}
