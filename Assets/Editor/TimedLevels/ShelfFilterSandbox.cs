using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ShelfFilterSandbox
{
    private const string PendingLevelKey = "ShelfFilterSandbox.PendingLevel";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/ThirdLevel.unity";

    static ShelfFilterSandbox()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Timed Levels/Play Filter Level/1")]
    public static void PlayFirstLevel() => Play(1);

    [MenuItem("Tools/Timed Levels/Play Filter Level/2")]
    public static void PlaySecondLevel() => Play(2);

    [MenuItem("Tools/Timed Levels/Play Filter Level/3")]
    public static void PlayThirdLevel() => Play(3);

    [MenuItem("Tools/Timed Levels/Play Filter Level/4")]
    public static void PlayFourthLevel() => Play(4);

    [MenuItem("Tools/Timed Levels/Play Filter Level/5")]
    public static void PlayFifthLevel() => Play(5);

    public static string GetLevelEntryPath(int chapterLevel) => $"Assets/Levels/Menu/LevelEntry_C2_{chapterLevel:00}.asset";

    private static void Play(int chapterLevel)
    {
        string levelPath = GetLevelEntryPath(chapterLevel);

        if (AssetDatabase.LoadAssetAtPath<LevelEntry>(levelPath) == null)
            throw new InvalidOperationException($"{levelPath} does not exist yet: chapter 2 levels are created in stage 7.1 of the shelf filter plan.");

        if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SessionState.SetString(PendingLevelKey, levelPath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode)
            return;

        string levelPath = SessionState.GetString(PendingLevelKey, string.Empty);

        if (levelPath.Length == 0)
            return;

        SessionState.EraseString(PendingLevelKey);
        LevelEntry level = AssetDatabase.LoadAssetAtPath<LevelEntry>(levelPath);
        AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath).Select(level);
        SceneManager.LoadScene(level.SceneName);
    }
}
