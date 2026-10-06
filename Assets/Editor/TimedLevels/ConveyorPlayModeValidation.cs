using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    private static readonly BoardMoveSimulator Simulator = new BoardMoveSimulator();
    private static IEnumerator _scenarios;
    private static int _lastFrame = -1;
    private static string _unexpectedError;
    private static LevelSession _session;
    private static ShelfBoard _board;
    private static ConveyorController _conveyor;
    private static ShelfItemDragController _drag;

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
        SelectLevel();
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
            ValidateRestartWithPendingShift
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

    private static IEnumerable LoadLevel()
    {
        SelectLevel();
        SceneManager.LoadScene(SceneName);
        yield return null;

        foreach (object step in WaitUntil(() => FindSession() != null && FindSession().State == LevelState.Playing && FindSession().IsBoardSettled, "level load")) yield return step;

        CaptureScene();
        Time.timeScale = ScenarioTimeScale;
    }

    private static void CaptureScene()
    {
        _session = FindSession();
        _board = UnityEngine.Object.FindObjectOfType<ShelfBoard>();
        _conveyor = UnityEngine.Object.FindObjectOfType<ConveyorController>();
        _drag = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();

        if (_board.ConveyorShelves.Count != 4)
            throw new InvalidOperationException("Conveyor scenarios require all four conveyor shelves.");
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

    private static void SelectLevel()
    {
        LevelSelectionState selection = AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath);
        LevelEntry level = AssetDatabase.LoadAssetAtPath<LevelEntry>(LevelEntryPath);

        if (selection == null || level == null)
            throw new InvalidOperationException("Conveyor scenarios need the level selection and level 20.");

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
