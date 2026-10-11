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
public static class KeyLockCollectionRecorder
{
    private const string ActiveKey = "KeyLockCollectionRecorder.Active";
    private const string SelectionPath = "Assets/Levels/Menu/CurrentLevelSelection.asset";
    private const string ScenePath = "Assets/Scenes/SixthLevel.unity";
    private const string OutputFolderName = ".utmp/key-locks/video";
    private const string TakeName = "key-collection";
    private const int OutputWidth = 1280;
    private const int OutputHeight = 720;
    private const float FrameRate = 30f;
    private const int MaximumWaitFrames = 3000;
    private const int DragFrameCount = 12;
    private const int TailFrameCount = 45;

    private static readonly (int Delay, string Name)[] CollectionMoments =
    {
        (3, "2-key-hop"),
        (10, "3-key-flight"),
        (21, "4-key-turn"),
        (25, "5-lock-click"),
        (33, "6-lock-drop"),
        (40, "7-bars-lift"),
        (45, "8-item-wave")
    };

    private static readonly HashSet<string> CapturedMoments = new HashSet<string>();
    private static IEnumerator _steps;
    private static int _lastFrame = -1;
    private static RecorderController _recorder;

    private static string OutputFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolderName);
    private static string RecordingFolder => Path.Combine(Path.GetTempPath(), "KeyLockRecording");

    static KeyLockCollectionRecorder()
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

    [MenuItem("Tools/Timed Levels/Record Key Collection")]
    public static void Begin()
    {
        LevelSelectionState selection = AssetDatabase.LoadAssetAtPath<LevelSelectionState>(SelectionPath);
        selection.Select(KeyLockPlayModeValidation.GetScenarioLevel());
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
                Debug.Log($"KEY_LOCK_RECORDING_DONE: {outputPath}");
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
        LevelSession session = null;

        foreach (object step in WaitUntil(() => (session = UnityEngine.Object.FindObjectOfType<LevelSession>()) != null && session.State == LevelState.Playing && session.IsBoardSettled))
            yield return step;

        ShelfBoard board = UnityEngine.Object.FindObjectOfType<ShelfBoard>();
        KeyLocksController keyLocks = UnityEngine.Object.FindObjectOfType<KeyLocksController>();
        ShelfItemDragController drag = UnityEngine.Object.FindObjectOfType<ShelfItemDragController>();
        StartRecording();
        IReadOnlyList<TimedLevelMove> solution = session.CurrentLevel.Definition.Variants[0].CreateSolution();

        foreach (TimedLevelMove move in solution)
        {
            ShelfItem item = board.Shelves[move.Source.ShelfIndex].Columns[move.Source.ColumnIndex].FrontItem;
            Vector2 from = Camera.main.WorldToScreenPoint(item.transform.position);
            Vector2 to = Camera.main.WorldToScreenPoint(board.Shelves[move.Target.ShelfIndex].ColumnViews[move.Target.ColumnIndex].ItemAnchor.position);

            if (!drag.TryBeginDrag(item, from))
                throw new InvalidOperationException("The recorded move could not start.");

            for (int frame = 1; frame <= DragFrameCount; frame++)
            {
                drag.UpdateDrag(Vector2.Lerp(from, to, (float)frame / DragFrameCount));
                yield return null;
            }

            drag.EndDrag(to);
            int collectionFrame = -1;

            for (int frame = 0; !(session.IsBoardSettled && !keyLocks.IsCollecting); frame++)
            {
                if (frame >= MaximumWaitFrames)
                    throw new InvalidOperationException("The recording timed out.");

                if (board.Shelves.Any(shelf => shelf.Columns.Any(column => column.FrontKey != null)))
                    CaptureMoment("1-key-in-front");

                if (keyLocks.IsCollecting && collectionFrame < 0)
                    collectionFrame = frame;

                foreach ((int delay, string name) in CollectionMoments)
                {
                    if (collectionFrame >= 0 && frame - collectionFrame == delay)
                        CaptureMoment(name);
                }

                yield return null;
            }

            if (!board.HasKeyLocks)
                break;
        }

        CaptureMoment("9-safe-open");

        for (int frame = 0; frame < TailFrameCount; frame++)
            yield return null;
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
