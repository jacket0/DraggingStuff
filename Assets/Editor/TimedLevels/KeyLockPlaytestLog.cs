using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class KeyLockPlaytestLog
{
    private const string EnabledKey = "KeyLockPlaytestLog.Enabled";
    private const string MenuPath = "Tools/Key Locks/Playtest Log";
    private const string SceneName = "SixthLevel";
    private const string LogFolder = ".utmp/key-locks";
    private const string LogFileName = "playtest.csv";
    private const string Header = "date;level;variant;seed;solution_moves;won;stars;time_s;moves;safe_openings";

    private static readonly string[] AutomatedRunKeys =
    {
        "KeyLockPlayModeValidation.Active",
        "KeyLockCollectionRecorder.Active",
        "TimedCampaignPlayModeValidation.Active"
    };

    private static readonly List<string> SafeOpenings = new List<string>();
    private static LevelSession _session;
    private static KeyLocksController _keyLocks;
    private static ShelfItemDragController _dragController;
    private static ShelfItem _draggedItem;
    private static ShelfColumn _dragSourceColumn;
    private static int _moveCount;
    private static float _firstMoveTime = -1f;

    static KeyLockPlaytestLog()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static string LogPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, LogFolder, LogFileName);

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        bool isEnabled = !EditorPrefs.GetBool(EnabledKey, false);
        EditorPrefs.SetBool(EnabledKey, isEnabled);
        Debug.Log(isEnabled ? $"KEY_PLAYTEST_LOG: on, results go to {LogPath}" : "KEY_PLAYTEST_LOG: off");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(EnabledKey, false));
        return true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Unsubscribe();

        if (!EditorApplication.isPlaying || !EditorPrefs.GetBool(EnabledKey, false) || scene.name != SceneName || AutomatedRunKeys.Any(key => SessionState.GetBool(key, false)))
            return;

        _session = UnityEngine.Object.FindObjectOfType<LevelSession>();
        _keyLocks = UnityEngine.Object.FindObjectOfType<KeyLocksController>();
        _dragController = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();

        if (_session == null || _keyLocks == null || _dragController == null)
            return;

        _session.SessionStarted += HandleSessionStarted;
        _session.RunEnded += HandleRunEnded;
        _keyLocks.SafeOpened += HandleSafeOpened;
        _dragController.DragStarting += HandleDragStarting;
        _dragController.DragEnded += HandleDragEnded;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode)
            Unsubscribe();
    }

    private static void Unsubscribe()
    {
        if (_session != null)
        {
            _session.SessionStarted -= HandleSessionStarted;
            _session.RunEnded -= HandleRunEnded;
        }

        if (_keyLocks != null)
            _keyLocks.SafeOpened -= HandleSafeOpened;

        if (_dragController != null)
        {
            _dragController.DragStarting -= HandleDragStarting;
            _dragController.DragEnded -= HandleDragEnded;
        }

        _session = null;
        _keyLocks = null;
        _dragController = null;
    }

    private static void HandleSessionStarted()
    {
        SafeOpenings.Clear();
        _moveCount = 0;
        _firstMoveTime = -1f;
        _draggedItem = null;
        _dragSourceColumn = null;
    }

    private static void HandleDragStarting(ShelfItem item)
    {
        _draggedItem = item;
        _dragSourceColumn = item.Column;
    }

    private static void HandleDragEnded()
    {
        if (_draggedItem != null && _draggedItem.Column != _dragSourceColumn)
        {
            _moveCount++;

            if (_firstMoveTime < 0f)
                _firstMoveTime = Time.time;
        }

        _draggedItem = null;
        _dragSourceColumn = null;
    }

    private static void HandleSafeOpened(Shelf safe)
    {
        float seconds = _firstMoveTime < 0f ? 0f : Time.time - _firstMoveTime;
        SafeOpenings.Add($"{safe.name} move {_moveCount} at {seconds.ToString("F1", CultureInfo.InvariantCulture)}s");
    }

    private static void HandleRunEnded(LevelRunResult result)
    {
        if (!AssetDatabase.Contains(_session.CurrentLevel))
            return;

        int solutionMoves = _session.CurrentLevel.Definition.Variants[result.VariantIndex].MoveCount;
        string time = (result.ActiveTimeMilliseconds / 1000f).ToString("F1", CultureInfo.InvariantCulture);
        string openings = string.Join(", ", SafeOpenings);
        string line = string.Join(";",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            result.LevelNumber,
            result.VariantIndex + 1,
            result.Seed,
            solutionMoves,
            result.Won,
            result.Stars,
            time,
            _moveCount,
            openings);
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));

        if (!File.Exists(LogPath))
            File.WriteAllText(LogPath, Header + Environment.NewLine);

        File.AppendAllText(LogPath, line + Environment.NewLine);
        Debug.Log($"KEY_PLAYTEST: level {result.LevelNumber}, variant {result.VariantIndex + 1}, {(result.Won ? "won" : "lost")}, {result.Stars} stars, {time} s, {_moveCount} moves (solution {solutionMoves}), safes: {openings}");
    }
}
