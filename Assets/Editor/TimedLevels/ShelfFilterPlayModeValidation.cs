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
public static class ShelfFilterPlayModeValidation
{
    private const string ActiveKey = "ShelfFilterPlayModeValidation.Active";
    private const string CompletedKey = "ShelfFilterPlayModeValidation.Completed";
    private const string FailedKey = "ShelfFilterPlayModeValidation.Failed";
    private const string RunInBackgroundKey = "ShelfFilterPlayModeValidation.RunInBackground";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string ScenePath = "Assets/Scenes/ThirdLevel.unity";
    private const string SceneName = "ThirdLevel";
    private const int ScenarioSeed = 600001;
    private const int ScenarioLevelNumber = 6;
    private const int MaximumWaitFrames = 3000;
    private const float ScenarioTimeScale = 3f;

    private static readonly BoardMoveSimulator Simulator = new BoardMoveSimulator();
    private static IEnumerator _scenarios;
    private static int _lastFrame = -1;
    private static string _unexpectedError;
    private static LevelEntry _filterLevel;
    private static LevelEntry _levelWithoutFilters;
    private static LevelSession _session;
    private static ShelfBoard _board;
    private static ShelfItemDragController _drag;
    private static MoveSuggestionProvider _suggestions;
    private static ShelfFilterIntroHint _introHint;
    private static ShelfFilterSignView[] _signs;

    static ShelfFilterPlayModeValidation()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Application.logMessageReceived -= HandleLogMessage;
        Application.logMessageReceived += HandleLogMessage;
    }

    [MenuItem("Tools/Timed Levels/Validate Filter Play Mode")]
    public static void Begin()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(CompletedKey, false);
        SessionState.SetBool(FailedKey, false);
        SessionState.SetBool(RunInBackgroundKey, Application.runInBackground);
        Application.runInBackground = true;
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
            ValidateIntroHint,
            ValidateForbiddenMove,
            ValidateSignDimming,
            ValidateRestart,
            ValidateLevelWithoutFilters,
            ValidateStuckFilteredTriple,
            ValidateMatchInBox,
            ValidateSuggestionIgnoresStuckTriple
        };

        foreach (Func<IEnumerable> scenario in scenarios)
        {
            int startFrame = Time.frameCount;

            foreach (object step in scenario())
                yield return step;

            Debug.Log($"SHELF_FILTER_SCENARIO_PASS: {scenario.Method.Name} in {Time.frameCount - startFrame} frames");
        }
    }

    private static IEnumerable ValidateSolutionReplay()
    {
        foreach (object step in LoadLevel(GetFilterLevel())) yield return step;

        TimedLevelVariant variant = _filterLevel.Definition.Variants[0];
        BoardStateSnapshot expected = _board.CreateSnapshot();
        Expect(BoardStateFingerprint.CreateHash(expected) == variant.LayoutHash, "P1: the runtime board must start from the variant layout");
        Expect(!_introHint.IsVisible, "P1: a level outside the catalog must not show the intro hint");

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            ExpectSuggestionRespectsFilters("P3");
            expected = Move(expected, move.Source, move.Target);

            foreach (object step in WaitUntilSettled("P1 solution move")) yield return step;

            ExpectBoard(expected, "P1 solution move");
        }

        Expect(_board.IsCleared, "P1: the solution must clear the board");
    }

    private static IEnumerable ValidateIntroHint()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        foreach (object step in LoadLevel(catalog.Levels.First(level => level.Definition.HasShelfFilters))) yield return step;

        Expect(_introHint.IsVisible, "the intro hint must be shown on the first catalog level with filters");
        Expect(_suggestions.TryGetSuggestion(out MoveSuggestion suggestion), "the first filter level must offer a move");
        BoardStateSnapshot expected = Move(_board.CreateSnapshot(), ToPosition(suggestion.SourceColumn), ToPosition(suggestion.TargetColumn));

        foreach (object step in WaitUntilSettled("intro hint move")) yield return step;

        ExpectBoard(expected, "intro hint move");
        Expect(!_introHint.IsVisible, "the intro hint must hide after the first move");
    }

    private static IEnumerable ValidateForbiddenMove()
    {
        foreach (object step in LoadLevel(GetFilterLevel())) yield return step;

        Shelf filter = _board.Shelves.First(_board.IsFiltered);
        ShelfColumnView emptyFilteredColumn = filter.ColumnViews.First(column => column.IsEmpty);
        ShelfColumnView source = _board.Shelves
            .Where(shelf => !_board.IsFiltered(shelf))
            .SelectMany(shelf => shelf.ColumnViews)
            .First(column => !column.IsEmpty && !_board.Accepts(filter, column.FrontItem.Type));
        string hashBefore = BoardStateFingerprint.CreateHash(_board.CreateSnapshot());
        MoveOutcome outcome = _session.TryStartMove(source, emptyFilteredColumn, out Action completePlacement);
        completePlacement?.Invoke();
        Expect(!outcome.IsSuccessful, "P2: a forbidden type must not enter a filtered empty column");

        foreach (object step in WaitUntilSettled("P2")) yield return step;

        Expect(BoardStateFingerprint.CreateHash(_board.CreateSnapshot()) == hashBefore, "P2: a rejected move must not change the board");
    }

    private static IEnumerable ValidateSignDimming()
    {
        foreach (object step in LoadLevel(GetFilterLevel())) yield return step;

        Shelf filter = _board.Shelves.First(_board.IsFiltered);

        foreach (bool isAccepted in new[] { false, true })
        {
            ShelfItem item = _board.Shelves
                .SelectMany(shelf => shelf.ColumnViews)
                .Where(column => !column.IsEmpty && _session.CanPickUp(column))
                .Select(column => column.FrontItem)
                .First(frontItem => _board.Accepts(filter, frontItem.Type) == isAccepted);
            Expect(_drag.TryBeginDrag(item, GetScreenCenter(item)), "P4: the drag must start");

            foreach (ShelfFilterSignView sign in _signs)
            {
                bool shouldDim = sign.IsShown && !_board.Accepts(sign.Shelf, item.Type);
                Expect(sign.IsDimmed == shouldDim, $"P4: the sign on {sign.Shelf.name} must {(shouldDim ? "" : "not ")}dim while dragging {item.Type}");
            }

            _drag.CancelDrag();
            Expect(_signs.All(sign => !sign.IsDimmed), "P4: no sign may stay dimmed after the drag");

            foreach (object step in WaitUntilSettled("P4 drag return")) yield return step;
        }
    }

    private static IEnumerable ValidateRestart()
    {
        foreach (object step in LoadLevel(GetFilterLevel())) yield return step;

        LevelSession previousSession = _session;
        previousSession.Restart();

        foreach (object step in WaitUntil(() => FindSession() != null && FindSession() != previousSession && FindSession().State == LevelState.Playing && FindSession().IsBoardSettled, "P5 restart")) yield return step;

        CaptureScene();
        ExpectFiltersMatchDefinition("P5");
    }

    private static IEnumerable ValidateLevelWithoutFilters()
    {
        foreach (object step in LoadLevel(GetLevelWithoutFilters())) yield return step;

        Expect(!_board.HasFilters, "P6: a level without filters must not filter shelves");
        Expect(_signs.All(sign => !sign.IsShown), "P6: all signs must stay hidden");
        Expect(!_introHint.IsVisible, "P6: the intro hint must stay hidden");
        Expect(_suggestions.TryGetSuggestion(out MoveSuggestion suggestion), "P6: the level must offer a move");
        BoardStateSnapshot expected = Move(_board.CreateSnapshot(), ToPosition(suggestion.SourceColumn), ToPosition(suggestion.TargetColumn));

        foreach (object step in WaitUntilSettled("P6 move")) yield return step;

        ExpectBoard(expected, "P6 move");
    }

    private static IEnumerable ValidateStuckFilteredTriple()
    {
        foreach (object step in LoadLevel(GetFilterLevel())) yield return step;

        ItemType type = GetFilteredType();
        Shelf shelf = _board.Shelves.First(candidate => !_board.IsFiltered(candidate) && candidate.Columns.All(column => !column.IsEmpty));
        ShelfColumnView source = StagePair(shelf, type, 2);
        ShelfFilterSignView sign = _signs.First(candidate => candidate.IsShown && _board.GetAcceptedTypes(candidate.Shelf).Contains(type));
        bool isReminded = false;

        void HandleBoardSettled() => isReminded = sign.IsPulsing && shelf.Columns.All(column => DOTween.IsTweening(column.FrontItem.transform));

        _session.BoardSettled += HandleBoardSettled;

        try
        {
            BoardStateSnapshot expected = Move(_board.CreateSnapshot(), ToPosition(source), ToPosition(shelf.ColumnViews[2]));

            foreach (object step in WaitUntilSettled("P7 move")) yield return step;

            ExpectBoard(expected, "P7 move");
        }
        finally
        {
            _session.BoardSettled -= HandleBoardSettled;
        }

        Expect(shelf.HasMatch() && !_board.CanMatch(shelf), $"P7: three {type} items must stay on the regular shelf {shelf.name}");
        Expect(isReminded, "P7: the stuck triple must shake its items and pulse the sign of its box");
    }

    private static IEnumerable ValidateMatchInBox()
    {
        foreach (object step in LoadLevel(GetFilterLevel())) yield return step;

        ItemType type = GetFilteredType();
        Shelf box = _board.Shelves.First(candidate => _board.GetAcceptedTypes(candidate).Contains(type));
        ShelfColumnView source = StagePair(box, type, 2);
        ScoreSystem score = UnityEngine.Object.FindObjectOfType<ScoreSystem>();
        ComboSystem combo = UnityEngine.Object.FindObjectOfType<ComboSystem>();
        Expect(score != null && combo != null, "P8: the level needs the score and combo systems");
        long scoreBefore = score.CurrentScore;
        int itemCountBefore = CountItems(type);
        int comboIncreaseCount = 0;

        void HandleComboIncreased(ComboState state) => comboIncreaseCount++;

        combo.ComboIncreased += HandleComboIncreased;

        try
        {
            BoardStateSnapshot expected = Move(_board.CreateSnapshot(), ToPosition(source), ToPosition(box.ColumnViews[2]));

            foreach (object step in WaitUntilSettled("P8 move")) yield return step;

            ExpectBoard(expected, "P8 move");
        }
        finally
        {
            combo.ComboIncreased -= HandleComboIncreased;
        }

        Expect(CountItems(type) == itemCountBefore - box.Capacity, $"P8: three {type} items must match in their box");
        Expect(score.CurrentScore > scoreBefore, "P8: a match in the box must add score");
        Expect(comboIncreaseCount > 0, "P8: a match in the box must increase the combo");
    }

    private static IEnumerable ValidateSuggestionIgnoresStuckTriple()
    {
        foreach (object step in LoadLevel(GetFilterLevel())) yield return step;

        ItemType type = GetFilteredType();
        Shelf shelf = _board.Shelves.First(candidate => !_board.IsFiltered(candidate) && candidate.Columns.Count(column => column.IsEmpty) == 1);
        int emptyColumnIndex = shelf.Columns.ToList().FindIndex(column => column.IsEmpty);
        StagePair(shelf, type, emptyColumnIndex);
        Expect(_suggestions.TryGetSuggestion(out MoveSuggestion suggestion), "P9: the staged board must offer a move");
        Expect(
            suggestion.SourceColumn.FrontItem.Type != type || suggestion.TargetColumn.Shelf != shelf || suggestion.TargetMatchingItems.Count == 0,
            $"P9: the suggestion must not collect {type} on the regular shelf {shelf.name}");
        ExpectSuggestionRespectsFilters("P9");
    }

    private static ItemType GetFilteredType() => _board.GetAcceptedTypes(_board.Shelves.First(_board.IsFiltered))[0];

    private static int CountItems(ItemType type) => _board.Shelves.SelectMany(shelf => shelf.Columns).SelectMany(column => column.Items).Count(item => item.Type == type);

    private static ShelfColumnView StagePair(Shelf shelf, ItemType type, int targetColumnIndex)
    {
        foreach (int columnIndex in Enumerable.Range(0, shelf.Capacity).OrderBy(index => index != targetColumnIndex))
        {
            ShelfColumn column = shelf.Columns[columnIndex];
            bool needsType = columnIndex != targetColumnIndex;

            if (column.IsEmpty ? !needsType : (column.FrontItem.Type == type) == needsType)
                continue;

            Expect(
                FindColumnsOutside(shelf, front => (front == type) == needsType).Any(donor => TryExchangeFront(column, donor.Column)),
                $"staging could not put {(needsType ? type.ToString() : "another type")} in front of column {columnIndex} on {shelf.name}");
        }

        foreach (Shelf boardShelf in _board.Shelves)
            boardShelf.View.Refresh();

        ShelfColumnView source = FindColumnsOutside(shelf, front => front == type).FirstOrDefault();
        Expect(source != null, $"staging found no {type} in front outside {shelf.name}");
        return source;
    }

    private static bool TryExchangeFront(ShelfColumn column, ShelfColumn donor)
    {
        bool isPlacement = column.IsEmpty;

        if (isPlacement)
            column.PlaceFront(donor.TakeFront());
        else
            column.SwapFrontWith(donor);

        if (_board.Shelves.All(shelf => !shelf.HasMatch()))
            return true;

        if (isPlacement)
            donor.PlaceFront(column.TakeFront());
        else
            column.SwapFrontWith(donor);

        return false;
    }

    private static IEnumerable<ShelfColumnView> FindColumnsOutside(Shelf shelf, Func<ItemType, bool> isFrontWanted)
    {
        return _board.Shelves
            .Where(candidate => candidate != shelf)
            .OrderBy(candidate => _board.IsFiltered(candidate))
            .SelectMany(candidate => candidate.ColumnViews)
            .Where(candidate => !candidate.IsEmpty && isFrontWanted(candidate.FrontItem.Type))
            .ToArray();
    }

    private static LevelEntry GetFilterLevel()
    {
        if (_filterLevel == null)
            _filterLevel = CreateScenarioLevel(ShelfFilterGenerationPreview.CreatePreviewDefinition(), "ShelfFilterScenarioLevel");

        return _filterLevel;
    }

    private static LevelEntry GetLevelWithoutFilters()
    {
        if (_levelWithoutFilters != null)
            return _levelWithoutFilters;

        TimedLevelDefinition definition = ShelfFilterGenerationPreview.CreatePreviewDefinition();
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_shelfFilters").arraySize = 0;
        serializedDefinition.FindProperty("_filteredEmptyColumnCount").intValue = 0;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        _levelWithoutFilters = CreateScenarioLevel(definition, "ShelfFilterScenarioLevelWithoutFilters");
        return _levelWithoutFilters;
    }

    private static LevelEntry CreateScenarioLevel(TimedLevelDefinition definition, string levelName)
    {
        TimedLevelGenerationResult generation = new TimedLevelLayoutGenerator().GenerateWithSolution(definition, ShelfFilterGenerationPreview.CreateBoardShape(), ScenarioSeed);
        TimedLevelVariantGenerator.WriteVariants(definition, new[]
        {
            new TimedLevelVariant(
                ScenarioSeed,
                TimedLevelLayoutRules.GeneratorVersion,
                generation.SolutionMoves.Count,
                BoardStateFingerprint.CreateHash(generation.State),
                generation.State,
                generation.SolutionMoves)
        });
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

    private static IEnumerable LoadLevel(LevelEntry level)
    {
        LevelSelectionState selection = AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath);

        if (selection == null || level == null)
            throw new InvalidOperationException("Filter scenarios need the level selection and the scenario level.");

        selection.Select(level);
        SceneManager.LoadScene(SceneName);
        yield return null;

        foreach (object step in WaitUntil(() => FindSession() != null && FindSession().CurrentLevel == level && FindSession().State == LevelState.Playing && FindSession().IsBoardSettled, "level load")) yield return step;

        CaptureScene();
        Time.timeScale = ScenarioTimeScale;

        if (level == _filterLevel)
            ExpectFiltersMatchDefinition("level load");
    }

    private static void CaptureScene()
    {
        _session = FindSession();
        _board = UnityEngine.Object.FindObjectOfType<ShelfBoard>();
        _drag = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();
        _suggestions = UnityEngine.Object.FindObjectOfType<MoveSuggestionProvider>();
        _introHint = UnityEngine.Object.FindObjectOfType<ShelfFilterIntroHint>();
        _signs = UnityEngine.Object.FindObjectsOfType<ShelfFilterSignView>(true);

        if (_board == null || _drag == null || _suggestions == null || _introHint == null || _signs.Length != _board.Shelves.Count)
            throw new InvalidOperationException("Filter scenarios need the ThirdLevel board, drag controller, move suggestions, intro hint and one sign per shelf.");
    }

    private static void ExpectFiltersMatchDefinition(string scenario)
    {
        IReadOnlyList<ShelfFilterDefinition> filters = _session.CurrentLevel.Definition.ShelfFilters;
        Expect(_board.Shelves.Count(_board.IsFiltered) == filters.Count, $"{scenario}: every filter of the definition must be on the board");

        foreach (ShelfFilterDefinition filter in filters)
        {
            Shelf shelf = _board.Shelves[filter.ShelfIndex];
            Expect(_board.GetAcceptedTypes(shelf).SequenceEqual(filter.AcceptedTypes.Distinct().OrderBy(type => type)), $"{scenario}: shelf {filter.ShelfIndex} must accept the defined types");
        }

        foreach (ShelfFilterSignView sign in _signs)
            Expect(sign.IsShown == _board.IsFiltered(sign.Shelf), $"{scenario}: the sign on {sign.Shelf.name} must be shown only on a filter");
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
    }

    private static IEnumerable WaitUntilSettled(string description)
    {
        return WaitUntil(() =>
        {
            ExpectSuggestionRespectsFilters($"P3 while waiting for {description}");
            return _session.IsBoardSettled;
        }, description);
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

    private static ColumnPosition ToPosition(ShelfColumnView column)
    {
        int shelfIndex = _board.Shelves.ToList().IndexOf(column.Shelf);
        return new ColumnPosition(shelfIndex, column.Shelf.ColumnViews.ToList().IndexOf(column));
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
            throw new InvalidOperationException($"Shelf filter Play Mode: {message}.");
    }

    private static LevelSession FindSession() => UnityEngine.Object.FindObjectOfType<LevelSession>();

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
        _filterLevel = null;
        _levelWithoutFilters = null;
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
            Debug.LogError("SHELF_FILTER_PLAYMODE_FAIL");
        else
            Debug.Log("SHELF_FILTER_PLAYMODE_PASS");
    }
}
