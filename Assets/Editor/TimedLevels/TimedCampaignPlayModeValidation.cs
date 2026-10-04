using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TimedCampaignPlayModeValidation
{
    private const string ActiveKey = "TimedCampaignPlayModeValidation.Active";
    private const string FailedKey = "TimedCampaignPlayModeValidation.Failed";
    private const string CompletedKey = "TimedCampaignPlayModeValidation.Completed";
    private const string RunInBackgroundKey = "TimedCampaignPlayModeValidation.RunInBackground";
    private const string LevelIndexKey = "TimedCampaignPlayModeValidation.LevelIndex";
    private const string ClosedShelfLossCheckedKey = "TimedCampaignPlayModeValidation.ClosedShelfLossChecked";
    private const int ClosedShelfLossLevelNumber = 13;
    private const int MaximumClosedShelfMoves = 800;
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string OutputDirectory = "C:/Users/Михаил/DruggingStuff/.utmp/timed-campaign-playmode";

    private static LevelSession _session;
    private static ShelfBoard _board;
    private static IReadOnlyList<TimedLevelMove> _moves;
    private static int _moveIndex;
    private static int _settledFrameCount;
    private static int _resultFrameCount;
    private static int _swapPhase;
    private static ColumnPosition _swapSource;
    private static ColumnPosition _swapTarget;
    private static string _initialLayoutHash;
    private static bool _observedInitialHint;
    private static ShelfSwapHoverView _swapHoverView;
    private static bool _isLossRun;
    private static BoardStateSnapshot _expectedState;
    private static MoveSuggestionProvider _suggestionProvider;
    private static ConveyorController _conveyorController;
    private static int _pendingBoardChangeFrameCount;
    private static int _shiftCount;
    private static int _shiftStartItemCount;
    private static string _shiftFailure;
    private static bool _isFramingValidated;
    private static string _unexpectedError;
    private static readonly BoardMoveSimulator Simulator = new BoardMoveSimulator();
    private static readonly System.Random Random = new System.Random(20260926);
    private static readonly float[] FramingAspects = { 21f / 9f, 16f / 9f, 16f / 10f, 3f / 2f, 4f / 3f, 1f, 3f / 4f, 9f / 16f };

    static TimedCampaignPlayModeValidation()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.logMessageReceived -= HandleLogMessage;
        Application.logMessageReceived += HandleLogMessage;
    }

    [MenuItem("Tools/Timed Levels/Validate Play Mode Route")]
    public static void Begin()
    {
        System.IO.Directory.CreateDirectory(OutputDirectory);
        LevelCatalog catalog = LoadCatalog();
        LevelSelectionState selection = LoadSelection();
        selection.Select(catalog.Levels[0]);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(FailedKey, false);
        SessionState.SetBool(CompletedKey, false);
        SessionState.SetBool(RunInBackgroundKey, Application.runInBackground);
        SessionState.SetInt(LevelIndexKey, 0);
        SessionState.SetBool(ClosedShelfLossCheckedKey, false);
        _unexpectedError = null;
        Application.runInBackground = true;
        EditorSceneManager.OpenScene(FindScenePath(catalog.Levels[0].SceneName), OpenSceneMode.Single);
        DisablePersistenceComponents();
        EditorApplication.isPlaying = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (SessionState.GetBool(ActiveKey, false))
            DisablePersistenceComponents();
    }

    private static void DisablePersistenceComponents()
    {
        foreach (LevelResultRecorder recorder in UnityEngine.Object.FindObjectsOfType<LevelResultRecorder>(true))
            recorder.enabled = false;

        foreach (YG.Insides.YGSendMessage initializer in UnityEngine.Object.FindObjectsOfType<YG.Insides.YGSendMessage>(true))
            initializer.enabled = false;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            FinishEditorRun();
            return;
        }

        EditorApplication.QueuePlayerLoopUpdate();

        try
        {
            RunPlayModeStep();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(FailedKey, true);
            EditorApplication.isPlaying = false;
        }
    }

    private static void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (SessionState.GetBool(ActiveKey, false) && EditorApplication.isPlaying && (type == LogType.Exception || type == LogType.Error) && _unexpectedError == null)
            _unexpectedError = condition;
    }

    private static void RunPlayModeStep()
    {
        if (_unexpectedError != null)
            throw new InvalidOperationException($"Unexpected error during the route: {_unexpectedError}");

        if (_session == null)
        {
            TryInitializeLevel();
            return;
        }

        if (_session.Result != null)
        {
            if (_isLossRun)
                HandleClosedShelfLoss();
            else
                HandleResult();

            return;
        }

        HintPresenter hintPresenter = UnityEngine.Object.FindObjectOfType<HintPresenter>();

        if (hintPresenter != null && hintPresenter.IsPlaying)
        {
            if (_session.CurrentLevel.Number == 5)
                _observedInitialHint = true;

            return;
        }

        ValidatePendingBoardChanges();

        if (!_session.IsBoardSettled)
            return;

        if (!_isFramingValidated)
        {
            ValidateCameraFraming();
            _isFramingValidated = true;
        }

        _settledFrameCount++;

        if (_settledFrameCount < 2)
            return;

        _settledFrameCount = 0;

        if (_session.CurrentLevel.Definition.HasClosedShelves)
        {
            PlayClosedShelfMove();
            return;
        }

        if (_session.CurrentLevel.Number == 1 && !ValidateSwapRoundTrip())
            return;

        if (_session.CurrentLevel.Number == 5)
        {
            if (!_observedInitialHint)
                throw new InvalidOperationException("Level 5: automatic initial hint was not shown.");

            Time.timeScale = 6f;
        }

        if (_moveIndex >= _moves.Count)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: solution ended before the run result.");

        TimedLevelMove move = _moves[_moveIndex];

        if (BoardStateFingerprint.CreateHash(_board.CreateSnapshot()) != BoardStateFingerprint.CreateHash(_expectedState))
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}, move {_moveIndex + 1}: runtime board differs from the simulated state.");

        if (!Simulator.TrySimulate(_expectedState, move.Source, move.Target, out BoardMoveSimulation expectedMove))
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}, move {_moveIndex + 1}: the simulator rejects the stored move.");

        _expectedState = expectedMove.State;
        ShelfColumnView source = _board.Shelves[move.Source.ShelfIndex].ColumnViews[move.Source.ColumnIndex];
        ShelfColumnView target = _board.Shelves[move.Target.ShelfIndex].ColumnViews[move.Target.ColumnIndex];
        MoveOutcome outcome = _session.TryStartMove(source, target, out Action completePlacement);

        if (!outcome.IsSuccessful)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}, move {_moveIndex + 1}: rejected.");

        completePlacement?.Invoke();
        _moveIndex++;

        if (_moveIndex == 1 && !_session.Timer.HasStarted)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: timer did not start after the first move.");
    }

    private static void TryInitializeLevel()
    {
        Application.runInBackground = true;
        _session = UnityEngine.Object.FindObjectOfType<LevelSession>();

        if (_session == null || _session.State != LevelState.Playing || _session.CurrentLevel == null)
        {
            _session = null;
            return;
        }

        _board = UnityEngine.Object.FindObjectOfType<ShelfBoard>();

        if (_board == null)
            throw new InvalidOperationException(nameof(ShelfBoard));

        BoardStateSnapshot runtimeState = _board.CreateSnapshot();
        TimedLevelVariant variant = _session.CurrentLevel.Definition.Variants[_session.CurrentVariantIndex];

        if (BoardStateFingerprint.CreateHash(runtimeState) != variant.LayoutHash)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: runtime layout differs from the generated layout.");

        if (_session.Timer.HasStarted || Math.Abs(_session.Timer.RemainingTime - _session.CurrentLevel.Definition.TimeLimitSeconds) > 0.01f)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: timer started before a move.");

        _moves = variant.CreateSolution();
        _expectedState = runtimeState;
        _suggestionProvider = UnityEngine.Object.FindObjectOfType<MoveSuggestionProvider>();
        BeginConveyorObservation();
        _isFramingValidated = false;
        _moveIndex = 0;
        _settledFrameCount = 0;
        _resultFrameCount = 0;
        _swapPhase = _session.CurrentLevel.Number == 1 ? 0 : 3;
        _initialLayoutHash = variant.LayoutHash;
        _observedInitialHint = false;
        _swapHoverView = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>()?.GetComponent<ShelfSwapHoverView>();
        Time.timeScale = _session.CurrentLevel.Number == 5 ? 1f : 6f;

        if (_session.CurrentLevel.Number == 1)
            BeginSwapHoverValidation();

        if (_session.CurrentLevel.Definition.HasClosedShelves)
            BeginClosedShelfLevel();

        if (_session.CurrentLevel.Number == 1)
            ScreenCapture.CaptureScreenshot($"{OutputDirectory}/level-01-gameplay.png", 1);
    }

    private static void BeginClosedShelfLevel()
    {
        int coverCount = UnityEngine.Object.FindObjectsOfType<ClosedShelfCoverView>().Length;

        if (coverCount != _session.CurrentLevel.Definition.ClosedShelves.Count)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: expected {_session.CurrentLevel.Definition.ClosedShelves.Count} covers, found {coverCount}.");

        GameObject introBubble = GameObject.Find("ClosedShelfIntroBubble");
        bool expectsIntro = ClosedShelfIntroHint.GetNewConditionKinds(LoadCatalog(), _session.CurrentLevel).Count > 0;

        if ((introBubble != null) != expectsIntro)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: intro hint visibility is wrong.");

        _isLossRun = _session.CurrentLevel.Number == ClosedShelfLossLevelNumber && !SessionState.GetBool(ClosedShelfLossCheckedKey, false);
    }

    private static void PlayClosedShelfMove()
    {
        if (_moveIndex >= MaximumClosedShelfMoves)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: not finished after {MaximumClosedShelfMoves} moves.");

        if (_isLossRun && _moveIndex > 0)
            return;

        if (!TryChooseClosedShelfMove(out ShelfColumnView source, out ShelfColumnView target))
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: no legal move with the level still running.");

        MoveOutcome outcome = _session.TryStartMove(source, target, out Action completePlacement);

        if (!outcome.IsSuccessful)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}, move {_moveIndex + 1}: rejected.");

        completePlacement?.Invoke();
        _moveIndex++;

        if (_moveIndex > 1)
            return;

        if (!_session.Timer.HasStarted)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: timer did not start after the first move.");

        if (_isLossRun)
            Time.timeScale = 20f;
        else
            _session.Timer.Pause();
    }

    private static bool TryChooseClosedShelfMove(out ShelfColumnView bestSource, out ShelfColumnView bestTarget)
    {
        HashSet<int> targetShelves = new HashSet<int>(_session.CurrentLevel.Definition.ClosedShelves
            .Where(shelf => shelf.Condition == ShelfUnlockConditionKind.MatchesOnShelf && _board.IsShelfClosed(_board.Shelves[shelf.ShelfIndex]))
            .Select(shelf => shelf.TargetShelfIndex));
        int currentScore = ScoreFronts(_board.CreateSnapshot(), targetShelves);
        List<ShelfColumnView> columns = _board.Shelves.SelectMany(shelf => shelf.ColumnViews).ToList();
        List<(ShelfColumnView Source, ShelfColumnView Target)> allowed = new List<(ShelfColumnView, ShelfColumnView)>();
        int bestScore = int.MinValue;
        bestSource = null;
        bestTarget = null;

        foreach (ShelfColumnView source in columns)
        {
            if (!_session.CanPickUp(source))
                continue;

            foreach (ShelfColumnView target in columns)
            {
                if (source == target || !_board.TrySimulateMove(source.Column, target.Column, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
                    continue;

                allowed.Add((source, target));
                int score = ScoreFronts(simulation.State, targetShelves) - currentScore;

                foreach (MatchInfo match in simulation.Matches)
                    score += targetShelves.Contains(match.ShelfIndex) ? 2000 : 1000;

                score = score * 8 + Random.Next(8);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSource = source;
                    bestTarget = target;
                }
            }
        }

        if (allowed.Count == 0)
            return false;

        if (bestScore < 8)
            (bestSource, bestTarget) = allowed[Random.Next(allowed.Count)];

        return true;
    }

    private static int ScoreFronts(BoardStateSnapshot state, HashSet<int> targetShelves)
    {
        int score = 0;

        for (int shelfIndex = 0; shelfIndex < state.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = state.Shelves[shelfIndex];

            if (!shelf.IsOpen)
                continue;

            int sameFronts = shelf.Columns
                .Where(column => column.FrontItem.HasValue)
                .GroupBy(column => column.FrontItem.Value)
                .Select(group => group.Count())
                .DefaultIfEmpty(0)
                .Max();

            score += sameFronts * sameFronts * (targetShelves.Contains(shelfIndex) ? 3 : 1);
        }

        return score;
    }

    private static void HandleClosedShelfLoss()
    {
        if (_session.Result.Won || _session.State != LevelState.Lost || !_board.HasClosedShelves)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: timeout did not end as a loss with closed shelves.");

        _resultFrameCount++;

        if (_resultFrameCount < 8)
            return;

        SessionState.SetBool(ClosedShelfLossCheckedKey, true);
        _isLossRun = false;
        Time.timeScale = 1f;
        _session.Restart();
        _session = null;
        _board = null;
        _moves = null;
    }

    private static void BeginSwapHoverValidation()
    {
        if (_swapHoverView == null || !TryFindReversibleSwap(out _swapSource, out _swapTarget))
            throw new InvalidOperationException("Level 1: reversible swap target is missing.");

        ShelfColumnView target = _board.Shelves[_swapTarget.ShelfIndex].ColumnViews[_swapTarget.ColumnIndex];
        ShelfItem item = target.FrontItem;
        Quaternion initialRotation = item.transform.localRotation;
        _swapHoverView.Show(item);

        if (!_swapHoverView.IsShowing)
            throw new InvalidOperationException("Level 1: swap hover animation did not start.");

        _swapHoverView.Stop();

        if (_swapHoverView.IsShowing || Quaternion.Angle(item.transform.localRotation, initialRotation) > 0.01f)
            throw new InvalidOperationException("Level 1: swap hover animation did not restore the item rotation.");
    }

    private static bool ValidateSwapRoundTrip()
    {
        if (_swapPhase == 0)
        {
            StartSwap(_swapSource, _swapTarget);
            _swapPhase = 1;

            if (!_session.Timer.HasStarted)
                throw new InvalidOperationException("Level 1: timer did not start after a swap.");

            return false;
        }

        if (_swapPhase == 1)
        {
            StartSwap(_swapSource, _swapTarget);
            _swapPhase = 2;
            return false;
        }

        if (_swapPhase == 2)
        {
            if (BoardStateFingerprint.CreateHash(_board.CreateSnapshot()) != _initialLayoutHash)
                throw new InvalidOperationException("Level 1: reverse swap did not restore the board.");

            _swapPhase = 3;
        }

        return true;
    }

    private static bool TryFindReversibleSwap(out ColumnPosition source, out ColumnPosition target)
    {
        BoardStateSnapshot state = _board.CreateSnapshot();
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        for (int sourceShelfIndex = 0; sourceShelfIndex < state.Shelves.Count; sourceShelfIndex++)
        {
            for (int sourceColumnIndex = 0; sourceColumnIndex < state.Shelves[sourceShelfIndex].Capacity; sourceColumnIndex++)
            {
                ColumnPosition candidateSource = new ColumnPosition(sourceShelfIndex, sourceColumnIndex);

                for (int targetShelfIndex = 0; targetShelfIndex < state.Shelves.Count; targetShelfIndex++)
                {
                    for (int targetColumnIndex = 0; targetColumnIndex < state.Shelves[targetShelfIndex].Capacity; targetColumnIndex++)
                    {
                        ColumnPosition candidateTarget = new ColumnPosition(targetShelfIndex, targetColumnIndex);

                        if (!simulator.TrySimulate(state, candidateSource, candidateTarget, out BoardMoveSimulation first)
                            || !first.IsSwap
                            || first.MatchCount != 0
                            || !simulator.TrySimulate(first.State, candidateSource, candidateTarget, out BoardMoveSimulation second)
                            || !second.IsSwap
                            || second.MatchCount != 0)
                        {
                            continue;
                        }

                        source = candidateSource;
                        target = candidateTarget;
                        return true;
                    }
                }
            }
        }

        source = default;
        target = default;
        return false;
    }

    private static void StartSwap(ColumnPosition sourcePosition, ColumnPosition targetPosition)
    {
        ShelfColumnView source = _board.Shelves[sourcePosition.ShelfIndex].ColumnViews[sourcePosition.ColumnIndex];
        ShelfColumnView target = _board.Shelves[targetPosition.ShelfIndex].ColumnViews[targetPosition.ColumnIndex];
        MoveOutcome outcome = _session.TryStartMove(source, target, out Action completePlacement);

        if (!outcome.IsSuccessful || !outcome.IsSwap || outcome.DisplacedItem == null)
            throw new InvalidOperationException("Level 1: runtime swap was rejected.");

        completePlacement?.Invoke();
    }

    private static void ValidateCameraFraming()
    {
        BoardCameraFramer framer = UnityEngine.Object.FindObjectOfType<BoardCameraFramer>();

        if (framer == null)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: {nameof(BoardCameraFramer)} is missing.");

        framer.CaptureLayout();
        // Items resting in columns, the same set the framer measures.
        Bounds[] items = _board.Shelves
            .SelectMany(shelf => shelf.ColumnViews)
            .SelectMany(column => column.ItemAnchor.GetComponentsInChildren<Renderer>())
            .Select(renderer => renderer.bounds)
            .ToArray();

        List<string> fieldsOfView = new List<string>();

        foreach (float aspect in FramingAspects)
        {
            float viewAspect = framer.ClampAspect(aspect);
            float fieldOfView = framer.ComputeFieldOfView(framer.DesignFieldOfView, viewAspect);
            fieldsOfView.Add($"{aspect:F2}={fieldOfView:F1}");

            if (!framer.FitsBounds(items, fieldOfView, viewAspect))
                throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: items do not fit at aspect {aspect:F2} (fov {fieldOfView:F1}).");

            int emptyPixels = CountEmptyPixels(Camera.main, viewAspect, fieldOfView);

            if (emptyPixels > 0)
                throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: {emptyPixels} pixels beyond the set are visible at aspect {aspect:F2} (fov {fieldOfView:F1}).");
        }

        Debug.Log($"CAMERA_FRAMING level {_session.CurrentLevel.Number}: design {framer.DesignFieldOfView:F1}, {string.Join(", ", fieldsOfView)}");
    }

    // Renders the view over a magenta background; any magenta left means the camera sees past the edges of the set.
    private static int CountEmptyPixels(Camera camera, float aspect, float fieldOfView)
    {
        const int Height = 270;
        CameraClearFlags clearFlags = camera.clearFlags;
        Color backgroundColor = camera.backgroundColor;
        float originalFieldOfView = camera.fieldOfView;
        Rect rect = camera.rect;
        RenderTexture target = new RenderTexture(Mathf.RoundToInt(Height * aspect), Height, 24);
        Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);

        try
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = target;
            camera.aspect = aspect;
            camera.fieldOfView = fieldOfView;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            return pixels.GetPixels32().Count(pixel => pixel.r > 230 && pixel.g < 25 && pixel.b > 230);
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = null;
            camera.rect = rect;
            camera.ResetAspect();
            camera.fieldOfView = originalFieldOfView;
            camera.clearFlags = clearFlags;
            camera.backgroundColor = backgroundColor;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    private static void BeginConveyorObservation()
    {
        _pendingBoardChangeFrameCount = 0;
        _shiftCount = 0;
        _shiftFailure = null;
        _conveyorController = UnityEngine.Object.FindObjectOfType<ConveyorController>();

        if (_conveyorController == null)
            return;

        _conveyorController.ShiftStarted += HandleShiftStarted;
        _conveyorController.ShiftCompleted += HandleShiftCompleted;
    }

    private static void HandleShiftStarted()
    {
        _shiftCount++;
        _shiftStartItemCount = _board.CreateSnapshot().ItemCount;
    }

    private static void HandleShiftCompleted()
    {
        if (_board.CreateSnapshot().ItemCount != _shiftStartItemCount)
            _shiftFailure = $"Level {_session.CurrentLevel.Number}: a conveyor shift changed the item count.";
    }

    private static void ValidatePendingBoardChanges()
    {
        if (_shiftFailure != null)
            throw new InvalidOperationException(_shiftFailure);

        if (!_session.HasPendingBoardChanges)
            return;

        _pendingBoardChangeFrameCount++;

        if (!_board.HasLockedShelves)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: a pending board change left every shelf unlocked.");

        if (_suggestionProvider != null && _suggestionProvider.TryGetSuggestion(out _))
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: a hint was offered while a board change was pending.");
    }

    private static void ValidateConveyorObservation()
    {
        if (!_session.CurrentLevel.Definition.HasConveyors)
            return;

        if (_shiftCount == 0 || _pendingBoardChangeFrameCount == 0)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: no conveyor shift was observed.");

        _conveyorController.ShiftStarted -= HandleShiftStarted;
        _conveyorController.ShiftCompleted -= HandleShiftCompleted;
    }

    private static void HandleResult()
    {
        if (!_session.Result.Won || _session.State != LevelState.Won || !_board.IsCleared)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: did not finish as a cleared victory.");

        if (_resultFrameCount == 0)
            ValidateConveyorObservation();

        if (UnityEngine.Object.FindObjectsOfType<ClosedShelfCoverView>().Length != 0)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: won with closed shelf covers still present.");

        if (_session.Result.Seed != _session.CurrentSeed || _session.Result.VariantIndex != _session.CurrentVariantIndex)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: result variant metadata differs.");

        _resultFrameCount++;

        if (_resultFrameCount == 2 && _session.CurrentLevel.Number == 1)
            ScreenCapture.CaptureScreenshot($"{OutputDirectory}/level-01-result.png", 1);

        if (_resultFrameCount < 8)
            return;

        int levelIndex = SessionState.GetInt(LevelIndexKey, 0);
        LevelCatalog catalog = LoadCatalog();

        if (levelIndex >= catalog.Levels.Count - 1)
        {
            Time.timeScale = 1f;
            SessionState.SetBool(CompletedKey, true);
            EditorApplication.isPlaying = false;
            return;
        }

        int nextIndex = levelIndex + 1;
        LevelEntry nextLevel = catalog.Levels[nextIndex];
        LoadSelection().Select(nextLevel);
        SessionState.SetInt(LevelIndexKey, nextIndex);
        _session = null;
        _board = null;
        _moves = null;
        SceneManager.LoadScene(nextLevel.SceneName);
    }

    private static void FinishEditorRun()
    {
        bool failed = SessionState.GetBool(FailedKey, false) || !SessionState.GetBool(CompletedKey, false);
        SessionState.SetBool(ActiveKey, false);
        SessionState.EraseBool(FailedKey);
        SessionState.EraseBool(CompletedKey);
        SessionState.EraseInt(LevelIndexKey);
        SessionState.EraseBool(ClosedShelfLossCheckedKey);
        Application.runInBackground = SessionState.GetBool(RunInBackgroundKey, false);
        SessionState.EraseBool(RunInBackgroundKey);
        _unexpectedError = null;

        if (failed)
        {
            Debug.LogError("TIMED_CAMPAIGN_PLAYMODE_FAIL");

            if (Application.isBatchMode)
                EditorApplication.Exit(1);

            return;
        }

        Debug.Log("TIMED_CAMPAIGN_PLAYMODE_PASS");

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    private static LevelCatalog LoadCatalog()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        return catalog != null ? catalog : throw new InvalidOperationException(CatalogPath);
    }

    private static LevelSelectionState LoadSelection()
    {
        LevelSelectionState selection = AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath);
        return selection != null ? selection : throw new InvalidOperationException(SelectionPath);
    }

    private static string FindScenePath(string sceneName)
    {
        string path = AssetDatabase.FindAssets($"{sceneName} t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(candidate => string.Equals(System.IO.Path.GetFileNameWithoutExtension(candidate), sceneName, StringComparison.Ordinal));
        return !string.IsNullOrEmpty(path) ? path : throw new InvalidOperationException(sceneName);
    }
}
