using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ConveyorPlayModeValidation
{
    private const string ActiveKey = "ConveyorPlayModeValidation.Active";
    private const string CompletedKey = "ConveyorPlayModeValidation.Completed";
    private const string FailedKey = "ConveyorPlayModeValidation.Failed";
    private const string RunInBackgroundKey = "ConveyorPlayModeValidation.RunInBackground";
    private const string LevelEntryPath = "Assets/Levels/Menu/LevelEntry_C4_05.asset";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/FifthLevel.unity";
    private const string SceneName = "FifthLevel";
    private const int MaximumWaitFrames = 3000;
    private const int PausedFrameCount = 10;
    private const float ScenarioTimeScale = 3f;
    private const float ExpiringCountdownRate = 100000f;
    private const int LevelWithoutFiltersSeed = 2099001;
    private const int BoxShelfIndex = 1;
    private const int BeltShelfIndex = 8;

    private static readonly BoardMoveSimulator Simulator = new BoardMoveSimulator();
    private static IEnumerator _scenarios;
    private static int _lastFrame = -1;
    private static string _unexpectedError;
    private static LevelSession _session;
    private static ShelfBoard _board;
    private static ConveyorController _conveyor;
    private static ShelfItemDragController _drag;
    private static MoveSuggestionProvider _suggestions;
    private static ShelfFilterSignView[] _signs;
    private static LevelEntry _levelWithoutFilters;

    static ConveyorPlayModeValidation()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Application.logMessageReceived -= HandleLogMessage;
        Application.logMessageReceived += HandleLogMessage;
    }

    [MenuItem("Tools/Timed Levels/Validate Conveyor Play Mode")]
    public static void Begin()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(CompletedKey, false);
        SessionState.SetBool(FailedKey, false);
        SessionState.SetBool(RunInBackgroundKey, Application.runInBackground);
        Application.runInBackground = true;
        SelectLevel(LoadFilterLevel());
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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
            ValidateDoubleMatchShiftsOnce,
            ValidateCascadeBeforeShift,
            ValidateMatchFromShiftWithoutSecondShift,
            ValidateShortColumns,
            ValidateParallelMoves,
            ValidatePauseDuringShift,
            ValidateDragFromReservedShelf,
            ValidateTimeoutWithPendingShift,
            ValidateTimeoutVictory,
            ValidateRestartWithPendingShift,
            ValidateFilterSolutionReplay,
            ValidateRejectedDropFromConveyor,
            ValidateStuckTripleFromShift,
            ValidateSuggestionSkipsTripleOnConveyor
        };

        foreach (Func<IEnumerable> scenario in scenarios)
        {
            int startFrame = Time.frameCount;

            foreach (object step in scenario())
                yield return step;

            Debug.Log($"CONVEYOR_SCENARIO_PASS: {scenario.Method.Name} in {Time.frameCount - startFrame} frames");
        }
    }

    private static IEnumerable ValidateDoubleMatchShiftsOnce()
    {
        foreach (object step in LoadLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Lamp)),
            Shelf(1, Column(ItemType.Lamp), Column(ItemType.Lamp), Column(ItemType.Ball)),
            Shelf(8, Column(ItemType.Plant, ItemType.Toy, ItemType.Bear), Column(ItemType.Bear, ItemType.MapBall), Column(ItemType.Beauty)));
        EventCounter counter = new EventCounter(_session, _conveyor);
        expected = Move(expected, new ColumnPosition(0, 2), new ColumnPosition(1, 2));

        foreach (object step in WaitUntilSettled("double match")) yield return step;

        counter.Dispose();
        ExpectBoard(expected, "double match");
        Expect(counter.MatchCount == 2 && counter.ShiftCount == 1, "a swap with two matches must shift the conveyors once");
    }

    private static IEnumerable ValidateCascadeBeforeShift()
    {
        foreach (object step in LoadLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(
            Shelf(8, Column(ItemType.Ball, ItemType.Toy, ItemType.Plant), Column(ItemType.Ball, ItemType.Toy), Column(ItemType.Lamp, ItemType.Toy)),
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Bear), Column()));
        EventCounter counter = new EventCounter(_session, _conveyor);
        expected = Move(expected, new ColumnPosition(8, 2), new ColumnPosition(0, 0));

        foreach (object step in WaitUntilSettled("cascade")) yield return step;

        counter.Dispose();
        ExpectBoard(expected, "cascade");
        Expect(counter.MatchCount == 2, "the cascade must resolve on the conveyor before the shift");
    }

    private static IEnumerable ValidateMatchFromShiftWithoutSecondShift()
    {
        foreach (object step in LoadLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(1, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Toy)),
            Shelf(9,
                Column(ItemType.Lamp, ItemType.Toy, ItemType.Plant, ItemType.Beauty),
                Column(ItemType.Plant, ItemType.Toy, ItemType.MapBall, ItemType.Beauty),
                Column(ItemType.MapBall, ItemType.Toy, ItemType.Lamp, ItemType.Beauty)));
        EventCounter counter = new EventCounter(_session, _conveyor);
        expected = Move(expected, new ColumnPosition(1, 0), new ColumnPosition(0, 2));

        foreach (object step in WaitUntilSettled("match from shift")) yield return step;

        counter.Dispose();
        ExpectBoard(expected, "match from shift");
        Expect(counter.MatchCount == 2 && counter.ShiftCount == 1, "a match from the shift must be scored without a second shift");
    }

    private static IEnumerable ValidateShortColumns()
    {
        foreach (object step in LoadLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(1, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Toy)),
            Shelf(10, Column(), Column(ItemType.Lamp), Column(ItemType.Plant, ItemType.Toy)));
        expected = Move(expected, new ColumnPosition(1, 0), new ColumnPosition(0, 2));

        foreach (object step in WaitUntilSettled("short columns")) yield return step;

        ExpectBoard(expected, "short columns");
    }

    private static IEnumerable ValidateParallelMoves()
    {
        foreach (object step in LoadLevel()) yield return step;

        BoardStateSnapshot expected = Arrange(ParallelMovesLayout());
        EventCounter counter = new EventCounter(_session, _conveyor);
        expected = Move(expected, new ColumnPosition(1, 0), new ColumnPosition(0, 2));
        expected = Move(expected, new ColumnPosition(3, 0), new ColumnPosition(2, 2));

        foreach (object step in WaitUntilSettled("parallel moves")) yield return step;

        counter.Dispose();
        ExpectBoard(expected, "parallel moves");
        Expect(counter.ShiftCount == 2, "two parallel matching moves must shift the conveyors twice");
    }

    private static IEnumerable ValidatePauseDuringShift()
    {
        foreach (object step in LoadLevel()) yield return step;

        BoardStateSnapshot expected = Move(Arrange(ParallelMovesLayout()), new ColumnPosition(1, 0), new ColumnPosition(0, 2));

        foreach (object step in WaitUntil(() => _conveyor.IsShifting, "shift start")) yield return step;

        Expect(_session.TryPause(), "the level must pause during a shift");
        string pausedHash = BoardStateFingerprint.CreateHash(_board.CreateSnapshot());
        Vector3[] pausedPositions = GetConveyorItemPositions();

        for (int frame = 0; frame < PausedFrameCount; frame++)
        {
            yield return null;
            Expect(_conveyor.IsShifting, "the shift must stay unfinished while paused");
            Expect(BoardStateFingerprint.CreateHash(_board.CreateSnapshot()) == pausedHash, "the board must not change while paused");
            Expect(GetConveyorItemPositions().SequenceEqual(pausedPositions), "conveyor items must not move while paused");
        }

        _session.Resume();
        Time.timeScale = ScenarioTimeScale;

        foreach (object step in WaitUntilSettled("resume after pause")) yield return step;

        ExpectBoard(expected, "pause");
    }

    private static IEnumerable ValidateDragFromReservedShelf()
    {
        foreach (object step in LoadLevel()) yield return step;

        BoardStateSnapshot expected = Move(
            Arrange(
                Shelf(0, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
                Shelf(1, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Toy)),
                Shelf(8, Column(ItemType.Plant, ItemType.Toy), Column(ItemType.Lamp, ItemType.Bear), Column(ItemType.MapBall, ItemType.Beauty))),
            new ColumnPosition(1, 0),
            new ColumnPosition(0, 2));
        Shelf conveyorShelf = _board.Shelves[8];
        ShelfItem draggedItem = conveyorShelf.Columns[0].FrontItem;
        Vector2 pressPosition = GetScreenCenter(draggedItem);
        Expect(_drag.TryBeginDrag(draggedItem, pressPosition), "a drag from an unreserved conveyor must start");
        _drag.UpdateDrag(pressPosition + new Vector2(120f, 60f));
        bool isReturnedBeforeShift = false;
        void CheckReturn() => isReturnedBeforeShift = !_session.ItemAnimations.IsAnimating(draggedItem) && draggedItem.transform.parent == conveyorShelf.ColumnViews[0].ItemAnchor;
        _conveyor.ShiftStarted += CheckReturn;

        foreach (object step in WaitUntil(() => !_drag.IsDragging, "drag cancellation")) yield return step;

        Expect(_board.IsShelfLocked(conveyorShelf), "the drag must be cancelled by the shelf reservation");

        foreach (object step in WaitUntilSettled("drag cancellation")) yield return step;

        _conveyor.ShiftStarted -= CheckReturn;
        Expect(isReturnedBeforeShift, "the shift must wait until the dragged item returns");
        ExpectBoard(expected, "drag cancellation");
    }

    private static IEnumerable ValidateTimeoutWithPendingShift()
    {
        foreach (object step in LoadLevel()) yield return step;

        BoardStateSnapshot expected = Move(Arrange(ParallelMovesLayout()), new ColumnPosition(1, 0), new ColumnPosition(0, 2));

        foreach (object step in WaitUntil(() => _session.HasPendingBoardChanges, "pending shift")) yield return step;

        _session.Timer.SetCountdownRate(ExpiringCountdownRate);

        foreach (object step in WaitUntil(() => _session.State != LevelState.Playing, "timer expiry")) yield return step;

        while (_session.Result == null)
        {
            Expect(_session.State == LevelState.Ending, "the level must wait in the ending state");
            yield return null;
        }

        Expect(!_session.Result.Won && !_session.HasPendingBoardChanges && !_conveyor.IsShifting, "the loss must be decided after the pending shift");
        ExpectBoard(expected, "timeout");
    }

    private static IEnumerable ValidateTimeoutVictory()
    {
        foreach (object step in LoadLevel()) yield return step;

        Move(
            Arrange(
                Shelf(0, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
                Shelf(1, Column(ItemType.Ball), Column(), Column())),
            new ColumnPosition(1, 0),
            new ColumnPosition(0, 2));
        _session.Timer.SetCountdownRate(ExpiringCountdownRate);

        foreach (object step in WaitUntil(() => _session.Result != null, "timeout victory")) yield return step;

        Expect(_session.Result.Won && _board.IsCleared, "a board cleared by moves accepted before the timeout must win");
    }

    private static IEnumerable ValidateRestartWithPendingShift()
    {
        foreach (object step in LoadLevel()) yield return step;

        Move(Arrange(ParallelMovesLayout()), new ColumnPosition(1, 0), new ColumnPosition(0, 2));

        foreach (object step in WaitUntil(() => _session.HasPendingBoardChanges, "pending shift")) yield return step;

        LevelSession previousSession = _session;
        previousSession.Restart();

        foreach (object step in WaitUntil(() => FindSession() != null && FindSession() != previousSession && FindSession().State == LevelState.Playing, "restart")) yield return step;

        CaptureScene();

        for (int frame = 0; frame < 30; frame++)
            yield return null;

        Expect(!_session.HasPendingBoardChanges && _session.State == LevelState.Playing, "a restart must drop pending shifts");
    }

    private static IEnumerable ValidateFilterSolutionReplay()
    {
        LevelEntry level = LoadFilterLevel();

        foreach (object step in LoadLevel(level)) yield return step;

        ExpectFilterOnBox("C-F1");
        BoardStateSnapshot expected = _board.CreateSnapshot();
        string layoutHash = BoardStateFingerprint.CreateHash(expected);
        TimedLevelVariant variant = level.Definition.Variants.FirstOrDefault(candidate => candidate.LayoutHash == layoutHash);
        Expect(variant != null, "C-F1: the runtime board must start from a variant of level 20");

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            ExpectSuggestionRespectsFilters("C-F1");
            expected = Move(expected, move.Source, move.Target);

            foreach (object step in WaitUntilSettled("C-F1 solution move")) yield return step;

            ExpectBoard(expected, "C-F1 solution move");
        }

        Expect(_board.IsCleared, "C-F1: the solution must clear the board");
    }

    private static IEnumerable ValidateRejectedDropFromConveyor()
    {
        foreach (object step in LoadLevel(LoadFilterLevel())) yield return step;

        BoardStateSnapshot expected = Arrange(
            Shelf(BoxShelfIndex, Column(), Column(ItemType.Lamp), Column(ItemType.Ball)),
            Shelf(BeltShelfIndex, Column(ItemType.Bear, ItemType.Toy), Column(ItemType.Plant, ItemType.Beauty), Column(ItemType.MapBall, ItemType.Toy)));
        Shelf box = _board.Shelves[BoxShelfIndex];
        ShelfFilterSignView sign = _signs.Single(candidate => candidate.Shelf == box);
        ShelfItem item = _board.Shelves[BeltShelfIndex].Columns[0].FrontItem;
        int rejectionCount = 0;

        void HandleDropRejected(Vector2 screenPosition) => rejectionCount++;

        _drag.DropRejected += HandleDropRejected;

        try
        {
            Expect(_drag.TryBeginDrag(item, GetScreenCenter(item)), "C-F2: the drag from the conveyor must start");
            Expect(sign.IsDimmed, "C-F2: the box sign must dim for a type it does not accept");
            _drag.EndDrag(Camera.main.WorldToScreenPoint(box.ColumnViews[0].ItemAnchor.position));

            foreach (object step in WaitUntilSettled("C-F2")) yield return step;
        }
        finally
        {
            _drag.DropRejected -= HandleDropRejected;
        }

        Expect(rejectionCount == 1, "C-F2: dropping a rejected type on the box must be refused");
        Expect(!sign.IsDimmed, "C-F2: the box sign must stop dimming after the drag");
        ExpectBoard(expected, "C-F2");
        Expect(item.transform.parent == _board.Shelves[BeltShelfIndex].ColumnViews[0].ItemAnchor, "C-F2: the refused item must return to its conveyor column");
    }

    private static IEnumerable ValidateStuckTripleFromShift()
    {
        foreach (object step in LoadLevel(LoadFilterLevel())) yield return step;

        BoardStateSnapshot expected = Arrange(
            Shelf(0, Column(ItemType.Plant), Column(ItemType.Plant), Column()),
            Shelf(BoxShelfIndex, Column(), Column(ItemType.Ball), Column()),
            Shelf(2, Column(ItemType.Plant), Column(ItemType.Toy), Column()),
            Shelf(3, Column(ItemType.Toy), Column(ItemType.Toy), Column()),
            Shelf(BeltShelfIndex, Column(ItemType.Bear, ItemType.Lamp), Column(ItemType.Toy, ItemType.Lamp), Column(ItemType.Beauty, ItemType.Lamp)));
        Shelf belt = _board.Shelves[BeltShelfIndex];
        ShelfFilterSignView sign = _signs.Single(candidate => candidate.Shelf == _board.Shelves[BoxShelfIndex]);
        int reminderCount = 0;

        void HandleBoardSettled()
        {
            if (sign.IsPulsing && belt.Columns.All(column => DOTween.IsTweening(column.FrontItem.transform)))
                reminderCount++;
        }

        _session.BoardSettled += HandleBoardSettled;

        try
        {
            expected = Move(expected, new ColumnPosition(2, 0), new ColumnPosition(0, 2));

            foreach (object step in WaitUntilSettled("C-F3 shift")) yield return step;
        }
        finally
        {
            _session.BoardSettled -= HandleBoardSettled;
        }

        ExpectBoard(expected, "C-F3 shift");
        Expect(belt.HasMatch() && !_board.CanMatch(belt), "C-F3: three Lamp items brought by the shift must stay on the conveyor");
        Expect(reminderCount > 0, "C-F3: the stuck triple on the conveyor must pulse its box sign and shake its items");

        ShelfItem toy = _board.Shelves[2].Columns[1].FrontItem;
        Expect(_drag.TryBeginDrag(toy, GetScreenCenter(toy)), "C-F3: the next drag must start");
        Expect(belt.Columns.All(column => !DOTween.IsTweening(column.FrontItem.transform)), "C-F3: picking up an item must stop the shakes on the conveyor");
        BoardStateSnapshot afterSecondShift = Simulate(_board.CreateSnapshot(), new ColumnPosition(2, 1), new ColumnPosition(3, 2));
        _drag.EndDrag(Camera.main.WorldToScreenPoint(_board.Shelves[3].ColumnViews[2].ItemAnchor.position));

        foreach (object step in WaitUntilSettled("C-F3 second shift")) yield return step;

        ExpectBoard(afterSecondShift, "C-F3 second shift");
        Expect(belt.Columns.All(column => column.FrontItem.transform.localPosition.sqrMagnitude < 0.000001f), "C-F3: conveyor items must rest in front of their columns after the next shift");
    }

    private static IEnumerable ValidateSuggestionSkipsTripleOnConveyor()
    {
        foreach (object step in LoadLevel(LoadFilterLevel())) yield return step;

        Arrange(
            Shelf(BoxShelfIndex, Column(ItemType.Lamp), Column(), Column(ItemType.Ball)),
            Shelf(2, Column(ItemType.Lamp), Column(ItemType.Plant), Column()),
            Shelf(BeltShelfIndex, Column(ItemType.Lamp, ItemType.Toy), Column(ItemType.Lamp, ItemType.Bear), Column()));

        Expect(_suggestions.TryGetSuggestion(out MoveSuggestion suggestion), "C-F4: the arranged board must offer a move");
        Expect(
            suggestion.SourceColumn.FrontItem.Type != ItemType.Lamp || suggestion.TargetColumn.Shelf != _board.Shelves[BeltShelfIndex],
            "C-F4: the suggestion must not collect Lamp on the conveyor");
        ExpectSuggestionRespectsFilters("C-F4");
    }

    private static void ExpectFilterOnBox(string scenario)
    {
        Shelf box = _board.Shelves[BoxShelfIndex];
        Expect(_board.Shelves.Count(_board.IsFiltered) == 1 && _board.GetAcceptedTypes(box).SequenceEqual(new[] { ItemType.Lamp }), $"{scenario}: level 20 must have one Lamp box on shelf {BoxShelfIndex}");

        foreach (ShelfFilterSignView sign in _signs)
            Expect(sign.IsShown == (sign.Shelf == box), $"{scenario}: the sign on {sign.Shelf.name} must be shown only on the box");
    }

    private static void ExpectSuggestionRespectsFilters(string scenario)
    {
        if (!_suggestions.TryGetSuggestion(out MoveSuggestion suggestion))
            return;

        ShelfColumnView source = suggestion.SourceColumn;
        ShelfColumnView target = suggestion.TargetColumn;
        Expect(_board.Accepts(target.Shelf, source.FrontItem.Type), $"{scenario}: the suggestion must not put {source.FrontItem.Type} on {target.Shelf.name}");
        Expect(target.IsEmpty || _board.Accepts(source.Shelf, target.FrontItem.Type), $"{scenario}: the suggested swap must not put {target.FrontItem?.Type} on {source.Shelf.name}");
        Expect(suggestion.TargetMatchingItems.Count == 0 || _board.CanMatchType(target.Shelf, source.FrontItem.Type), $"{scenario}: the suggestion must not collect {source.FrontItem.Type} on {target.Shelf.name}");
    }

    private static BoardStateSnapshot Simulate(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target)
    {
        if (!Simulator.TrySimulate(board, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            throw new InvalidOperationException("The scenario move is not valid in the simulator.");

        return simulation.State;
    }

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns)[] ParallelMovesLayout()
    {
        return new[]
        {
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(1, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Toy)),
            Shelf(2, Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            Shelf(3, Column(ItemType.Lamp), Column(ItemType.MapBall), Column(ItemType.Beauty)),
            Shelf(8, Column(ItemType.Plant, ItemType.Toy, ItemType.Bear), Column(ItemType.Bear, ItemType.Beauty, ItemType.Toy), Column(ItemType.MapBall, ItemType.Plant, ItemType.Beauty))
        };
    }

    private static IEnumerable LoadLevel() => LoadLevel(GetLevelWithoutFilters());

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
        _conveyor = UnityEngine.Object.FindObjectOfType<ConveyorController>();
        _drag = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();
        _suggestions = UnityEngine.Object.FindObjectOfType<MoveSuggestionProvider>();
        _signs = UnityEngine.Object.FindObjectsOfType<ShelfFilterSignView>(true);

        if (_board.ConveyorShelves.Count != 4)
            throw new InvalidOperationException("Conveyor scenarios require all four conveyor shelves.");

        if (_suggestions == null || _signs.Length == 0)
            throw new InvalidOperationException("Conveyor scenarios need move suggestions and shelf filter signs in FifthLevel.");
    }

    private static BoardStateSnapshot Arrange(params (int ShelfIndex, ColumnStateSnapshot[] Columns)[] shelves)
    {
        ShelfItemCatalog catalog = _session.CurrentLevel.Definition.ItemCatalog;

        foreach (Shelf shelf in _board.Shelves)
        {
            foreach (ShelfColumn column in shelf.Columns)
            {
                while (!column.IsEmpty)
                    UnityEngine.Object.Destroy(column.TakeFront().gameObject);
            }
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
        if (!Simulator.TrySimulate(expected, source, target, out BoardMoveSimulation simulation))
            throw new InvalidOperationException("The scenario move is not valid in the simulator.");

        ShelfColumnView sourceView = _board.Shelves[source.ShelfIndex].ColumnViews[source.ColumnIndex];
        ShelfColumnView targetView = _board.Shelves[target.ShelfIndex].ColumnViews[target.ColumnIndex];
        MoveOutcome outcome = _session.TryStartMove(sourceView, targetView, out Action completePlacement);
        Expect(outcome.IsSuccessful, "the scenario move must be accepted");
        completePlacement?.Invoke();
        return simulation.State;
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
        return WaitUntil(() => _session.IsBoardSettled && !_conveyor.IsShifting, description);
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

    private static Vector3[] GetConveyorItemPositions()
    {
        return _board.ConveyorShelves
            .SelectMany(shelf => shelf.Columns)
            .SelectMany(column => column.Items)
            .Select(item => item.transform.position)
            .ToArray();
    }

    private static Vector2 GetScreenCenter(ShelfItem item)
    {
        if (!item.GetComponent<ShelfItemTargetView>().TryGetBounds(out Bounds bounds))
            throw new InvalidOperationException($"{item.name}: no bounds to press.");

        return Camera.main.WorldToScreenPoint(bounds.center);
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Conveyor Play Mode: {message}.");
    }

    private static LevelSession FindSession() => UnityEngine.Object.FindObjectOfType<LevelSession>();

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns) Shelf(int shelfIndex, params ColumnStateSnapshot[] columns) => (shelfIndex, columns);

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    private static LevelEntry LoadFilterLevel()
    {
        LevelEntry level = AssetDatabase.LoadAssetAtPath<LevelEntry>(LevelEntryPath);

        if (level == null || !level.Definition.HasShelfFilters)
            throw new InvalidOperationException("Conveyor scenarios need level 20 with its box.");

        return level;
    }

    private static LevelEntry GetLevelWithoutFilters()
    {
        if (_levelWithoutFilters != null)
            return _levelWithoutFilters;

        LevelEntry filterLevel = LoadFilterLevel();
        BoardStateSnapshot boardShape = new BoardStateSnapshot(filterLevel.Definition.Variants[0].CreateLayout().Shelves
            .Select(shelf => new ShelfStateSnapshot(shelf.Columns.Select(_ => new ColumnStateSnapshot(Array.Empty<ItemType>())).ToArray()))
            .ToArray());
        TimedLevelDefinition definition = UnityEngine.Object.Instantiate(filterLevel.Definition);
        definition.name = "ConveyorScenarioDefinition";
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_shelfFilters").arraySize = 0;
        serializedDefinition.FindProperty("_filteredEmptyColumnCount").intValue = 0;
        serializedDefinition.FindProperty("_shuffleSwapCount").intValue = 0;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        TimedLevelGenerationResult generation = new TimedLevelLayoutGenerator().GenerateWithSolution(definition, boardShape, LevelWithoutFiltersSeed);
        TimedLevelVariantGenerator.WriteVariants(definition, new[]
        {
            new TimedLevelVariant(
                LevelWithoutFiltersSeed,
                TimedLevelLayoutRules.GeneratorVersion,
                generation.SolutionMoves.Count,
                BoardStateFingerprint.CreateHash(generation.State),
                generation.State,
                generation.SolutionMoves)
        });
        LevelEntry level = ScriptableObject.CreateInstance<LevelEntry>();
        level.name = "ConveyorScenarioLevel";
        level.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedLevel = new SerializedObject(level);
        serializedLevel.FindProperty("_number").intValue = filterLevel.Number;
        serializedLevel.FindProperty("_sceneName").stringValue = SceneName;
        serializedLevel.FindProperty("_definition").objectReferenceValue = definition;
        serializedLevel.FindProperty("_available").boolValue = true;
        serializedLevel.ApplyModifiedPropertiesWithoutUndo();
        _levelWithoutFilters = level;
        return level;
    }

    private static void SelectLevel(LevelEntry level)
    {
        LevelSelectionState selection = AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath);

        if (selection == null)
            throw new InvalidOperationException("Conveyor scenarios need the level selection.");

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

        if (failed)
            Debug.LogError("CONVEYOR_PLAYMODE_FAIL");
        else
            Debug.Log("CONVEYOR_PLAYMODE_PASS");
    }

    private sealed class EventCounter : IDisposable
    {
        private readonly LevelSession _session;
        private readonly ConveyorController _conveyor;

        public int MatchCount { get; private set; }
        public int ShiftCount { get; private set; }

        public EventCounter(LevelSession session, ConveyorController conveyor)
        {
            _session = session;
            _conveyor = conveyor;
            _session.MatchSucceeded += CountMatch;
            _conveyor.ShiftStarted += CountShift;
        }

        public void Dispose()
        {
            _session.MatchSucceeded -= CountMatch;
            _conveyor.ShiftStarted -= CountShift;
        }

        private void CountMatch(MatchResolution match) => MatchCount++;

        private void CountShift() => ShiftCount++;
    }
}
