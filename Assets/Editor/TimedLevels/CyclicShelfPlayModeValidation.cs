using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CyclicShelfPlayModeValidation
{
    private const string ActiveKey = "CyclicShelfPlayModeValidation.Active";
    private const string CompletedKey = "CyclicShelfPlayModeValidation.Completed";
    private const string FailedKey = "CyclicShelfPlayModeValidation.Failed";
    private const string RunInBackgroundKey = "CyclicShelfPlayModeValidation.RunInBackground";
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const float MinimumClosedFraction = 0.9f;
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/SeventhLevel.unity";
    private const string SceneName = "SeventhLevel";
    public const string SwingLevelName = "CyclicShelfScenarioLevel_Swing";
    private const string RotorLevelName = "CyclicShelfScenarioLevel_Rotor";
    private const int ScenarioLevelNumber = 26;
    private const int SwingSeedBase = 2699000;
    private const int RotorSeedBase = 2699500;
    private const int MaximumScenarioSeeds = 300;
    private const int BoardShelfCount = 12;
    private const int ShelfCapacity = 3;
    private const int EmptyColumnCount = 3;
    private const int OpenCellIndex = 0;
    private const int MiddleCellIndex = 1;
    private const int ClosedCellIndex = 2;
    private const int MaximumWaitFrames = 3000;
    private const int PausedFrameCount = 10;
    private const float ScenarioTimeScale = 3f;
    private const float ExpiringCountdownRate = 100000f;

    private static readonly BoardMoveSimulator Simulator = new BoardMoveSimulator();
    public static readonly ColumnPosition StuckMoveSource = new ColumnPosition(4, 0);
    public static readonly ColumnPosition StuckMoveTarget = new ColumnPosition(5, 0);
    private static readonly ColumnPosition FirstMatchSource = new ColumnPosition(6, 0);
    private static readonly ColumnPosition FirstMatchTarget = new ColumnPosition(3, 2);
    private static readonly ColumnPosition SecondMatchSource = new ColumnPosition(7, 0);
    private static readonly ColumnPosition SecondMatchTarget = new ColumnPosition(4, 2);
    private static readonly ColumnPosition ThirdMatchSource = new ColumnPosition(8, 0);
    private static readonly ColumnPosition ThirdMatchTarget = new ColumnPosition(5, 2);

    private static readonly CyclicShelfDefinitionData[] SwingShelves =
    {
        new CyclicShelfDefinitionData(OpenCellIndex, true, 3, 3, 3),
        new CyclicShelfDefinitionData(ClosedCellIndex, false, 3, 3, 3)
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

    private static readonly (ItemType Type, int Count)[] RotorGroups =
    {
        (ItemType.Ball, 3), (ItemType.Bear, 2), (ItemType.Plant, 2), (ItemType.Lamp, 2), (ItemType.MapBall, 2), (ItemType.Beauty, 2), (ItemType.Toy, 2)
    };

    private static IEnumerator _scenarios;
    private static int _lastFrame = -1;
    private static string _unexpectedError;
    private static LevelSession _session;
    private static ShelfBoard _board;
    private static CyclicShelvesController _cyclicShelves;
    private static DepartureBoardView _departureBoard;
    private static ShelfShutterView[] _shutters;
    private static ShelfItemDragController _drag;
    private static MoveSuggestionProvider _suggestions;
    private static CycleRescueToastView _rescueToast;

    static CyclicShelfPlayModeValidation()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Application.logMessageReceived -= HandleLogMessage;
        Application.logMessageReceived += HandleLogMessage;
    }

    [MenuItem("Tools/Timed Levels/Validate Cyclic Shelf Play Mode")]
    public static void Begin()
    {
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the open scene before the cyclic shelf scenarios.");

        if (EditorBuildSettings.scenes.All(scene => scene.path != ScenePath))
            throw new InvalidOperationException($"{ScenePath} must be in the build settings: the restart scenario reloads it by index.");

        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(CompletedKey, false);
        SessionState.SetBool(FailedKey, false);
        SessionState.SetBool(RunInBackgroundKey, Application.runInBackground);
        Application.runInBackground = true;
        GetScenarioLevel(SwingLevelName);
        GetScenarioLevel(RotorLevelName);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (!EditorApplication.isPlaying)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Finish();

            return;
        }

        EditorApplication.QueuePlayerLoopUpdate();

        if (Time.frameCount == _lastFrame)
            return;

        _lastFrame = Time.frameCount;

        try
        {
            if (_unexpectedError != null)
                throw new InvalidOperationException($"Unexpected error during the scenario: {_unexpectedError}");

            _scenarios ??= RunScenarios().GetEnumerator();

            if (!_scenarios.MoveNext())
            {
                SessionState.SetBool(CompletedKey, true);
                Stop();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(FailedKey, true);
            Stop();
        }
    }

    private static IEnumerable RunScenarios()
    {
        Func<IEnumerable>[] scenarios =
        {
            ValidateSwingSolutionReplay,
            ValidateRotorSolutionReplay,
            ValidateTwoOverlappingMatchingMoves,
            ValidateDragFromClosingCell,
            ValidateEarlySwitch,
            ValidateRepeatedDeadlock,
            ValidatePauseDuringSwitch,
            ValidateRestartWithPendingSwitch,
            ValidateTimeoutDuringSwitch,
            ValidateDropOnClosedCell,
            ValidateSuggestions
        };

        foreach (Func<IEnumerable> scenario in scenarios)
        {
            int startFrame = Time.frameCount;

            foreach (object step in scenario())
                yield return step;

            Debug.Log($"CYCLIC_SHELF_SCENARIO_PASS: {scenario.Method.Name} in {Time.frameCount - startFrame} frames");
        }
    }

    private static IEnumerable ValidateSwingSolutionReplay() => ValidateSolutionReplay(SwingLevelName, "P1 swing");

    private static IEnumerable ValidateRotorSolutionReplay() => ValidateSolutionReplay(RotorLevelName, "P1 rotor");

    private static IEnumerable ValidateSolutionReplay(string levelName, string scenario)
    {
        LevelEntry level = GetScenarioLevel(levelName);

        foreach (object step in LoadLevel(level)) yield return step;

        TimedLevelVariant variant = level.Definition.Variants[0];
        BoardStateSnapshot expected = level.Definition.AddCyclicShelves(variant.CreateLayout());
        ExpectBoard(expected, $"{scenario} start");

        foreach (object step in WaitUntilViewsMatch(scenario)) yield return step;

        int switchCount = 0;

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            switchCount += Simulate(expected, move.Source, move.Target).SwitchedShelfIndexes.Count;
            expected = Move(expected, move.Source, move.Target);

            foreach (object step in WaitUntilSettled($"{scenario} move")) yield return step;

            ExpectBoard(expected, $"{scenario} move");

            foreach (object step in WaitUntilViewsMatch(scenario)) yield return step;
        }

        Expect(switchCount > 0, $"{scenario}: the solution must switch at least one cell");
        Expect(_board.IsCleared && _session.State == LevelState.Won, $"{scenario}: the solution must win the level");
    }

    private static IEnumerable ValidateTwoOverlappingMatchingMoves()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(ThreeMatchesLayout());
        expected = Move(expected, FirstMatchSource, FirstMatchTarget);
        Expect(!_session.IsBoardSettled, "P2: the first move must still be resolving");
        expected = Move(expected, SecondMatchSource, SecondMatchTarget);

        foreach (object step in WaitUntilSettled("P2")) yield return step;

        ExpectBoard(expected, "P2");
        Expect(_board.GetCycle(_board.Shelves[OpenCellIndex]).MovesToSwitch == 1, "P2: two matching moves must count twice");

        foreach (object step in WaitUntilViewsMatch("P2")) yield return step;
    }

    private static IEnumerable ValidateDragFromClosingCell()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(ThreeMatchesLayout());
        expected = Move(expected, FirstMatchSource, FirstMatchTarget);
        expected = Move(expected, SecondMatchSource, SecondMatchTarget);

        foreach (object step in WaitUntilSettled("P3 preparation")) yield return step;

        Shelf cell = _board.Shelves[OpenCellIndex];
        expected = Move(expected, ThirdMatchSource, ThirdMatchTarget);
        ShelfItem draggedItem = cell.Columns[0].FrontItem;
        Vector2 pressPosition = GetScreenCenter(draggedItem);
        Expect(_drag.TryBeginDrag(draggedItem, pressPosition), "P3: a drag from the closing cell must start");
        _drag.UpdateDrag(pressPosition + new Vector2(120f, -60f));

        while (!_board.IsShelfClosed(cell))
        {
            Expect(!_drag.IsDragging || !_board.IsShelfLocked(cell), "P3: the reservation must cancel the drag");
            yield return null;
        }

        Expect(
            !_drag.IsDragging && !_session.ItemAnimations.IsAnimating(draggedItem) && draggedItem.transform.parent == cell.ColumnViews[0].ItemAnchor,
            "P3: the cell must close only after the dragged item returns");

        foreach (object step in WaitUntilSettled("P3")) yield return step;

        ExpectBoard(expected, "P3");

        foreach (object step in WaitUntilViewsMatch("P3")) yield return step;
    }

    private static IEnumerable ValidateEarlySwitch()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot expected = ArrangeStuckSwing(false);
        List<bool> rescues = new List<bool>();
        List<string> toastTexts = new List<string>();

        void HandleRescueApplied(bool isRepeated)
        {
            rescues.Add(isRepeated);
            toastTexts.Add(_rescueToast.Text);
        }

        _cyclicShelves.RescueApplied += HandleRescueApplied;

        try
        {
            expected = Move(expected, StuckMoveSource, StuckMoveTarget);
            expected = Simulator.ApplyCycleRescue(expected, false, out _);

            foreach (object step in WaitUntilSettled("P4")) yield return step;
        }
        finally
        {
            _cyclicShelves.RescueApplied -= HandleRescueApplied;
        }

        Expect(rescues.SequenceEqual(new[] { false }), "P4: a deadlock must trigger exactly one early switch");
        Expect(_rescueToast.IsVisible && !string.IsNullOrEmpty(toastTexts[0]), "P4: the early switch must show the rescue toast");
        ExpectBoard(expected, "P4");
        Expect(_board.IsShelfClosed(_board.Shelves[OpenCellIndex]) && !_board.IsShelfClosed(_board.Shelves[ClosedCellIndex]), "P4: the early switch must swap the cells");

        foreach (object step in WaitUntilViewsMatch("P4")) yield return step;
    }

    private static IEnumerable ValidateRepeatedDeadlock()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot expected = ArrangeStuckSwing(true);
        List<bool> rescues = new List<bool>();
        List<string> toastTexts = new List<string>();

        void HandleRescueApplied(bool isRepeated)
        {
            rescues.Add(isRepeated);
            toastTexts.Add(_rescueToast.Text);
        }

        _cyclicShelves.RescueApplied += HandleRescueApplied;

        try
        {
            expected = Move(expected, StuckMoveSource, StuckMoveTarget);
            expected = Simulator.ApplyCycleRescue(expected, false, out _);
            expected = Simulator.ApplyCycleRescue(expected, true, out _);

            foreach (object step in WaitUntilSettled("P5")) yield return step;

            ExpectBoard(expected, "P5");
            Expect(rescues.SequenceEqual(new[] { false, true }), "P5: a repeated deadlock must open every cell");
            Expect(!_board.HasCycles && !_board.HasClosedShelves, "P5: after the repeated deadlock every cell stays open without a cycle");
            Expect(_rescueToast.IsVisible && toastTexts.Count == 2 && !string.IsNullOrEmpty(toastTexts[1]) && toastTexts[0] != toastTexts[1], "P5: the repeated deadlock must show its own rescue toast");

            foreach (object step in WaitUntilViewsMatch("P5")) yield return step;

            expected = Move(expected, new ColumnPosition(ClosedCellIndex, 0), new ColumnPosition(OpenCellIndex, 2));

            foreach (object step in WaitUntilSettled("P5 matching move")) yield return step;
        }
        finally
        {
            _cyclicShelves.RescueApplied -= HandleRescueApplied;
        }

        ExpectBoard(expected, "P5 matching move");
        Expect(!_board.HasClosedShelves && rescues.Count == 2, "P5: a matching move after the repeated deadlock must not close anything");
    }

    private static IEnumerable ValidatePauseDuringSwitch()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(ThreeMatchesLayout());
        expected = Move(expected, FirstMatchSource, FirstMatchTarget);
        expected = Move(expected, SecondMatchSource, SecondMatchTarget);
        expected = Move(expected, ThirdMatchSource, ThirdMatchTarget);

        foreach (object step in WaitUntil(() => _cyclicShelves.IsSwitching, "the switch")) yield return step;

        Expect(_session.TryPause(), "P6: the level must pause during the switch");
        ShelfShutterView closingShutter = GetShutter(OpenCellIndex);
        float pausedFraction = closingShutter.ClosedFraction;
        string pausedHash = BoardStateFingerprint.CreateHash(_board.CreateSnapshot());

        for (int frame = 0; frame < PausedFrameCount; frame++)
        {
            yield return null;
            Expect(_cyclicShelves.IsSwitching, "P6: the switch must stay unfinished while paused");
            Expect(closingShutter.ClosedFraction == pausedFraction, "P6: the shutter must not move while paused");
            Expect(BoardStateFingerprint.CreateHash(_board.CreateSnapshot()) == pausedHash, "P6: the board must not change while paused");
        }

        _session.Resume();
        Time.timeScale = ScenarioTimeScale;

        foreach (object step in WaitUntilSettled("P6 resume")) yield return step;

        ExpectBoard(expected, "P6");

        foreach (object step in WaitUntilViewsMatch("P6")) yield return step;
    }

    private static IEnumerable ValidateRestartWithPendingSwitch()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot start = _board.CreateSnapshot();
        Arrange(ThreeMatchesLayout());
        Move(_board.CreateSnapshot(), FirstMatchSource, FirstMatchTarget);
        Move(_board.CreateSnapshot(), SecondMatchSource, SecondMatchTarget);
        Move(_board.CreateSnapshot(), ThirdMatchSource, ThirdMatchTarget);

        foreach (object step in WaitUntil(() => _session.HasPendingBoardChanges, "pending switch")) yield return step;

        LevelSession previousSession = _session;
        previousSession.Restart();

        foreach (object step in WaitUntil(() => FindSession() != null && FindSession() != previousSession && FindSession().State == LevelState.Playing, "restart")) yield return step;

        CaptureScene();

        for (int frame = 0; frame < 30; frame++)
            yield return null;

        Expect(!_session.HasPendingBoardChanges && !_cyclicShelves.IsSwitching && _session.State == LevelState.Playing, "P7: a restart must drop the pending switch");
        ExpectBoard(start, "P7");

        foreach (object step in WaitUntilViewsMatch("P7")) yield return step;
    }

    private static IEnumerable ValidateTimeoutDuringSwitch()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(ThreeMatchesLayout());
        expected = Move(expected, FirstMatchSource, FirstMatchTarget);
        expected = Move(expected, SecondMatchSource, SecondMatchTarget);
        expected = Move(expected, ThirdMatchSource, ThirdMatchTarget);

        foreach (object step in WaitUntil(() => _cyclicShelves.IsSwitching, "the switch")) yield return step;

        _session.Timer.SetCountdownRate(ExpiringCountdownRate);

        foreach (object step in WaitUntil(() => _session.State != LevelState.Playing, "timer expiry")) yield return step;

        while (_session.Result == null)
        {
            Expect(_session.State == LevelState.Ending, "P8: the level must wait in the ending state");
            yield return null;
        }

        Expect(!_session.Result.Won && !_session.HasPendingBoardChanges && !_cyclicShelves.IsSwitching, "P8: the result must be decided after the switch");
        ExpectBoard(expected, "P8");
    }

    private static IEnumerable ValidateDropOnClosedCell()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(ThreeMatchesLayout());
        ShelfItem item = _board.Shelves[FirstMatchSource.ShelfIndex].Columns[FirstMatchSource.ColumnIndex].FrontItem;
        int rejectionCount = 0;

        void HandleDropRejected(Vector2 screenPosition) => rejectionCount++;

        _drag.DropRejected += HandleDropRejected;

        try
        {
            Expect(_drag.TryBeginDrag(item, GetScreenCenter(item)), "P9: the drag must start");
            _drag.EndDrag(Camera.main.WorldToScreenPoint(_board.Shelves[ClosedCellIndex].ColumnViews[2].ItemAnchor.position));

            foreach (object step in WaitUntilSettled("P9")) yield return step;
        }
        finally
        {
            _drag.DropRejected -= HandleDropRejected;
        }

        Expect(rejectionCount == 1, "P9: a drop on a closed cell must be refused");
        ExpectBoard(expected, "P9");
        Expect(item.transform.parent == _board.Shelves[FirstMatchSource.ShelfIndex].ColumnViews[FirstMatchSource.ColumnIndex].ItemAnchor, "P9: the refused item must return");
    }

    private static IEnumerable ValidateSuggestions()
    {
        foreach (object step in LoadSwingLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(
            Shelf(OpenCellIndex, Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            Shelf(ClosedCellIndex, Column(ItemType.Lamp), Column(ItemType.Toy), Column()),
            Shelf(3, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(4, Column(ItemType.Bear), Column(ItemType.Bear), Column()),
            Shelf(5, Column(ItemType.Plant), Column(ItemType.Plant), Column()),
            Shelf(6, Column(ItemType.Ball), Column(), Column()),
            Shelf(7, Column(ItemType.Bear), Column(), Column()),
            Shelf(8, Column(ItemType.Plant), Column(), Column()));

        foreach (object step in WaitUntilSettled("P10 arrange")) yield return step;

        ExpectSuggestionAvoidsClosedCells("P10");
        expected = Move(expected, SecondMatchSource, SecondMatchTarget);
        expected = Move(expected, ThirdMatchSource, ThirdMatchTarget);

        foreach (object step in WaitUntilSettled("P10 preparation")) yield return step;

        ExpectBoard(expected, "P10 preparation");
        Expect(_board.GetCycle(_board.Shelves[OpenCellIndex]).MovesToSwitch == 1, "P10: the open cell must close on the next matching move");
        ExpectSuggestionAvoidsClosedCells("P10 threat");
        Expect(_suggestions.TryGetSuggestion(out MoveSuggestion suggestion), "P10: the threatened board must offer a move");
        Expect(suggestion.SourceColumn.Shelf == _board.Shelves[OpenCellIndex], "P10: the suggestion must take an item out of the closing cell instead of the trapping match");
    }

    private static void ExpectSuggestionAvoidsClosedCells(string scenario)
    {
        Expect(_suggestions.TryGetSuggestion(out MoveSuggestion suggestion), $"{scenario}: the board must offer a move");
        Expect(!_board.IsShelfClosed(suggestion.SourceColumn.Shelf) && !_board.IsShelfClosed(suggestion.TargetColumn.Shelf), $"{scenario}: the suggestion must not use a closed cell");
    }

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns)[] ThreeMatchesLayout()
    {
        return new[]
        {
            Shelf(OpenCellIndex, Column(ItemType.Toy), Column(ItemType.MapBall), Column()),
            Shelf(ClosedCellIndex, Column(ItemType.Beauty), Column(ItemType.Beauty), Column()),
            Shelf(3, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(4, Column(ItemType.Bear), Column(ItemType.Bear), Column()),
            Shelf(5, Column(ItemType.Plant), Column(ItemType.Plant), Column()),
            Shelf(6, Column(ItemType.Ball), Column(), Column()),
            Shelf(7, Column(ItemType.Bear), Column(), Column()),
            Shelf(8, Column(ItemType.Plant), Column(), Column()),
            Shelf(9, Column(ItemType.Lamp, ItemType.Lamp, ItemType.Lamp), Column(), Column())
        };
    }

    public static BoardStateSnapshot ArrangeStuckSwing(bool isRepeatedDeadlock)
    {
        CaptureScene();

        if (isRepeatedDeadlock)
            return Arrange(StuckLayout(Column(ItemType.Lamp), Column(ItemType.Toy), Column()));

        return Arrange(StuckLayout(Column(ItemType.Ball), Column(ItemType.Ball), Column()));
    }

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns)[] StuckLayout(params ColumnStateSnapshot[] closedCellColumns)
    {
        return new[]
        {
            Shelf(OpenCellIndex, Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            Shelf(ClosedCellIndex, closedCellColumns),
            Shelf(3, Column(ItemType.Ball), Column(ItemType.Bear), Column()),
            Shelf(4, Column(ItemType.Plant), Column(), Column())
        };
    }

    private static IEnumerable LoadSwingLevel() => LoadLevel(GetScenarioLevel(SwingLevelName));

    private static IEnumerable LoadLevel(LevelEntry level)
    {
        SelectLevel(level);
        SceneManager.LoadScene(SceneName);
        yield return null;

        foreach (object step in WaitUntil(() => FindSession() != null && FindSession().CurrentLevel == level && FindSession().State == LevelState.Playing && FindSession().IsBoardSettled, "level load")) yield return step;

        CaptureScene();
        Time.timeScale = ScenarioTimeScale;
    }

    private static void CaptureScene()
    {
        _session = FindSession();
        _board = UnityEngine.Object.FindObjectOfType<ShelfBoard>();
        _cyclicShelves = UnityEngine.Object.FindObjectOfType<CyclicShelvesController>();
        _departureBoard = UnityEngine.Object.FindObjectOfType<DepartureBoardView>();
        _shutters = UnityEngine.Object.FindObjectsOfType<ShelfShutterView>(true);
        _drag = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();
        _suggestions = UnityEngine.Object.FindObjectOfType<MoveSuggestionProvider>();
        _rescueToast = UnityEngine.Object.FindObjectOfType<CycleRescueToastView>();

        if (!(_session is CyclicShelfLevelSession) || _board.Shelves.Count != BoardShelfCount || _cyclicShelves == null || _departureBoard == null || _drag == null || _suggestions == null || _rescueToast == null)
            throw new InvalidOperationException("Cyclic shelf scenarios need the SeventhLevel board, the cyclic shelf session and controller, the departure board, the drag controller, move suggestions and the rescue toast.");
    }

    private static BoardStateSnapshot Arrange(params (int ShelfIndex, ColumnStateSnapshot[] Columns)[] shelves)
    {
        ShelfItemCatalog catalog = _session.CurrentLevel.Definition.ItemCatalog;

        foreach (ShelfColumn column in _board.Shelves.SelectMany(shelf => shelf.Columns))
        {
            while (!column.IsEmpty)
                UnityEngine.Object.Destroy(column.TakeFront().gameObject);
        }

        foreach ((int shelfIndex, ColumnStateSnapshot[] columns) in shelves)
        {
            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                foreach (ItemType type in columns[columnIndex].Items)
                    _board.Shelves[shelfIndex].Columns[columnIndex].Append(UnityEngine.Object.Instantiate(catalog.GetPrefab(type)));
            }
        }

        foreach (Shelf shelf in _board.Shelves)
            shelf.View.Refresh();

        return _board.CreateSnapshot();
    }

    private static BoardStateSnapshot Move(BoardStateSnapshot expected, ColumnPosition source, ColumnPosition target)
    {
        BoardStateSnapshot state = Simulate(expected, source, target).State;
        ShelfColumnView sourceView = _board.Shelves[source.ShelfIndex].ColumnViews[source.ColumnIndex];
        ShelfColumnView targetView = _board.Shelves[target.ShelfIndex].ColumnViews[target.ColumnIndex];
        MoveOutcome outcome = _session.TryStartMove(sourceView, targetView, out Action completePlacement);
        Expect(outcome.IsSuccessful, "the scenario move must be accepted");
        completePlacement?.Invoke();
        return state;
    }

    private static BoardMoveSimulation Simulate(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target)
    {
        if (!Simulator.TrySimulate(board, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            throw new InvalidOperationException("The scenario move is not valid in the simulator.");

        return simulation;
    }

    private static void ExpectBoard(BoardStateSnapshot expected, string scenario)
    {
        Expect(BoardStateFingerprint.CreateHash(_board.CreateSnapshot()) == BoardStateFingerprint.CreateHash(expected), $"{scenario}: the runtime board must match the simulation");

        foreach (Shelf shelf in _board.Shelves)
        {
            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                Transform anchor = shelf.ColumnViews[columnIndex].ItemAnchor;
                Expect(shelf.Columns[columnIndex].Items.All(item => item.transform.parent == anchor), $"{scenario}: every item must rest on its column anchor");
            }
        }
    }

    private static IEnumerable WaitUntilSettled(string description)
    {
        return WaitUntil(() => _session.IsBoardSettled && !_cyclicShelves.IsSwitching, description);
    }

    private static IEnumerable WaitUntilViewsMatch(string description)
    {
        return WaitUntil(() => DescribeViewMismatch() == null, $"{description}: the shutters and the departure board must show the board ({DescribeViewMismatch()})");
    }

    private static string DescribeViewMismatch()
    {
        foreach (ShelfShutterView shutter in _shutters)
        {
            if (!_board.IsCyclic(shutter.Shelf))
            {
                if (shutter.ClosedFraction > 0f)
                    return $"{shutter.Shelf.name} has a shutter without a cycle";

                continue;
            }

            if (shutter.IsSwitching || shutter.IsClosed != _board.IsShelfClosed(shutter.Shelf) || IsCurtainLowered(shutter) != shutter.IsClosed)
                return $"{shutter.Shelf.name} shutter";

            ShelfCycleSnapshot cycle = _board.GetCycle(shutter.Shelf);
            string text = cycle == null ? "--" : cycle.MovesToSwitch.ToString("00");

            if (_departureBoard.GetText(shutter.Shelf) != text)
                return $"{shutter.Shelf.name} departure board shows {_departureBoard.GetText(shutter.Shelf)} instead of {text}";
        }

        return null;
    }

    private static IEnumerable WaitUntil(Func<bool> condition, string description)
    {
        for (int frame = 0; !condition(); frame++)
        {
            if (frame >= MaximumWaitFrames)
                throw new InvalidOperationException($"Timed out waiting for {description}.");

            yield return null;
        }
    }

    private static bool IsCurtainLowered(ShelfShutterView shutter) => shutter.ClosedFraction >= MinimumClosedFraction;

    private static ShelfShutterView GetShutter(int shelfIndex) => _shutters.First(shutter => shutter.Shelf == _board.Shelves[shelfIndex]);

    private static Vector2 GetScreenCenter(ShelfItem item)
    {
        if (!item.GetComponent<ShelfItemTargetView>().TryGetBounds(out Bounds bounds))
            throw new InvalidOperationException($"{item.name}: no bounds to press.");

        return Camera.main.WorldToScreenPoint(bounds.center);
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Cyclic Shelf Play Mode: {message}.");
    }

    private static LevelSession FindSession() => UnityEngine.Object.FindObjectOfType<LevelSession>();

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns) Shelf(int shelfIndex, params ColumnStateSnapshot[] columns) => (shelfIndex, columns);

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    public static LevelEntry GetScenarioLevel(string levelName)
    {
        LevelEntry existingLevel = Resources.FindObjectsOfTypeAll<LevelEntry>().FirstOrDefault(level => level.name == levelName && level.Definition != null);

        if (existingLevel != null)
            return existingLevel;

        bool isSwing = levelName == SwingLevelName;
        TimedLevelDefinition definition = CreateScenarioDefinition(levelName, isSwing ? SwingShelves : RotorShelves, isSwing ? SwingGroups : RotorGroups);
        TimedLevelVariantGenerator.WriteVariants(definition, new[] { GenerateScenarioVariant(definition, isSwing ? SwingSeedBase : RotorSeedBase) });
        LevelEntry level = ScriptableObject.CreateInstance<LevelEntry>();
        level.name = levelName;
        level.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedLevel = new SerializedObject(level);
        serializedLevel.FindProperty("_number").intValue = ScenarioLevelNumber;
        serializedLevel.FindProperty("_sceneName").stringValue = SceneName;
        serializedLevel.FindProperty("_definition").objectReferenceValue = definition;
        serializedLevel.FindProperty("_available").boolValue = true;
        serializedLevel.ApplyModifiedPropertiesWithoutUndo();
        return level;
    }

    private static TimedLevelVariant GenerateScenarioVariant(TimedLevelDefinition definition, int seedBase)
    {
        TimedLevelLayoutGenerator generator = new TimedLevelLayoutGenerator();
        BoardStateSnapshot boardShape = new BoardStateSnapshot(Enumerable.Range(0, BoardShelfCount)
            .Select(_ => new ShelfStateSnapshot(Enumerable.Range(0, ShelfCapacity).Select(__ => new ColumnStateSnapshot(Array.Empty<ItemType>())).ToArray()))
            .ToArray());

        for (int seed = seedBase; seed < seedBase + MaximumScenarioSeeds; seed++)
        {
            TimedLevelGenerationResult generation;

            try
            {
                generation = generator.GenerateWithSolution(definition, boardShape, seed);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            CyclicShelfLayoutMetrics metrics = CyclicShelfLayoutMetrics.Measure(definition.AddCyclicShelves(generation.State), generation.SolutionMoves);

            if (!metrics.IsReplayed || metrics.HasDeadlockOnPath || metrics.SwitchCount == 0)
                continue;

            return new TimedLevelVariant(
                seed,
                TimedLevelLayoutRules.GeneratorVersion,
                generation.SolutionMoves.Count,
                BoardStateFingerprint.CreateHash(generation.State),
                generation.State,
                generation.SolutionMoves);
        }

        throw new InvalidOperationException($"{definition.name}: no cyclic scenario level could be generated.");
    }

    private static TimedLevelDefinition CreateScenarioDefinition(
        string levelName,
        IReadOnlyList<CyclicShelfDefinitionData> cyclicShelves,
        IReadOnlyList<(ItemType Type, int Count)> groups)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        TimedLevelDefinition definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
        definition.name = $"{levelName}_Definition";
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = 600;
        serializedDefinition.FindProperty("_twoStarTimeSeconds").intValue = 400;
        serializedDefinition.FindProperty("_threeStarTimeSeconds").intValue = 300;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = EmptyColumnCount;
        serializedDefinition.FindProperty("_itemCatalog").objectReferenceValue = catalog.Levels[0].Definition.ItemCatalog;
        serializedDefinition.FindProperty("_minimumItemsBehindShutter").intValue = 0;
        serializedDefinition.FindProperty("_minimumPreparationMoves").intValue = 0;
        CyclicShelfDefinitionData.WriteAll(serializedDefinition.FindProperty("_cyclicShelves"), cyclicShelves);
        SerializedProperty groupsProperty = serializedDefinition.FindProperty("_itemGroups");
        groupsProperty.arraySize = groups.Count;

        for (int index = 0; index < groups.Count; index++)
        {
            groupsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_type").enumValueIndex = (int)groups[index].Type;
            groupsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_groupCount").intValue = groups[index].Count;
        }

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void SelectLevel(LevelEntry level)
    {
        LevelSelectionState selection = AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath);

        if (selection == null)
            throw new InvalidOperationException("Cyclic shelf scenarios need the level selection.");

        selection.Select(level);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        foreach (LevelResultRecorder recorder in UnityEngine.Object.FindObjectsOfType<LevelResultRecorder>(true))
            recorder.enabled = false;

        foreach (YG.Insides.YGSendMessage initializer in UnityEngine.Object.FindObjectsOfType<YG.Insides.YGSendMessage>(true))
            initializer.enabled = false;
    }

    private static void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (SessionState.GetBool(ActiveKey, false) && EditorApplication.isPlaying && (type == LogType.Exception || type == LogType.Error) && _unexpectedError == null)
            _unexpectedError = condition;
    }

    private static void Stop()
    {
        _scenarios = null;
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
    }

    private static void Finish()
    {
        bool failed = SessionState.GetBool(FailedKey, false) || !SessionState.GetBool(CompletedKey, false);
        SessionState.SetBool(ActiveKey, false);
        SessionState.EraseBool(CompletedKey);
        SessionState.EraseBool(FailedKey);
        Application.runInBackground = SessionState.GetBool(RunInBackgroundKey, false);
        SessionState.EraseBool(RunInBackgroundKey);
        _unexpectedError = null;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (failed)
            Debug.LogError("CYCLIC_SHELF_PLAYMODE_FAIL");
        else
            Debug.Log("CYCLIC_SHELF_PLAYMODE_PASS");
    }
}
