using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CyclicShelfPlaytestLog
{
    private const string EnabledKey = "CyclicShelfPlaytestLog.Enabled";
    private const string MenuPath = "Tools/Cyclic Shelves/Playtest Log";
    private const string SceneName = "SeventhLevel";
    private const string LogFolder = "LocalOutput/cyclic-shelves";
    private const string LogFileName = "chapter-playtest.csv";
    private const string Header = "date;level;variant;seed;solution_moves;won;stars;time_s;moves;matching_moves;switches;early_flips;open_all;carry_outs;items_at_closings";

    private static readonly string[] AutomatedRunKeys =
    {
        "CyclicShelfPlayModeValidation.Active",
        "TimedCampaignPlayModeValidation.Active"
    };

    private static readonly List<int> ItemsAtClosings = new List<int>();
    private static LevelSession _session;
    private static ShelfBoard _shelfBoard;
    private static CyclicShelvesController _cyclicShelves;
    private static ShelfItemDragController _dragController;
    private static ShelfItem _draggedItem;
    private static ShelfColumn _dragSourceColumn;
    private static bool _isDragFromClosingShelf;
    private static int _moveCount;
    private static int _matchingMoveCount;
    private static int _switchCount;
    private static int _earlyFlipCount;
    private static int _openAllCount;
    private static int _carryOutCount;
    private static float _firstMoveTime = -1f;

    static CyclicShelfPlaytestLog()
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
        Debug.Log(isEnabled ? $"CYCLIC_PLAYTEST_LOG: on, results go to {LogPath}" : "CYCLIC_PLAYTEST_LOG: off");
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
        _shelfBoard = UnityEngine.Object.FindObjectOfType<ShelfBoard>();
        _cyclicShelves = UnityEngine.Object.FindObjectOfType<CyclicShelvesController>();
        _dragController = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();

        if (_session == null || _shelfBoard == null || _cyclicShelves == null || _dragController == null)
            return;

        _session.SessionStarted += HandleSessionStarted;
        _session.MatchingMoveResolved += HandleMatchingMoveResolved;
        _session.RunEnded += HandleRunEnded;
        _cyclicShelves.ShelvesSwitched += HandleShelvesSwitched;
        _cyclicShelves.RescueApplied += HandleRescueApplied;
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
            _session.MatchingMoveResolved -= HandleMatchingMoveResolved;
            _session.RunEnded -= HandleRunEnded;
        }

        if (_cyclicShelves != null)
        {
            _cyclicShelves.ShelvesSwitched -= HandleShelvesSwitched;
            _cyclicShelves.RescueApplied -= HandleRescueApplied;
        }

        if (_dragController != null)
        {
            _dragController.DragStarting -= HandleDragStarting;
            _dragController.DragEnded -= HandleDragEnded;
        }

        _session = null;
        _shelfBoard = null;
        _cyclicShelves = null;
        _dragController = null;
    }

    private static void HandleSessionStarted()
    {
        ItemsAtClosings.Clear();
        _moveCount = 0;
        _matchingMoveCount = 0;
        _switchCount = 0;
        _earlyFlipCount = 0;
        _openAllCount = 0;
        _carryOutCount = 0;
        _firstMoveTime = -1f;
        _draggedItem = null;
        _dragSourceColumn = null;
        _isDragFromClosingShelf = false;
    }

    private static void HandleDragStarting(ShelfItem item)
    {
        _draggedItem = item;
        _dragSourceColumn = item.Column;
        _isDragFromClosingShelf = IsClosingOnNextMatchingMove(FindShelf(item.Column));
    }

    private static void HandleDragEnded()
    {
        if (_draggedItem != null && _draggedItem.Column != _dragSourceColumn)
        {
            _moveCount++;

            if (_firstMoveTime < 0f)
                _firstMoveTime = Time.time;

            if (_isDragFromClosingShelf && FindShelf(_draggedItem.Column) != FindShelf(_dragSourceColumn))
                _carryOutCount++;
        }

        _draggedItem = null;
        _dragSourceColumn = null;
        _isDragFromClosingShelf = false;
    }

    private static void HandleMatchingMoveResolved() => _matchingMoveCount++;

    private static void HandleShelvesSwitched(IReadOnlyList<Shelf> shelves)
    {
        _switchCount++;

        foreach (Shelf shelf in shelves.Where(_shelfBoard.IsShelfClosed))
            ItemsAtClosings.Add(shelf.Columns.Sum(column => column.Count));
    }

    private static void HandleRescueApplied(bool isRepeated)
    {
        if (isRepeated)
            _openAllCount++;
        else
            _earlyFlipCount++;
    }

    private static bool IsClosingOnNextMatchingMove(Shelf shelf)
    {
        ShelfCycleSnapshot cycle = _shelfBoard.GetCycle(shelf);
        return cycle != null && cycle.MovesToSwitch == 1 && _shelfBoard.IsCyclePhaseOpen(shelf) && !_shelfBoard.IsShelfClosed(shelf);
    }

    private static Shelf FindShelf(ShelfColumn column) => _shelfBoard.Shelves.FirstOrDefault(shelf => shelf.Columns.Contains(column));

    private static void HandleRunEnded(LevelRunResult result)
    {
        TimedLevelDefinition definition = _session.CurrentLevel.Definition;

        if (!AssetDatabase.Contains(definition))
            return;

        int solutionMoves = definition.Variants[result.VariantIndex].MoveCount;
        string time = (result.ActiveTimeMilliseconds / 1000f).ToString("F1", CultureInfo.InvariantCulture);
        string closings = string.Join(" ", ItemsAtClosings);
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
            _matchingMoveCount,
            _switchCount,
            _earlyFlipCount,
            _openAllCount,
            _carryOutCount,
            closings);
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));

        if (!File.Exists(LogPath))
            File.WriteAllText(LogPath, Header + Environment.NewLine);

        File.AppendAllText(LogPath, line + Environment.NewLine);
        Debug.Log(
            $"CYCLIC_PLAYTEST: level {result.LevelNumber}, variant {result.VariantIndex + 1}, {(result.Won ? "won" : "lost")}, {result.Stars} stars, {time} s, " +
            $"{_moveCount} moves ({_matchingMoveCount} matching, solution {solutionMoves}), {_switchCount} switches, " +
            $"early flips {_earlyFlipCount}, open all {_openAllCount}, carry-outs {_carryOutCount}, items at closings: {closings}");
    }
}
