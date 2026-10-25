using System;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class SlimePrototypeLauncher
{
    private const string PrototypeNumberKey = "SlimePrototypeLauncher.PrototypeNumber";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/EighthLevel.unity";
    private const string SceneName = "EighthLevel";
    private const string PrototypeDefinitionPathFormat = "Assets/Levels/Prototype/TimedLevel_SlimePrototype_{0}.asset";
    private const int DisplayedLevelNumber = 31;
    private const int SeedLevelNumberBase = 97;
    private const int TimeLimitSeconds = 240;
    private const int TwoStarTimeSeconds = 200;
    private const int ThreeStarTimeSeconds = 160;
    private const int BotRunsPerPolicy = 10;

    private static readonly int[] PrototypeGrowthPeriods = { 5, 3, 2 };
    private static readonly ColumnPosition[] StartStickyColumns = { new ColumnPosition(7, 0), new ColumnPosition(10, 2) };

    private static LevelEntry _prototypeLevel;

    static SlimePrototypeLauncher()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Timed Levels/Play Slime Prototype/1")]
    public static void PlayFirst() => Play(1);

    [MenuItem("Tools/Timed Levels/Play Slime Prototype/2")]
    public static void PlaySecond() => Play(2);

    [MenuItem("Tools/Timed Levels/Play Slime Prototype/3")]
    public static void PlayThird() => Play(3);

    [MenuItem("Tools/Timed Levels/Generate Slime Prototypes")]
    public static void GenerateAll()
    {
        EditorApplication.update -= GenerateOnNextUpdate;
        EditorApplication.update += GenerateOnNextUpdate;
    }

    [MenuItem("Tools/Timed Levels/Slime/Run Prototype Bot")]
    public static void RunBot()
    {
        for (int prototypeNumber = 1; prototypeNumber <= PrototypeGrowthPeriods.Length; prototypeNumber++)
            Debug.Log(SlimeBalanceBot.Run(LoadDefinition(prototypeNumber), $"slime prototype {prototypeNumber}", BotRunsPerPolicy));

        Debug.Log("SLIME_PROTOTYPE_BOT_PASS");
    }

    private static void GenerateOnNextUpdate()
    {
        EditorApplication.update -= GenerateOnNextUpdate;
        SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            for (int prototypeNumber = 1; prototypeNumber <= PrototypeGrowthPeriods.Length; prototypeNumber++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                TimedLevelDefinition definition = WriteDefinition(prototypeNumber);
                TimedLevelVariantGenerator.GenerateForDefinition(definition, SceneName, SeedLevelNumberBase + prototypeNumber);
                Debug.Log($"SLIME_PROTOTYPE {prototypeNumber}: {LoadDefinition(prototypeNumber).Variants.Count} variants in {stopwatch.Elapsed.TotalSeconds:F1} s.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("SLIME_PROTOTYPES_GENERATED_PASS");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    private static TimedLevelDefinition WriteDefinition(int prototypeNumber)
    {
        string path = string.Format(PrototypeDefinitionPathFormat, prototypeNumber);
        TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(path);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        SlimeGenerationPreview.ConfigureDefinition(
            definition,
            StartStickyColumns,
            PrototypeGrowthPeriods[prototypeNumber - 1],
            true,
            TimeLimitSeconds,
            TwoStarTimeSeconds,
            ThreeStarTimeSeconds);
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssets();
        return definition;
    }

    private static void Play(int prototypeNumber)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before starting a slime prototype.");

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (LoadDefinition(prototypeNumber).Variants.Count == 0)
            throw new InvalidOperationException($"Slime prototype {prototypeNumber} has no variants. Run Tools/Timed Levels/Generate Slime Prototypes.");

        SessionState.SetInt(PrototypeNumberKey, prototypeNumber);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static TimedLevelDefinition LoadDefinition(int prototypeNumber)
    {
        string path = string.Format(PrototypeDefinitionPathFormat, prototypeNumber);
        TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(path);

        if (definition == null)
            throw new InvalidOperationException($"Slime prototype definition was not found at {path}. Run Tools/Timed Levels/Generate Slime Prototypes.");

        return definition;
    }

    private static LevelEntry CreatePrototypeLevel(TimedLevelDefinition definition, int prototypeNumber)
    {
        LevelEntry level = ScriptableObject.CreateInstance<LevelEntry>();
        level.name = $"SlimePrototypeLevel_{prototypeNumber}";
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
}
