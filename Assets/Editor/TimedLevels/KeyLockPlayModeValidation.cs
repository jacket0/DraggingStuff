using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class KeyLockPlayModeValidation
{
    private const string ActiveKey = "KeyLockPlayModeValidation.Active";
    private const string CompletedKey = "KeyLockPlayModeValidation.Completed";
    private const string FailedKey = "KeyLockPlayModeValidation.Failed";
    private const string RunInBackgroundKey = "KeyLockPlayModeValidation.RunInBackground";
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string KeyPrefabPath = "Assets/Prefabs/Keys/ShelfKey.prefab";
    private const string ScenePath = "Assets/Scenes/SixthLevel.unity";
    private const string SceneName = "SixthLevel";
    private const int ScenarioLevelNumber = 26;
    private const int ScenarioSeedBase = 2199000;
    private const int MaximumScenarioSeeds = 2000;
    private const int BoardShelfCount = 10;
    private const int ShelfCapacity = 3;
    private const int PrototypeSafeIndex = 1;
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
    private static KeyLocksController _keyLocks;
    private static ShelfItemDragController _drag;
    private static MoveSuggestionProvider _suggestions;
    private static LevelEntry _scenarioLevel;

    static KeyLockPlayModeValidation()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Application.logMessageReceived -= HandleLogMessage;
        Application.logMessageReceived += HandleLogMessage;
    }

    [MenuItem("Tools/Timed Levels/Validate Key Lock Play Mode")]
    public static void Begin()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(CompletedKey, false);
        SessionState.SetBool(FailedKey, false);
        SessionState.SetBool(RunInBackgroundKey, Application.runInBackground);
        Application.runInBackground = true;
        SelectLevel(GetScenarioLevel());
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
            ValidateSolutionReplay,
            ValidateTwoKeysInOneMove,
            ValidatePauseDuringFlight,
            ValidateRestartWithPendingCollection,
            ValidateTimeoutWithPendingCollection,
            ValidateDragFromShelfWithKey,
            ValidateDropOnLockedShelf,
            ValidateSuggestionPrefersKey,
            ValidateKeyShelfFreedDuringOpening
        };

        foreach (Func<IEnumerable> scenario in scenarios)
        {
            int startFrame = Time.frameCount;

            foreach (object step in scenario())
                yield return step;

            Debug.Log($"KEY_LOCK_SCENARIO_PASS: {scenario.Method.Name} in {Time.frameCount - startFrame} frames");
        }
    }

    private static IEnumerable ValidateSolutionReplay()
    {
        LevelEntry level = GetScenarioLevel();

        foreach (object step in LoadLevel(level)) yield return step;

        TimedLevelVariant variant = level.Definition.Variants[0];
        BoardStateSnapshot expected = _board.CreateSnapshot();
        Expect(BoardStateFingerprint.CreateHash(expected) == variant.LayoutHash, "P1: the runtime board must start from the scenario variant");
        Shelf safe = _board.Shelves[PrototypeSafeIndex];
        int safeItemCount = CountItems(safe);
        int openedCount = 0;
        bool hasConservedSafeItems = true;

        void HandleSafeOpened(Shelf openedSafe)
        {
            openedCount++;
            hasConservedSafeItems &= openedSafe == safe && CountItems(safe) == safeItemCount;
        }

        _keyLocks.SafeOpened += HandleSafeOpened;

        try
        {
            foreach (TimedLevelMove move in variant.CreateSolution())
            {
                bool opensSafe = Simulate(expected, move.Source, move.Target).OpenedShelfIndexes.Contains(PrototypeSafeIndex);
                Expect(_board.IsKeyLocked(safe) == (openedCount == 0), "P1: the safe must stay locked until its key is collected");
                expected = Move(expected, move.Source, move.Target);

                foreach (object step in WaitUntilSettledWatchingKeys("P1 solution move")) yield return step;

                ExpectBoard(expected, "P1 solution move");
                Expect(!_board.IsKeyLocked(safe) == (openedCount == 1), "P1: the runtime safe must open together with the simulation");
                Expect(!opensSafe || openedCount == 1, "P1: the safe must open on the same step as in the simulation");
            }
        }
        finally
        {
            _keyLocks.SafeOpened -= HandleSafeOpened;
        }

        Expect(openedCount == 1 && hasConservedSafeItems, "P2: collecting the key must keep every item of the safe");
        Expect(_board.IsCleared, "P1: the solution must clear the board");
    }

    private static IEnumerable ValidateTwoKeysInOneMove()
    {
        foreach (object step in LoadLevel(GetScenarioLevel())) yield return step;

        BoardStateSnapshot expected = Arrange(new[] { 0, 2 }, TwoKeysLayout());
        int openedCount = 0;

        void HandleSafeOpened(Shelf safe) => openedCount++;

        _keyLocks.SafeOpened += HandleSafeOpened;

        try
        {
            expected = Move(expected, new ColumnPosition(3, 0), new ColumnPosition(4, 2));

            foreach (object step in WaitUntilSettledWatchingKeys("P4")) yield return step;
        }
        finally
        {
            _keyLocks.SafeOpened -= HandleSafeOpened;
        }

        ExpectBoard(expected, "P4");
        Expect(openedCount == 2 && !_board.HasKeyLocks, "P4: two keys freed by one move must open both safes once");
    }

    private static IEnumerable ValidatePauseDuringFlight()
    {
        foreach (object step in LoadLevel(GetScenarioLevel())) yield return step;

        BoardStateSnapshot expected = Move(Arrange(new[] { 0 }, SingleKeyLayout()), new ColumnPosition(3, 0), new ColumnPosition(4, 2));

        foreach (object step in WaitUntil(() => _keyLocks.IsCollecting && FindFlyingKey() != null, "key flight")) yield return step;

        Expect(_session.TryPause(), "P5: the level must pause during the key flight");
        ShelfKey key = FindFlyingKey();
        Vector3 pausedPosition = key.transform.position;
        string pausedHash = BoardStateFingerprint.CreateHash(_board.CreateSnapshot());

        for (int frame = 0; frame < PausedFrameCount; frame++)
        {
            yield return null;
            Expect(_keyLocks.IsCollecting, "P5: the collection must stay unfinished while paused");
            Expect(key.transform.position == pausedPosition, "P5: the key must not fly while paused");
            Expect(BoardStateFingerprint.CreateHash(_board.CreateSnapshot()) == pausedHash, "P5: the board must not change while paused");
        }

        _session.Resume();
        Time.timeScale = ScenarioTimeScale;

        foreach (object step in WaitUntilSettledWatchingKeys("P5 resume")) yield return step;

        ExpectBoard(expected, "P5");
    }

    private static IEnumerable ValidateKeyShelfFreedDuringOpening()
    {
        foreach (object step in LoadLevel(GetScenarioLevel())) yield return step;

        BoardStateSnapshot expected = Move(Arrange(new[] { 0 }, SingleKeyLayout()), new ColumnPosition(3, 0), new ColumnPosition(4, 2));
        Shelf keyShelf = _board.Shelves[3];
        Shelf safe = _board.Shelves[0];

        foreach (object step in WaitUntil(() => _keyLocks.IsCollecting && FindFlyingKey() != null, "key flight")) yield return step;
        foreach (object step in WaitUntil(() => !_board.IsShelfLocked(keyShelf) || !_keyLocks.IsCollecting, "key shelf release")) yield return step;

        Expect(_keyLocks.IsCollecting, "P11: the key shelf must be released while the safe is still opening");
        Expect(_session.CanPickUp(keyShelf.ColumnViews[0]), "P11: an item of the key shelf must be available during the opening");
        Expect(_board.IsShelfLocked(safe) || _board.IsKeyLocked(safe), "P11: the safe must stay unavailable until its door is open");

        foreach (object step in WaitUntilSettledWatchingKeys("P11")) yield return step;

        ExpectBoard(expected, "P11");
    }

    private static IEnumerable ValidateRestartWithPendingCollection()
    {
        foreach (object step in LoadLevel(GetScenarioLevel())) yield return step;

        Move(Arrange(new[] { 0 }, SingleKeyLayout()), new ColumnPosition(3, 0), new ColumnPosition(4, 2));

        foreach (object step in WaitUntil(() => _session.HasPendingBoardChanges, "pending collection")) yield return step;

        LevelSession previousSession = _session;
        previousSession.Restart();

        foreach (object step in WaitUntil(() => FindSession() != null && FindSession() != previousSession && FindSession().State == LevelState.Playing, "restart")) yield return step;

        CaptureScene();

        for (int frame = 0; frame < 30; frame++)
            yield return null;

        Expect(!_session.HasPendingBoardChanges && !_keyLocks.IsCollecting && _session.State == LevelState.Playing, "P6: a restart must drop the pending collection");
    }

    private static IEnumerable ValidateTimeoutWithPendingCollection()
    {
        foreach (object step in LoadLevel(GetScenarioLevel())) yield return step;

        BoardStateSnapshot expected = Move(Arrange(new[] { 0 }, SingleKeyLayout()), new ColumnPosition(3, 0), new ColumnPosition(4, 2));

        foreach (object step in WaitUntil(() => _session.HasPendingBoardChanges, "pending collection")) yield return step;

        _session.Timer.SetCountdownRate(ExpiringCountdownRate);

        foreach (object step in WaitUntil(() => _session.State != LevelState.Playing, "timer expiry")) yield return step;

        while (_session.Result == null)
        {
            Expect(_session.State == LevelState.Ending, "P7: the level must wait in the ending state");
            yield return null;
        }

        Expect(!_session.Result.Won && !_session.HasPendingBoardChanges && !_keyLocks.IsCollecting, "P7: the result must be decided after the collection");
        ExpectBoard(expected, "P7");
    }

    private static IEnumerable ValidateDragFromShelfWithKey()
    {
        foreach (object step in LoadLevel(GetScenarioLevel())) yield return step;

        BoardStateSnapshot expected = Move(Arrange(new[] { 0, 2 }, TwoKeysLayout()), new ColumnPosition(3, 0), new ColumnPosition(4, 2));
        Shelf keyShelf = _board.Shelves[4];
        ShelfColumn keyColumn = keyShelf.Columns[0];

        foreach (object step in WaitUntil(() => keyColumn.FrontKey != null && !_board.IsShelfLocked(keyShelf) && _keyLocks.IsCollecting, "a key waiting behind another collection")) yield return step;

        ShelfItem draggedItem = keyShelf.Columns[1].FrontItem;
        Vector2 pressPosition = GetScreenCenter(draggedItem);
        Expect(_drag.TryBeginDrag(draggedItem, pressPosition), "P8: a drag from the shelf with a waiting key must start");
        _drag.UpdateDrag(pressPosition + new Vector2(120f, 60f));
        while (keyColumn.FrontKey != null)
        {
            Expect(!_drag.IsDragging || !_board.IsShelfLocked(keyShelf), "P8: the reservation must cancel the drag");
            yield return null;
        }

        Expect(
            !_drag.IsDragging && !_session.ItemAnimations.IsAnimating(draggedItem) && draggedItem.transform.parent == keyShelf.ColumnViews[1].ItemAnchor,
            "P8: the key must be collected only after the dragged item returns");

        foreach (object step in WaitUntilSettledWatchingKeys("P8")) yield return step;

        ExpectBoard(expected, "P8");
    }

    private static IEnumerable ValidateDropOnLockedShelf()
    {
        foreach (object step in LoadLevel(GetScenarioLevel())) yield return step;

        BoardStateSnapshot expected = Arrange(
            new[] { 0 },
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            Shelf(3, KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.Beauty), Column()),
            Shelf(4, Column(), Column(), Column()));
        ShelfItem item = _board.Shelves[3].Columns[1].FrontItem;
        int rejectionCount = 0;

        void HandleDropRejected(Vector2 screenPosition) => rejectionCount++;

        _drag.DropRejected += HandleDropRejected;

        try
        {
            Expect(_drag.TryBeginDrag(item, GetScreenCenter(item)), "P9: the drag must start");
            _drag.EndDrag(Camera.main.WorldToScreenPoint(_board.Shelves[0].ColumnViews[1].ItemAnchor.position));

            foreach (object step in WaitUntilSettledWatchingKeys("P9")) yield return step;
        }
        finally
        {
            _drag.DropRejected -= HandleDropRejected;
        }

        Expect(rejectionCount == 1, "P9: a drop on a locked safe must be refused");
        ExpectBoard(expected, "P9");
        Expect(item.transform.parent == _board.Shelves[3].ColumnViews[1].ItemAnchor, "P9: the refused item must return to its column");
    }

    private static IEnumerable ValidateSuggestionPrefersKey()
    {
        foreach (object step in LoadLevel(GetScenarioLevel())) yield return step;

        Arrange(
            new[] { 0 },
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            Shelf(3, KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear), Column(ItemType.MapBall)),
            Shelf(4, Column(ItemType.Plant), Column(), Column()));

        foreach (object step in WaitUntilSettledWatchingKeys("P10 arrange")) yield return step;

        Expect(_suggestions.TryGetSuggestion(out MoveSuggestion suggestion), "P10: the arranged board must offer a move");
        Expect(suggestion.SourceColumn == _board.Shelves[3].ColumnViews[0], "P10: the suggestion must free the key before a useless transfer");
    }

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns)[] SingleKeyLayout()
    {
        return new[]
        {
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            Shelf(3, KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear), Column(ItemType.MapBall)),
            Shelf(4, Column(ItemType.Lamp), Column(ItemType.Lamp), Column())
        };
    }

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns)[] TwoKeysLayout()
    {
        return new[]
        {
            Shelf(0, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            Shelf(2, Column(ItemType.Toy), Column(ItemType.MapBall), Column(ItemType.Beauty)),
            Shelf(3, KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear), Column(ItemType.MapBall)),
            Shelf(4, KeyColumn(1, 2, ItemType.Lamp, ItemType.TrainToy), Column(ItemType.Lamp, ItemType.Plant), Column())
        };
    }

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
        _keyLocks = UnityEngine.Object.FindObjectOfType<KeyLocksController>();
        _drag = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();
        _suggestions = UnityEngine.Object.FindObjectOfType<MoveSuggestionProvider>();

        if (_board.Shelves.Count != BoardShelfCount || _keyLocks == null || _drag == null || _suggestions == null)
            throw new InvalidOperationException("Key lock scenarios need the SixthLevel board, the key locks controller, the drag controller and move suggestions.");
    }

    private static BoardStateSnapshot Arrange(IReadOnlyList<int> lockedShelfIndices, params (int ShelfIndex, ColumnStateSnapshot[] Columns)[] shelves)
    {
        ShelfItemCatalog catalog = _session.CurrentLevel.Definition.ItemCatalog;
        ShelfKey keyPrefab = AssetDatabase.LoadAssetAtPath<ShelfKey>(KeyPrefabPath);

        foreach (Shelf shelf in _board.Shelves)
        {
            foreach (ShelfColumn column in shelf.Columns)
            {
                while (!column.IsEmpty)
                {
                    if (column.FrontKey != null)
                        UnityEngine.Object.Destroy(column.TakeFrontKey().gameObject);
                    else
                        UnityEngine.Object.Destroy(column.TakeFront().gameObject);
                }
            }

            if (_board.IsKeyLocked(shelf))
                _board.OpenKeyLockedShelf(shelf);
        }

        foreach ((int shelfIndex, ColumnStateSnapshot[] columns) in shelves)
        {
            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                ShelfColumn column = _board.Shelves[shelfIndex].Columns[columnIndex];

                foreach (ItemType type in columns[columnIndex].Items)
                    column.Append(UnityEngine.Object.Instantiate(catalog.GetPrefab(type)));

                foreach (ColumnKeySnapshot keyLayout in columns[columnIndex].Keys)
                {
                    ShelfKey key = UnityEngine.Object.Instantiate(keyPrefab);
                    key.Bind(_board.Shelves[keyLayout.LockedShelfIndex]);
                    column.AddKey(key, keyLayout.ItemsInFront);
                }
            }
        }

        foreach (int shelfIndex in lockedShelfIndices)
            _board.CloseKeyLockedShelf(_board.Shelves[shelfIndex]);

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
                ShelfColumn column = shelf.Columns[columnIndex];
                Expect(column.Items.All(item => item.transform.parent == anchor), $"{scenario}: every item must rest on its column anchor");
                Expect(column.Keys.All(key => key.transform.parent == anchor), $"{scenario}: every key must rest on its column anchor");
            }
        }
    }

    private static IEnumerable WaitUntilSettledWatchingKeys(string description)
    {
        for (int frame = 0; !(_session.IsBoardSettled && !_keyLocks.IsCollecting); frame++)
        {
            if (frame >= MaximumWaitFrames)
                throw new InvalidOperationException($"Timed out waiting for {description}.");

            ExpectKeyRules(description);
            yield return null;
        }
    }

    private static void ExpectKeyRules(string description)
    {
        ShelfColumn[] frontKeyColumns = _board.Shelves.SelectMany(shelf => shelf.Columns).Where(column => column.FrontKey != null).ToArray();

        if (!_keyLocks.IsCollecting && frontKeyColumns.Length == 0)
            return;

        Expect(!_suggestions.TryGetSuggestion(out _), $"P3 ({description}): no move may be suggested while a key is collected");
        Expect(frontKeyColumns.All(column => !_board.CanPickUp(column)), $"P3 ({description}): a column with a key in front must not be picked up");
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

    private static ShelfKey FindFlyingKey() => UnityEngine.Object.FindObjectsOfType<ShelfKey>().FirstOrDefault(key => key.Column == null);

    private static int CountItems(Shelf shelf) => shelf.Columns.Sum(column => column.Count);

    private static Vector2 GetScreenCenter(ShelfItem item)
    {
        if (!item.GetComponent<ShelfItemTargetView>().TryGetBounds(out Bounds bounds))
            throw new InvalidOperationException($"{item.name}: no bounds to press.");

        return Camera.main.WorldToScreenPoint(bounds.center);
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Key Lock Play Mode: {message}.");
    }

    private static LevelSession FindSession() => UnityEngine.Object.FindObjectOfType<LevelSession>();

    private static (int ShelfIndex, ColumnStateSnapshot[] Columns) Shelf(int shelfIndex, params ColumnStateSnapshot[] columns) => (shelfIndex, columns);

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    private static ColumnStateSnapshot KeyColumn(int itemsInFront, int lockedShelfIndex, params ItemType[] items)
    {
        return new ColumnStateSnapshot(items, new[] { new ColumnKeySnapshot(itemsInFront, lockedShelfIndex) });
    }

    public static LevelEntry GetScenarioLevel()
    {
        if (_scenarioLevel != null)
            return _scenarioLevel;

        TimedLevelDefinition definition = CreateScenarioDefinition();
        BoardStateSnapshot boardShape = new BoardStateSnapshot(Enumerable.Range(0, BoardShelfCount)
            .Select(_ => new ShelfStateSnapshot(Enumerable.Range(0, ShelfCapacity).Select(__ => new ColumnStateSnapshot(Array.Empty<ItemType>())).ToArray()))
            .ToArray());
        TimedLevelVariantGenerator.WriteVariants(definition, new[] { GenerateScenarioVariant(definition, boardShape) });
        LevelEntry level = ScriptableObject.CreateInstance<LevelEntry>();
        level.name = "KeyLockScenarioLevel";
        level.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedLevel = new SerializedObject(level);
        serializedLevel.FindProperty("_number").intValue = ScenarioLevelNumber;
        serializedLevel.FindProperty("_sceneName").stringValue = SceneName;
        serializedLevel.FindProperty("_definition").objectReferenceValue = definition;
        serializedLevel.FindProperty("_available").boolValue = true;
        serializedLevel.ApplyModifiedPropertiesWithoutUndo();
        _scenarioLevel = level;
        return level;
    }

    private static TimedLevelVariant GenerateScenarioVariant(TimedLevelDefinition definition, BoardStateSnapshot boardShape)
    {
        TimedLevelLayoutGenerator generator = new TimedLevelLayoutGenerator();
        TimedLevelKeyPlacer keyPlacer = new TimedLevelKeyPlacer();

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

            if (keyPlacer.TryPlace(generation.State, generation.SolutionMoves, definition.KeyLockedShelfIndices, definition.MinimumKeyDepth, seed, out BoardStateSnapshot keyedLayout)
                != TimedLevelKeyPlacer.PlacementStatus.Placed)
            {
                continue;
            }

            return new TimedLevelVariant(
                seed,
                TimedLevelLayoutRules.GeneratorVersion,
                generation.SolutionMoves.Count,
                BoardStateFingerprint.CreateHash(keyedLayout),
                keyedLayout,
                generation.SolutionMoves);
        }

        throw new InvalidOperationException("Key lock scenarios could not generate a level with a key.");
    }

    private static TimedLevelDefinition CreateScenarioDefinition()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        TimedLevelDefinition definition = ScriptableObject.CreateInstance<TimedLevelDefinition>();
        definition.name = "KeyLockScenarioDefinition";
        definition.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_timeLimitSeconds").intValue = 600;
        serializedDefinition.FindProperty("_twoStarTimeSeconds").intValue = 400;
        serializedDefinition.FindProperty("_threeStarTimeSeconds").intValue = 300;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = 3;
        serializedDefinition.FindProperty("_itemCatalog").objectReferenceValue = catalog.Levels[0].Definition.ItemCatalog;
        serializedDefinition.FindProperty("_minimumKeyDepth").intValue = 1;
        serializedDefinition.FindProperty("_minimumMovesToFirstKey").intValue = 1;
        SerializedProperty lockedShelves = serializedDefinition.FindProperty("_keyLockedShelfIndices");
        lockedShelves.arraySize = 1;
        lockedShelves.GetArrayElementAtIndex(0).intValue = PrototypeSafeIndex;
        (ItemType Type, int Count)[] groups =
        {
            (ItemType.Ball, 2), (ItemType.Bear, 2), (ItemType.Plant, 2), (ItemType.Lamp, 2),
            (ItemType.MapBall, 2), (ItemType.Beauty, 2), (ItemType.Toy, 1), (ItemType.TrainToy, 1)
        };
        SerializedProperty groupsProperty = serializedDefinition.FindProperty("_itemGroups");
        groupsProperty.arraySize = groups.Length;

        for (int index = 0; index < groups.Length; index++)
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
            throw new InvalidOperationException("Key lock scenarios need the level selection.");

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
            Debug.LogError("KEY_LOCK_PLAYMODE_FAIL");
        else
            Debug.Log("KEY_LOCK_PLAYMODE_PASS");
    }
}
