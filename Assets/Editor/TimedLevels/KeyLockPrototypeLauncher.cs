using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class KeyLockPrototypeLauncher
{
    private const string PrototypeNumberKey = "KeyLockPrototypeLauncher.PrototypeNumber";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/SixthLevel.unity";
    private const string SceneName = "SixthLevel";
    private const string PrototypeDefinitionPathFormat = "Assets/Levels/Prototype/TimedLevel_KeyPrototype_{0}.asset";
    private const int DisplayedLevelNumber = 26;
    private const int SeedLevelNumberBase = 90;

    private static LevelEntry _prototypeLevel;

    static KeyLockPrototypeLauncher()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Timed Levels/Play Key Prototype/1")]
    public static void PlayFirst() => Play(1);

    [MenuItem("Tools/Timed Levels/Play Key Prototype/2")]
    public static void PlaySecond() => Play(2);

    [MenuItem("Tools/Timed Levels/Generate Key Prototypes")]
    public static void GenerateAll()
    {
        EditorApplication.update -= GenerateOnNextUpdate;
        EditorApplication.update += GenerateOnNextUpdate;
    }

    private static void GenerateOnNextUpdate()
    {
        EditorApplication.update -= GenerateOnNextUpdate;
        SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            for (int prototypeNumber = 1; prototypeNumber <= 2; prototypeNumber++)
                TimedLevelVariantGenerator.GenerateForDefinition(LoadDefinition(prototypeNumber), SceneName, SeedLevelNumberBase + prototypeNumber);

            AssetDatabase.SaveAssets();
            Debug.Log("KEY_PROTOTYPES_GENERATED_PASS");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    private static void Play(int prototypeNumber)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before starting a key prototype.");

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (LoadDefinition(prototypeNumber).Variants.Count == 0)
            throw new InvalidOperationException($"Key prototype {prototypeNumber} has no variants. Run Tools/Timed Levels/Generate Key Prototypes.");

        SessionState.SetInt(PrototypeNumberKey, prototypeNumber);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static TimedLevelDefinition LoadDefinition(int prototypeNumber)
    {
        string path = string.Format(PrototypeDefinitionPathFormat, prototypeNumber);
        TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(path);

        if (definition == null)
            throw new InvalidOperationException($"Key prototype definition was not found at {path}.");

        return definition;
    }

    private static LevelEntry CreatePrototypeLevel(TimedLevelDefinition definition, int prototypeNumber)
    {
        LevelEntry level = ScriptableObject.CreateInstance<LevelEntry>();
        level.name = $"KeyPrototypeLevel_{prototypeNumber}";
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
