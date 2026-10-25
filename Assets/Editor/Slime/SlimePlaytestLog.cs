using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SlimePlaytestLog
{
    private const string EnabledKey = "SlimePlaytestLog.Enabled";
    private const string MenuPath = "Tools/Slime/Playtest Log";
    private const string SceneName = "EighthLevel";
    private const string LogFolder = "LocalOutput/slime";
    private const string LogFileName = "prototype-playtest.csv";
    private const string Header = "date;definition;variant;seed;solution_moves;won;stars;time_s;moves;matching_moves;cleanings;cleaned_columns;growths;growths_inward;growths_onto_items;placed_into_slime;rejected_pick_ups;deadlock;shelf_lost;max_sticky;sticky_at_end";

    private static readonly string[] AutomatedRunKeys =
    {
        "SlimePlayModeValidation.Active",
        "TimedCampaignPlayModeValidation.Active"
    };

    private static LevelSession _session;
    private static ShelfBoard _shelfBoard;
    private static SlimeController _slime;
    private static ShelfItemDragController _dragController;
    private static ShelfItem _draggedItem;
    private static ShelfColumn _dragSourceColumn;
    private static int _moveCount;
    private static int _matchingMoveCount;
    private static int _cleaningCount;
    private static int _cleanedColumnCount;
    private static int _growthCount;
    private static int _inwardGrowthCount;
    private static int _growthOntoItemsCount;
    private static int _placedIntoSlimeCount;
    private static int _rejectedPickUpCount;
    private static bool _isDeadlocked;
    private static string _lostShelf;
    private static int _maximumStickyCount;

    static SlimePlaytestLog()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static string LogPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, LogFolder, LogFileName);

    private static int StickyCount => _shelfBoard.HasSlime ? _shelfBoard.Slime.StickyColumns.Count : 0;

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        bool isEnabled = !EditorPrefs.GetBool(EnabledKey, false);
        EditorPrefs.SetBool(EnabledKey, isEnabled);
        Debug.Log(isEnabled ? $"SLIME_PLAYTEST_LOG: on, results go to {LogPath}" : "SLIME_PLAYTEST_LOG: off");
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
        _slime = UnityEngine.Object.FindObjectOfType<SlimeController>();
        _dragController = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();

        if (_session == null || _shelfBoard == null || _slime == null || _dragController == null)
            return;

        _session.SessionStarted += HandleSessionStarted;
        _session.MatchingMoveResolved += HandleMatchingMoveResolved;
        _session.RunEnded += HandleRunEnded;
        _slime.SlimeCleaned += HandleSlimeCleaned;
        _slime.SlimeGrown += HandleSlimeGrown;
        _slime.DeadlockReached += HandleDeadlockReached;
        _slime.ShelfLost += HandleShelfLost;
        _slime.PickUpRejected += HandlePickUpRejected;
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

        if (_slime != null)
        {
            _slime.SlimeCleaned -= HandleSlimeCleaned;
            _slime.SlimeGrown -= HandleSlimeGrown;
            _slime.DeadlockReached -= HandleDeadlockReached;
            _slime.ShelfLost -= HandleShelfLost;
            _slime.PickUpRejected -= HandlePickUpRejected;
        }

        if (_dragController != null)
        {
            _dragController.DragStarting -= HandleDragStarting;
            _dragController.DragEnded -= HandleDragEnded;
        }

        _session = null;
        _shelfBoard = null;
        _slime = null;
        _dragController = null;
    }

    private static void HandleSessionStarted()
    {
        _moveCount = 0;
        _matchingMoveCount = 0;
        _cleaningCount = 0;
        _cleanedColumnCount = 0;
        _growthCount = 0;
        _inwardGrowthCount = 0;
        _growthOntoItemsCount = 0;
        _placedIntoSlimeCount = 0;
        _rejectedPickUpCount = 0;
        _isDeadlocked = false;
        _lostShelf = "";
        _maximumStickyCount = StickyCount;
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

            if (_shelfBoard.IsSticky(_draggedItem.Column))
                _placedIntoSlimeCount++;
        }

        _draggedItem = null;
        _dragSourceColumn = null;
    }

    private static void HandleMatchingMoveResolved(IReadOnlyList<Shelf> matchedShelves) => _matchingMoveCount++;

    private static void HandleSlimeCleaned(IReadOnlyList<ColumnPosition> columns)
    {
        _cleaningCount++;
        _cleanedColumnCount += columns.Count;
    }

    private static void HandleSlimeGrown(ColumnPosition column)
    {
        _growthCount++;

        if (!_shelfBoard.Slime.PerimeterColumns.Contains(column))
            _inwardGrowthCount++;

        if (_shelfBoard.GetColumn(column).Count > 0)
            _growthOntoItemsCount++;

        _maximumStickyCount = Math.Max(_maximumStickyCount, StickyCount);
    }

    private static void HandleDeadlockReached() => _isDeadlocked = true;

    private static void HandleShelfLost(Shelf shelf) => _lostShelf = shelf.name;

    private static void HandlePickUpRejected(ShelfItem item) => _rejectedPickUpCount++;

    private static void HandleRunEnded(LevelRunResult result)
    {
        TimedLevelDefinition definition = _session.CurrentLevel.Definition;

        if (!AssetDatabase.Contains(definition))
            return;

        int solutionMoves = definition.Variants[result.VariantIndex].MoveCount;
        string time = (result.ActiveTimeMilliseconds / 1000f).ToString("F1", CultureInfo.InvariantCulture);
        int stickyAtEnd = StickyCount;
        string line = string.Join(";",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            definition.name,
            result.VariantIndex + 1,
            result.Seed,
            solutionMoves,
            result.Won,
            result.Stars,
            time,
            _moveCount,
            _matchingMoveCount,
            _cleaningCount,
            _cleanedColumnCount,
            _growthCount,
            _inwardGrowthCount,
            _growthOntoItemsCount,
            _placedIntoSlimeCount,
            _rejectedPickUpCount,
            _isDeadlocked,
            _lostShelf,
            _maximumStickyCount,
            stickyAtEnd);
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));

        if (!File.Exists(LogPath))
            File.WriteAllText(LogPath, Header + Environment.NewLine);

        File.AppendAllText(LogPath, line + Environment.NewLine);
        Debug.Log(
            $"SLIME_PLAYTEST: {definition.name}, variant {result.VariantIndex + 1}, {(result.Won ? "won" : "lost")}, {result.Stars} stars, {time} s, " +
            $"{_moveCount} moves ({_matchingMoveCount} matching, solution {solutionMoves}), cleanings {_cleaningCount} ({_cleanedColumnCount} columns), " +
            $"growths {_growthCount} ({_inwardGrowthCount} inward, {_growthOntoItemsCount} onto items), placed into slime {_placedIntoSlimeCount}, rejected pick-ups {_rejectedPickUpCount}, " +
            $"deadlock {_isDeadlocked}, shelf lost '{_lostShelf}', max sticky {_maximumStickyCount}, sticky at end {stickyAtEnd}");
    }
}
