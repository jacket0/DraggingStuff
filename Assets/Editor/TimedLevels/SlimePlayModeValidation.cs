using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SlimePlayModeValidation
{
    private const string ActiveKey = "SlimePlayModeValidation.Active";
    private const string CompletedKey = "SlimePlayModeValidation.Completed";
    private const string FailedKey = "SlimePlayModeValidation.Failed";
    private const string RunInBackgroundKey = "SlimePlayModeValidation.RunInBackground";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/EighthLevel.unity";
    private const string SceneName = "EighthLevel";
    private const string ScenarioLevelName = "SlimeScenarioLevel";
    private const int ScenarioLevelNumber = 31;
    private const int ScenarioSeedBase = 3199000;
    private const int MaximumScenarioSeeds = 300;
    private const int ScenarioTimeLimitSeconds = 600;
    private const int MaximumWaitFrames = 3000;
    private const int PausedFrameCount = 10;
    private const float ScenarioTimeScale = 3f;
    private const float SelectionPaddingPixels = 48f;
    private const float ForecastTolerance = 0.05f;

    private static readonly BoardMoveSimulator Simulator = new BoardMoveSimulator();
    private static readonly ColumnPosition[] ScenarioStickyColumns = { new ColumnPosition(1, 0), new ColumnPosition(6, 2), new ColumnPosition(8, 1) };
    private static readonly ColumnPosition[] GrowingStickyColumns = { new ColumnPosition(6, 0), new ColumnPosition(6, 1), new ColumnPosition(6, 2) };
    private static readonly ColumnPosition GrowthTarget = new ColumnPosition(7, 0);
    private static readonly ColumnPosition FirstMatchSource = new ColumnPosition(8, 0);
    private static readonly ColumnPosition FirstMatchTarget = new ColumnPosition(2, 2);
    private static readonly ColumnPosition SecondMatchSource = new ColumnPosition(9, 0);
    private static readonly ColumnPosition SecondMatchTarget = new ColumnPosition(4, 2);

    private static IEnumerator _scenarios;
    private static int _lastFrame = -1;
    private static string _unexpectedError;
    private static LevelSession _session;
    private static ShelfBoard _board;
    private static SlimeController _slime;
    private static SlimeBoardView _view;
    private static ShelfItemDragController _drag;
    private static MoveSuggestionProvider _suggestions;

    static SlimePlayModeValidation()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Application.logMessageReceived -= HandleLogMessage;
        Application.logMessageReceived += HandleLogMessage;
    }

    [MenuItem("Tools/Timed Levels/Validate Slime Play Mode")]
    public static void Begin()
    {
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the open scene before the slime scenarios.");

        if (EditorBuildSettings.scenes.All(scene => scene.path != ScenePath))
            throw new InvalidOperationException($"{ScenePath} must be in the build settings: the scenarios load it by name.");

        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(CompletedKey, false);
        SessionState.SetBool(FailedKey, false);
        SessionState.SetBool(RunInBackgroundKey, Application.runInBackground);
        Application.runInBackground = true;
        GetScenarioLevel();
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
            ValidateSolutionReplay,
            ValidatePickUpRejection,
            ValidatePlacementIntoSlime,
            ValidateNeighborCleaning,
            ValidateGrowthThroughSeam,
            ValidateGrowthCancelsDrag,
            ValidateTwoOverlappingMatchingMoves,
            ValidateDeadlockRescue,
            ValidatePauseDuringGrowth,
            ValidateRestartWithPendingGrowth,
            ValidateSuggestions,
            ValidateWinWithRemainingSlime
        };

        foreach (Func<IEnumerable> scenario in scenarios)
        {
            int startFrame = Time.frameCount;

            foreach (object step in scenario())
                yield return step;

            Debug.Log($"SLIME_SCENARIO_PASS: {scenario.Method.Name} in {Time.frameCount - startFrame} frames");
        }
    }

    private static IEnumerable ValidateSolutionReplay()
    {
        LevelEntry level = GetScenarioLevel();

        foreach (object step in LoadScenarioLevel()) yield return step;

        TimedLevelVariant variant = level.Definition.Variants[0];
        BoardStateSnapshot expected = level.Definition.AddSlime(variant.CreateLayout());
        ExpectBoard(expected, "P1 start");

        foreach (object step in WaitUntilViewsMatch("P1 start")) yield return step;

        int cleanedCount = 0;
        int growthCount = 0;

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            BoardMoveSimulation simulation = Simulate(expected, move.Source, move.Target);
            cleanedCount += simulation.CleanedColumns.Count;
            growthCount += simulation.GrownColumn.HasValue ? 1 : 0;
            expected = Move(expected, move.Source, move.Target);

            foreach (object step in WaitUntilSettled("P1 move")) yield return step;

            ExpectBoard(expected, "P1 move");

            if (_session.State == LevelState.Playing)
            {
                foreach (object step in WaitUntilViewsMatch("P1 move")) yield return step;
            }
        }

        Expect(cleanedCount > 0 && growthCount > 0, "P1: the solution must clean and grow slime");
        Expect(_board.IsCleared && _session.State == LevelState.Won, "P1: the solution must win the level");
    }

    private static IEnumerable ValidatePickUpRejection()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(
            new[] { new ColumnPosition(3, 1) },
            Shelf(3, Column(ItemType.Lamp), Column(ItemType.Toy), Column()),
            Shelf(7, Column(ItemType.Ball), Column(), Column()));
        ShelfItem stickyItem = GetFrontItem(new ColumnPosition(3, 1));
        Vector2 pressPosition = GetScreenCenter(stickyItem);
        ShelfItemTargetResolver resolver = new ShelfItemTargetResolver(Camera.main, _board, SelectionPaddingPixels);
        List<ShelfItem> rejections = new List<ShelfItem>();

        void HandlePickUpRejected(ShelfItem item) => rejections.Add(item);

        _slime.PickUpRejected += HandlePickUpRejected;

        try
        {
            Expect(!_drag.TryBeginDrag(stickyItem, pressPosition), "P2: a sticky item must not start a drag");
            Expect(!resolver.TryResolve(pressPosition, out ShelfItem pickable) || pickable != stickyItem, "P2: the pointer must not pick a sticky item");
            Expect(resolver.TryResolveSticky(pressPosition, out ShelfItem sticky) && sticky == stickyItem, "P2: the pointer must find the sticky item to refuse it");
            _drag.RejectPickUp(stickyItem);

            foreach (object step in WaitUntilSettled("P2")) yield return step;
        }
        finally
        {
            _slime.PickUpRejected -= HandlePickUpRejected;
        }

        Expect(rejections.SequenceEqual(new[] { stickyItem }), "P2: the slime must answer the refused pick-up once");
        ExpectBoard(expected, "P2");
    }

    private static IEnumerable ValidatePlacementIntoSlime()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        ColumnPosition stickyEmpty = new ColumnPosition(3, 2);
        BoardStateSnapshot expected = Arrange(
            new[] { new ColumnPosition(3, 1), stickyEmpty },
            Shelf(3, Column(ItemType.Lamp), Column(ItemType.Toy), Column()),
            Shelf(7, Column(ItemType.Ball), Column(), Column()),
            Shelf(8, Column(ItemType.Bear), Column(), Column()));
        expected = Move(expected, new ColumnPosition(7, 0), stickyEmpty);

        foreach (object step in WaitUntilSettled("P3 placement")) yield return step;

        ExpectBoard(expected, "P3 placement");
        Expect(!_board.CanPickUp(_board.GetColumn(stickyEmpty)), "P3: an item placed into slime must stick");
        Expect(!TryStartMove(new ColumnPosition(8, 0), stickyEmpty), "P3: a swap into a sticky column must be refused");
        Expect(!TryStartMove(stickyEmpty, new ColumnPosition(8, 0)), "P3: a swap out of a sticky column must be refused");
        Expect(!TryStartMove(new ColumnPosition(3, 1), new ColumnPosition(9, 0)), "P3: a sticky item must not move into an empty column");

        foreach (object step in WaitUntilSettled("P3")) yield return step;

        ExpectBoard(expected, "P3");
    }

    private static IEnumerable ValidateNeighborCleaning()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        ColumnPosition farColumn = new ColumnPosition(6, 1);
        BoardStateSnapshot expected = Arrange(
            new[] { new ColumnPosition(2, 1), new ColumnPosition(3, 0), farColumn },
            Shelf(2, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(3, Column(ItemType.Lamp), Column(), Column()),
            Shelf(6, Column(ItemType.Toy), Column(ItemType.Bear), Column()),
            Shelf(7, Column(ItemType.Ball), Column(), Column()));
        List<ColumnPosition> cleaned = new List<ColumnPosition>();

        void HandleSlimeCleaned(IReadOnlyList<ColumnPosition> columns) => cleaned.AddRange(columns);

        _slime.SlimeCleaned += HandleSlimeCleaned;

        try
        {
            expected = Move(expected, new ColumnPosition(7, 0), FirstMatchTarget);

            foreach (object step in WaitUntilSettled("P4")) yield return step;
        }
        finally
        {
            _slime.SlimeCleaned -= HandleSlimeCleaned;
        }

        ExpectBoard(expected, "P4");
        Expect(cleaned.OrderBy(column => column.ShelfIndex).SequenceEqual(new[] { new ColumnPosition(2, 1), new ColumnPosition(3, 0) }), "P4: a match must clean its shelf and the linked neighbor");
        Expect(_board.Slime.StickyColumns.SequenceEqual(new[] { farColumn }), "P4: slime on other shelves must stay");
        Expect(_board.Slime.MovesToGrowth == _board.Slime.GrowthPeriod, "P4: a cleaning move must reset the growth counter");

        foreach (object step in WaitUntilViewsMatch("P4")) yield return step;
    }

    private static IEnumerable ValidateGrowthThroughSeam()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        BoardStateSnapshot expected = ArrangeGrowth();
        List<ColumnPosition> grown = new List<ColumnPosition>();

        void HandleSlimeGrown(ColumnPosition column) => grown.Add(column);

        _slime.SlimeGrown += HandleSlimeGrown;

        try
        {
            expected = Move(expected, FirstMatchSource, FirstMatchTarget);

            foreach (object step in WaitUntilSettled("P5 first move")) yield return step;

            ExpectBoard(expected, "P5 first move");
            Expect(grown.Count == 0 && _board.Slime.MovesToGrowth == 1, "P5: the first matching move must only count down");

            foreach (object step in WaitUntilViewsMatch("P5 first move")) yield return step;

            Expect(IsForecastOn(GrowthTarget, 1), "P5: the forecast must show the next growth across the seam");
            expected = Move(expected, SecondMatchSource, SecondMatchTarget);

            foreach (object step in WaitUntilSettled("P5 second move")) yield return step;
        }
        finally
        {
            _slime.SlimeGrown -= HandleSlimeGrown;
        }

        ExpectBoard(expected, "P5");
        Expect(grown.SequenceEqual(new[] { GrowthTarget }), "P5: the second matching move must grow slime across the seam");
        Expect(_board.IsSticky(_board.GetColumn(GrowthTarget)) && GetFrontItem(GrowthTarget) != null, "P5: slime must grow onto the column with an item");

        foreach (object step in WaitUntilViewsMatch("P5")) yield return step;
    }

    private static IEnumerable ValidateGrowthCancelsDrag()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        BoardStateSnapshot expected = ArrangeGrowth();
        expected = Move(expected, FirstMatchSource, FirstMatchTarget);

        foreach (object step in WaitUntilSettled("P6 preparation")) yield return step;

        Shelf targetShelf = _board.Shelves[GrowthTarget.ShelfIndex];
        expected = Move(expected, SecondMatchSource, SecondMatchTarget);
        ShelfItem draggedItem = targetShelf.Columns[1].FrontItem;
        Vector2 pressPosition = GetScreenCenter(draggedItem);
        Expect(_drag.TryBeginDrag(draggedItem, pressPosition), "P6: a drag from the growth target shelf must start");
        _drag.UpdateDrag(pressPosition + new Vector2(120f, -60f));

        while (!_board.IsSticky(_board.GetColumn(GrowthTarget)) || _slime.IsGrowing)
        {
            Expect(!_drag.IsDragging || !_board.IsShelfLocked(targetShelf), "P6: the growth reservation must cancel the drag");
            yield return null;
        }

        foreach (object step in WaitUntilSettled("P6")) yield return step;

        ExpectBoard(expected, "P6");
        Expect(!_drag.IsDragging && draggedItem.transform.parent == targetShelf.ColumnViews[1].ItemAnchor, "P6: the dragged item must return to its column");

        foreach (object step in WaitUntilViewsMatch("P6")) yield return step;
    }

    private static IEnumerable ValidateTwoOverlappingMatchingMoves()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        BoardStateSnapshot expected = ArrangeGrowth();
        expected = Move(expected, FirstMatchSource, FirstMatchTarget);
        Expect(!_session.IsBoardSettled, "P7: the first move must still be resolving");
        expected = Move(expected, SecondMatchSource, SecondMatchTarget);

        foreach (object step in WaitUntilSettled("P7")) yield return step;

        ExpectBoard(expected, "P7");
        Expect(_board.IsSticky(_board.GetColumn(GrowthTarget)), "P7: two overlapping matching moves must count twice");

        foreach (object step in WaitUntilViewsMatch("P7")) yield return step;
    }

    private static IEnumerable ValidateDeadlockRescue()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        ColumnPosition lastFreeItem = new ColumnPosition(4, 0);
        ColumnPosition stickyEmpty = new ColumnPosition(4, 1);
        BoardStateSnapshot expected = Arrange(
            new[] { new ColumnPosition(2, 0), new ColumnPosition(2, 1), stickyEmpty },
            Shelf(2, Column(ItemType.Lamp), Column(ItemType.Toy), Column()),
            Shelf(4, Column(ItemType.Ball), Column(), Column()));
        List<ColumnPosition> rescues = new List<ColumnPosition>();

        void HandleRescueApplied(ColumnPosition column) => rescues.Add(column);

        _slime.RescueApplied += HandleRescueApplied;

        try
        {
            expected = Move(expected, lastFreeItem, stickyEmpty);
            Expect(SlimeRule.IsStuck(expected, Simulator), "P8: the arranged move must leave no legal move");
            expected = SlimeRule.ApplyRescue(expected, out ColumnPosition rescuedColumn);

            foreach (object step in WaitUntilSettled("P8")) yield return step;

            Expect(rescues.SequenceEqual(new[] { rescuedColumn }), "P8: a deadlock must remove exactly one slime spot by priority");
        }
        finally
        {
            _slime.RescueApplied -= HandleRescueApplied;
        }

        ExpectBoard(expected, "P8");
        Expect(!SlimeRule.IsStuck(_board.CreateSnapshot(), Simulator), "P8: the rescue must leave a legal move");

        foreach (object step in WaitUntilViewsMatch("P8")) yield return step;
    }

    private static IEnumerable ValidatePauseDuringGrowth()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        BoardStateSnapshot expected = ArrangeGrowth();
        expected = Move(expected, FirstMatchSource, FirstMatchTarget);
        expected = Move(expected, SecondMatchSource, SecondMatchTarget);

        foreach (object step in WaitUntil(() => _slime.IsGrowing && _view.IsAnimating, "the growth")) yield return step;

        Expect(_session.TryPause(), "P9: the level must pause during the growth");
        string pausedHash = BoardStateFingerprint.CreateHash(_board.CreateSnapshot());

        for (int frame = 0; frame < PausedFrameCount; frame++)
        {
            yield return null;
            Expect(_slime.IsGrowing && _view.IsAnimating, "P9: the growth must stay unfinished while paused");
            Expect(BoardStateFingerprint.CreateHash(_board.CreateSnapshot()) == pausedHash, "P9: the board must not change while paused");
        }

        _session.Resume();
        Time.timeScale = ScenarioTimeScale;

        foreach (object step in WaitUntilSettled("P9 resume")) yield return step;

        ExpectBoard(expected, "P9");

        foreach (object step in WaitUntilViewsMatch("P9")) yield return step;
    }

    private static IEnumerable ValidateRestartWithPendingGrowth()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        BoardStateSnapshot start = _board.CreateSnapshot();
        BoardStateSnapshot expected = ArrangeGrowth();
        expected = Move(expected, FirstMatchSource, FirstMatchTarget);
        Move(expected, SecondMatchSource, SecondMatchTarget);

        foreach (object step in WaitUntil(() => _slime.IsGrowing, "pending growth")) yield return step;

        LevelSession previousSession = _session;
        previousSession.Restart();

        foreach (object step in WaitUntil(() => FindSession() != null && FindSession() != previousSession && FindSession().State == LevelState.Playing, "restart")) yield return step;

        CaptureScene();

        for (int frame = 0; frame < 30; frame++)
            yield return null;

        Expect(!_session.HasPendingBoardChanges && !_slime.IsGrowing, "P9: a restart must drop the pending growth");
        ExpectBoard(start, "P9 restart");

        foreach (object step in WaitUntilViewsMatch("P9 restart")) yield return step;
    }

    private static IEnumerable ValidateSuggestions()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        ColumnPosition stickyBall = new ColumnPosition(6, 0);
        Arrange(
            new[] { stickyBall },
            Shelf(2, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(6, Column(ItemType.Ball), Column(), Column()),
            Shelf(8, Column(ItemType.Toy), Column(), Column()));

        foreach (object step in WaitUntilSettled("P10 arrange")) yield return step;

        Expect(_suggestions.TryGetSuggestion(out MoveSuggestion suggestion), "P10: the board must offer a move");
        Expect(!_board.IsSticky(suggestion.SourceColumn.Column), "P10: the suggestion must not take a sticky item");
    }

    private static IEnumerable ValidateWinWithRemainingSlime()
    {
        foreach (object step in LoadScenarioLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(
            new[] { new ColumnPosition(8, 1) },
            Shelf(2, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(7, Column(ItemType.Ball), Column(), Column()));
        expected = Move(expected, new ColumnPosition(7, 0), FirstMatchTarget);

        foreach (object step in WaitUntilSettled("P10 win")) yield return step;

        ExpectBoard(expected, "P10 win");
        Expect(_session.State == LevelState.Won && _board.Slime.HasStickyColumns, "P10: the level must be won with slime left");

        foreach (object step in WaitUntil(() => !_view.IsAnimating, "P10 drying")) yield return step;

        Expect(_view.GetVisibleSpots().Count == 0 && !_view.Forecast.gameObject.activeSelf, "P10: the remaining slime must dry up on the win");
    }

    private static BoardStateSnapshot ArrangeGrowth()
    {
        return Arrange(
            GrowingStickyColumns,
            Shelf(2, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(4, Column(ItemType.Bear), Column(ItemType.Bear), Column()),
            Shelf(6, Column(ItemType.Lamp), Column(ItemType.Toy), Column()),
            Shelf(7, Column(ItemType.Plant), Column(ItemType.Plant), Column()),
            Shelf(8, Column(ItemType.Ball), Column(), Column()),
            Shelf(9, Column(ItemType.Bear), Column(), Column()));
    }

    private static IEnumerable LoadScenarioLevel()
    {
        LevelEntry level = GetScenarioLevel();
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
        _slime = UnityEngine.Object.FindObjectOfType<SlimeController>();
        _view = UnityEngine.Object.FindObjectOfType<SlimeBoardView>();
        _drag = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();
        _suggestions = UnityEngine.Object.FindObjectOfType<MoveSuggestionProvider>();

        if (_board.Shelves.Count != SlimeGenerationPreview.BoardShelfCount || !_board.HasSlime || _slime == null || _view == null || _drag == null || _suggestions == null)
            throw new InvalidOperationException("Slime scenarios need the EighthLevel board with slime, the slime controller and view, the drag controller and move suggestions.");
    }

    private static BoardStateSnapshot Arrange(IReadOnlyList<ColumnPosition> stickyColumns, params (int ShelfIndex, ColumnStateSnapshot[] Columns)[] shelves)
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

        _board.SetSlime(_board.Slime.WithStickyColumns(stickyColumns, _board.Slime.GrowthPeriod));
        _view.ShowLevel(_board.Slime.StickyColumns);
        return _board.CreateSnapshot();
    }

    private static BoardStateSnapshot Move(BoardStateSnapshot expected, ColumnPosition source, ColumnPosition target)
    {
        BoardStateSnapshot state = Simulate(expected, source, target).State;
        Expect(TryStartMove(source, target), "the scenario move must be accepted");
        return state;
    }

    private static bool TryStartMove(ColumnPosition source, ColumnPosition target)
    {
        ShelfColumnView sourceView = _board.Shelves[source.ShelfIndex].ColumnViews[source.ColumnIndex];
        ShelfColumnView targetView = _board.Shelves[target.ShelfIndex].ColumnViews[target.ColumnIndex];
        MoveOutcome outcome = _session.TryStartMove(sourceView, targetView, out Action completePlacement);
        completePlacement?.Invoke();
        return outcome.IsSuccessful;
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
        return WaitUntil(() => _session.IsBoardSettled && !_slime.IsGrowing, description);
    }

    private static IEnumerable WaitUntilViewsMatch(string description)
    {
        return WaitUntil(() => DescribeViewMismatch() == null, $"{description}: the slime view must show the board ({DescribeViewMismatch()})");
    }

    private static string DescribeViewMismatch()
    {
        if (_view.IsAnimating)
            return "spots are animating";

        ColumnPosition[] visibleSpots = _view.GetVisibleSpots().OrderBy(column => column.ShelfIndex).ThenBy(column => column.ColumnIndex).ToArray();

        if (!visibleSpots.SequenceEqual(_board.Slime.StickyColumns))
            return $"spots {Describe(visibleSpots)} instead of {Describe(_board.Slime.StickyColumns)}";

        ColumnPosition? target = SlimeRule.FindGrowthTarget(_board.CreateSnapshot());

        if (!target.HasValue)
            return _view.Forecast.gameObject.activeSelf ? "the forecast is shown without a growth target" : null;

        return IsForecastOn(target.Value, _board.Slime.MovesToGrowth) ? null : $"the forecast is not on {Describe(new[] { target.Value })} with {_board.Slime.MovesToGrowth}";
    }

    private static bool IsForecastOn(ColumnPosition column, int movesToGrowth)
    {
        Vector3 anchor = _board.GetView(_board.GetColumn(column)).ItemAnchor.position;
        Vector3 forecast = _view.Forecast.transform.position;
        return _view.Forecast.gameObject.activeSelf
            && _view.Forecast.MovesToGrowth == movesToGrowth
            && Vector2.Distance(new Vector2(anchor.x, anchor.z), new Vector2(forecast.x, forecast.z)) < ForecastTolerance;
    }

    private static string Describe(IEnumerable<ColumnPosition> columns)
    {
        return "[" + string.Join(" ", columns.Select(column => $"{column.ShelfIndex}.{column.ColumnIndex}")) + "]";
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

    private static ShelfItem GetFrontItem(ColumnPosition column) => _board.GetColumn(column).FrontItem;

    private static Vector2 GetScreenCenter(ShelfItem item)
    {
        if (!item.GetComponent<ShelfItemTargetView>().TryGetBounds(out Bounds bounds))
            throw new InvalidOperationException($"{item.name}: no bounds to press.");

        return Camera.main.WorldToScreenPoint(bounds.center);
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Slime Play Mode: {message}.");
    }

    private static LevelSession FindSession() => UnityEngine.Object.FindObjectOfType<LevelSession>();

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns) Shelf(int shelfIndex, params ColumnStateSnapshot[] columns) => (shelfIndex, columns);

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    private static LevelEntry GetScenarioLevel()
    {
        LevelEntry existingLevel = Resources.FindObjectsOfTypeAll<LevelEntry>().FirstOrDefault(level => level.name == ScenarioLevelName && level.Definition != null);

        if (existingLevel != null)
            return existingLevel;

        TimedLevelDefinition definition = SlimeGenerationPreview.CreatePreviewDefinition("PlayMode", ScenarioStickyColumns, new List<UnityEngine.Object>());
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = ScenarioTimeLimitSeconds;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        TimedLevelVariantGenerator.WriteVariants(definition, new[] { GenerateScenarioVariant(definition) });

        LevelEntry level = ScriptableObject.CreateInstance<LevelEntry>();
        level.name = ScenarioLevelName;
        level.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedLevel = new SerializedObject(level);
        serializedLevel.FindProperty("_number").intValue = ScenarioLevelNumber;
        serializedLevel.FindProperty("_sceneName").stringValue = SceneName;
        serializedLevel.FindProperty("_definition").objectReferenceValue = definition;
        serializedLevel.FindProperty("_available").boolValue = true;
        serializedLevel.ApplyModifiedPropertiesWithoutUndo();
        return level;
    }

    private static TimedLevelVariant GenerateScenarioVariant(TimedLevelDefinition definition)
    {
        TimedLevelLayoutGenerator generator = new TimedLevelLayoutGenerator();
        BoardStateSnapshot boardShape = SlimeGenerationPreview.CreateBoardShape();

        for (int seed = ScenarioSeedBase; seed < ScenarioSeedBase + MaximumScenarioSeeds; seed++)
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

            SlimeLayoutMetrics metrics = SlimeLayoutMetrics.Measure(definition.AddSlime(generation.State), generation.SolutionMoves);

            if (!metrics.IsReplayed || metrics.HasDeadlockOnPath || metrics.GrowthCount == 0 || metrics.CleanedColumnCount == 0)
                continue;

            return new TimedLevelVariant(
                seed,
                TimedLevelLayoutRules.GeneratorVersion,
                generation.SolutionMoves.Count,
                BoardStateFingerprint.CreateHash(generation.State),
                generation.State,
                generation.SolutionMoves);
        }

        throw new InvalidOperationException($"{definition.name}: no slime scenario level could be generated.");
    }

    private static void SelectLevel(LevelEntry level)
    {
        LevelSelectionState selection = AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath);

        if (selection == null)
            throw new InvalidOperationException("Slime scenarios need the level selection.");

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
            Debug.LogError("SLIME_PLAYMODE_FAIL");
        else
            Debug.Log("SLIME_PLAYMODE_PASS");
    }
}
