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

    static TimedCampaignPlayModeValidation()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
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

    private static void RunPlayModeStep()
    {
        if (_session == null)
        {
            TryInitializeLevel();
            return;
        }

        if (_session.Result != null)
        {
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

        if (!_session.IsBoardSettled)
            return;

        _settledFrameCount++;

        if (_settledFrameCount < 2)
            return;

        _settledFrameCount = 0;

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

        if (_session.CurrentLevel.Number == 1)
            ScreenCapture.CaptureScreenshot($"{OutputDirectory}/level-01-gameplay.png", 1);
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

    private static void HandleResult()
    {
        if (!_session.Result.Won || _session.State != LevelState.Won || !_board.IsCleared)
            throw new InvalidOperationException($"Level {_session.CurrentLevel.Number}: did not finish as a cleared victory.");

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
        Application.runInBackground = SessionState.GetBool(RunInBackgroundKey, false);
        SessionState.EraseBool(RunInBackgroundKey);

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
