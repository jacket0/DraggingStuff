using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CyclicShelfSwitchRecorder
{
    private const string ActiveKey = "CyclicShelfSwitchRecorder.Active";
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/SeventhLevel.unity";
    private const string SceneName = "SeventhLevel";
    private const string OutputFolderName = "LocalOutput/cyclic-shelves/video";
    private const string TakeName = "cyclic-shelf-switch";
    private const int OutputWidth = 1280;
    private const int OutputHeight = 720;
    private const float FrameRate = 30f;
    private const int MaximumWaitFrames = 3000;
    private const int DragFrameCount = 12;
    private const int MomentDelayFrames = 12;
    private const int ResolutionFrameCount = 6;
    private const int ToastDelayFrames = 20;
    private const int TailFrameCount = 45;

    private static readonly (int Width, int Height)[] ScreenshotSizes = { (1280, 720), (1600, 900), (1920, 1080) };

    private static readonly HashSet<string> CapturedMoments = new HashSet<string>();
    private static IEnumerator _steps;
    private static int _lastFrame = -1;
    private static RecorderController _recorder;

    private static string OutputFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolderName);
    private static string RecordingFolder => Path.Combine(Path.GetTempPath(), "CyclicShelfRecording");

    static CyclicShelfSwitchRecorder()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
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

    [MenuItem("Tools/Timed Levels/Record Cyclic Shelf Switch")]
    public static void Begin()
    {
        Select(GetChapterStartLevel());
        SessionState.SetBool(ActiveKey, true);
        CapturedMoments.Clear();
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
            {
                SessionState.SetBool(ActiveKey, false);
                string outputPath = Path.Combine(OutputFolder, TakeName + ".mp4");
                Directory.CreateDirectory(OutputFolder);
                File.Copy(Path.Combine(RecordingFolder, TakeName + ".mp4"), outputPath, true);
                Debug.Log($"CYCLIC_SHELF_RECORDING_DONE: {outputPath}");
            }

            return;
        }

        EditorApplication.QueuePlayerLoopUpdate();

        if (Time.frameCount == _lastFrame)
            return;

        _lastFrame = Time.frameCount;

        try
        {
            _steps ??= Record().GetEnumerator();

            if (!_steps.MoveNext())
                Stop();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Stop();
        }
    }

    private static IEnumerable Record()
    {
        LevelEntry level = GetChapterStartLevel();

        foreach (object step in WaitForLevel(level)) yield return step;

        foreach (object step in CaptureResolutions("1-intro-hint")) yield return step;

        StartRecording();

        foreach (object step in RecordSwitches()) yield return step;

        foreach (object step in RecordRescue(false, "7-toast-early-switch")) yield return step;

        foreach (object step in RecordRescue(true, "8-toast-all-open")) yield return step;
    }

    private static IEnumerable RecordSwitches()
    {
        LevelSession session = UnityEngine.Object.FindObjectOfType<LevelSession>();
        ShelfBoard board = UnityEngine.Object.FindObjectOfType<ShelfBoard>();
        CyclicShelvesController cyclicShelves = UnityEngine.Object.FindObjectOfType<CyclicShelvesController>();
        CyclicShelfIntroHint hint = UnityEngine.Object.FindObjectOfType<CyclicShelfIntroHint>();
        ShelfShutterView[] shutters = UnityEngine.Object.FindObjectsOfType<ShelfShutterView>();
        SplitFlapDisplay[] displays = UnityEngine.Object.FindObjectsOfType<SplitFlapDisplay>();
        string hintText = null;
        int hintStep = 0;
        int hintFrame = -1;
        int warningFrame = -1;

        TimedLevelVariant variant = session.CurrentLevel.Definition.Variants[session.CurrentVariantIndex];

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            foreach (object step in Drag(board, move)) yield return step;

            for (int frame = 0; !(session.IsBoardSettled && !cyclicShelves.IsSwitching) || frame < MomentDelayFrames; frame++)
            {
                if (frame >= MaximumWaitFrames)
                    throw new InvalidOperationException("The recording timed out.");

                if (hint.IsVisible && hint.Text != hintText)
                {
                    hintText = hint.Text;
                    hintStep++;
                    hintFrame = Time.frameCount;
                }

                if (hintFrame >= 0 && Time.frameCount - hintFrame == MomentDelayFrames)
                    CaptureMoment($"1-intro-hint-{hintStep}");

                if (displays.Any(display => display.IsFlipping))
                    CaptureMoment("2-flip");

                if (warningFrame < 0 && shutters.Any(shutter => !shutter.IsSwitching && !shutter.IsClosed && shutter.ClosedFraction > 0.05f))
                    warningFrame = Time.frameCount;

                if (warningFrame >= 0 && Time.frameCount - warningFrame == MomentDelayFrames)
                    CaptureMoment("3-warning");

                if (shutters.Any(shutter => shutter.IsSwitching && shutter.IsClosed && shutter.ClosedFraction > 0.4f && shutter.ClosedFraction < 0.8f))
                    CaptureMoment("4-closing");

                if (shutters.Any(shutter => shutter.IsSwitching && !shutter.IsClosed && shutter.ClosedFraction > 0.3f && shutter.ClosedFraction < 0.7f))
                    CaptureMoment("5-opening");

                yield return null;
            }

            if (CapturedMoments.Contains("4-closing") && CapturedMoments.Contains("5-opening"))
                break;
        }

        CaptureMoment("6-after-switch");

        for (int frame = 0; frame < TailFrameCount; frame++)
            yield return null;
    }

    private static IEnumerable RecordRescue(bool isRepeatedDeadlock, string momentName)
    {
        LevelEntry level = CyclicShelfPlayModeValidation.GetScenarioLevel(CyclicShelfPlayModeValidation.SwingLevelName);
        Select(level);
        SceneManager.LoadScene(SceneName);
        yield return null;

        foreach (object step in WaitForLevel(level)) yield return step;

        CyclicShelfPlayModeValidation.ArrangeStuckSwing(isRepeatedDeadlock);
        Time.timeScale = 1f;
        LevelSession session = UnityEngine.Object.FindObjectOfType<LevelSession>();
        ShelfBoard board = UnityEngine.Object.FindObjectOfType<ShelfBoard>();
        CyclicShelvesController cyclicShelves = UnityEngine.Object.FindObjectOfType<CyclicShelvesController>();
        int rescueFrame = -1;

        void HandleRescueApplied(bool isRepeated) => rescueFrame = Time.frameCount;

        cyclicShelves.RescueApplied += HandleRescueApplied;

        try
        {
            foreach (object step in WaitUntil(() => session.IsBoardSettled)) yield return step;

            foreach (object step in Drag(board, new TimedLevelMove(CyclicShelfPlayModeValidation.StuckMoveSource, CyclicShelfPlayModeValidation.StuckMoveTarget))) yield return step;

            foreach (object step in WaitUntil(() => rescueFrame >= 0 && Time.frameCount - rescueFrame >= ToastDelayFrames)) yield return step;

            CaptureMoment(momentName);

            foreach (object step in WaitUntil(() => session.IsBoardSettled && !cyclicShelves.IsSwitching)) yield return step;
        }
        finally
        {
            cyclicShelves.RescueApplied -= HandleRescueApplied;
        }

        for (int frame = 0; frame < TailFrameCount; frame++)
            yield return null;
    }

    private static IEnumerable Drag(ShelfBoard board, TimedLevelMove move)
    {
        ShelfItemDragController drag = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();
        ShelfItem item = board.Shelves[move.Source.ShelfIndex].Columns[move.Source.ColumnIndex].FrontItem;
        Vector2 from = Camera.main.WorldToScreenPoint(item.transform.position);
        Vector2 to = Camera.main.WorldToScreenPoint(board.Shelves[move.Target.ShelfIndex].ColumnViews[move.Target.ColumnIndex].ItemAnchor.position);

        if (!drag.TryBeginDrag(item, from))
            throw new InvalidOperationException($"The recorded move {move.Source.ShelfIndex}:{move.Source.ColumnIndex} -> {move.Target.ShelfIndex}:{move.Target.ColumnIndex} could not start (item {(item == null ? "missing" : item.name)}, source closed {board.IsShelfClosed(board.Shelves[move.Source.ShelfIndex])}).");

        for (int frame = 1; frame <= DragFrameCount; frame++)
        {
            drag.UpdateDrag(Vector2.Lerp(from, to, (float)frame / DragFrameCount));
            yield return null;
        }

        drag.EndDrag(to);
        yield return null;

        ShelfColumn target = board.Shelves[move.Target.ShelfIndex].Columns[move.Target.ColumnIndex];

        if (!target.Items.Contains(item))
            throw new InvalidOperationException($"The recorded move {move.Source.ShelfIndex}:{move.Source.ColumnIndex} -> {move.Target.ShelfIndex}:{move.Target.ColumnIndex} dropped {item.name} into {item.GetComponentInParent<ShelfColumnView>()?.name}/{item.GetComponentInParent<Shelf>()?.name}.");
    }

    private static IEnumerable WaitForLevel(LevelEntry level)
    {
        LevelSession session = null;
        return WaitUntil(() => (session = UnityEngine.Object.FindObjectOfType<LevelSession>()) != null && session.CurrentLevel == level && session.State == LevelState.Playing && session.IsBoardSettled);
    }

    private static IEnumerable CaptureResolutions(string momentName)
    {
        foreach ((int width, int height) in ScreenshotSizes)
        {
            string folder = Path.Combine(RecordingFolder, $"{momentName}-{width}x{height}");

            if (Directory.Exists(folder))
                Directory.Delete(folder, true);

            RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            ImageRecorderSettings imageSettings = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            imageSettings.name = momentName;
            imageSettings.Enabled = true;
            imageSettings.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            imageSettings.imageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
            imageSettings.OutputFile = $"{folder}/frame_{DefaultWildcard.Frame}";
            controllerSettings.AddRecorderSettings(imageSettings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = FrameRate;
            controllerSettings.CapFrameRate = true;
            RecorderController controller = new RecorderController(controllerSettings);
            controller.PrepareRecording();
            controller.StartRecording();

            for (int frame = 0; frame < ResolutionFrameCount; frame++)
                yield return null;

            controller.StopRecording();
            yield return null;

            string lastFrame = Directory.GetFiles(folder, "*.png").OrderBy(path => path).Last();
            Directory.CreateDirectory(OutputFolder);
            File.Copy(lastFrame, Path.Combine(OutputFolder, $"{momentName}-{width}x{height}.png"), true);
        }
    }

    private static void CaptureMoment(string momentName)
    {
        if (!CapturedMoments.Add(momentName))
            return;

        Directory.CreateDirectory(OutputFolder);
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, momentName + ".png"));
    }

    private static void StartRecording()
    {
        Directory.CreateDirectory(RecordingFolder);
        RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        MovieRecorderSettings movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieSettings.name = TakeName;
        movieSettings.Enabled = true;
        movieSettings.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
        };
        movieSettings.CaptureAudio = false;
        movieSettings.ImageInputSettings = new GameViewInputSettings { OutputWidth = OutputWidth, OutputHeight = OutputHeight };
        movieSettings.OutputFile = Path.Combine(RecordingFolder, TakeName);
        controllerSettings.AddRecorderSettings(movieSettings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
        controllerSettings.FrameRate = FrameRate;
        controllerSettings.CapFrameRate = true;
        _recorder = new RecorderController(controllerSettings);
        _recorder.PrepareRecording();
        _recorder.StartRecording();
    }

    private static LevelEntry GetChapterStartLevel()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        return catalog.Levels.First(level => CyclicShelfIntroHint.IsChapterStart(catalog, level));
    }

    private static void Select(LevelEntry level)
    {
        AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath).Select(level);
    }

    private static IEnumerable WaitUntil(Func<bool> condition)
    {
        for (int frame = 0; !condition(); frame++)
        {
            if (frame >= MaximumWaitFrames)
                throw new InvalidOperationException("The recording timed out.");

            yield return null;
        }
    }

    private static void Stop()
    {
        if (_recorder != null && _recorder.IsRecording())
            _recorder.StopRecording();

        _recorder = null;
        _steps = null;
        EditorApplication.isPlaying = false;
    }
}
