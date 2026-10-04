using System;
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
public static class TrailerCaptureRunner
{
    private const string QueueKey = "TrailerCapture.Queue";
    private const string CurrentTakeKey = "TrailerCapture.CurrentTake";
    private const string ReturnScenePathKey = "TrailerCapture.ReturnScenePath";
    private const string OutputFolderName = "Recordings/Trailer";
    private const int OutputWidth = 1920;
    private const int OutputHeight = 1080;
    private const float FrameRate = 60f;

    private static readonly TrailerTake[] Takes =
    {
        new TrailerTake("menu", "Assets/Scenes/MainMenu.unity", 0, 0, 0.5f),
        new TrailerTake("level03", "Assets/Scenes/SimpleLevel.unity", 3, int.MaxValue, 4f),
        new TrailerTake("level06", "Assets/Scenes/SecondLevel.unity", 6, 10, 1f),
        new TrailerTake("level11", "Assets/Scenes/ThirdLevel.unity", 11, 10, 1f),
        new TrailerTake("level14", "Assets/Scenes/FourthLevel.unity", 14, 18, 1.5f),
        new TrailerTake("level20", "Assets/Scenes/FifthLevel.unity", 20, 12, 1f),
        new TrailerTake("endless", "Assets/Scenes/EndlessLevel.unity", 0, 14, 1f)
    };

    private static RecorderController _recorderController;
    private static bool _isStopping;

    private static string OutputFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolderName);

    static TrailerCaptureRunner()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/Trailer Capture/Record All Takes")]
    private static void RecordAllTakes() => StartQueue(Takes.Select(take => take.Name));

    [MenuItem("Tools/Trailer Capture/Record Menu")]
    private static void RecordMenu() => StartQueue(new[] { "menu" });

    [MenuItem("Tools/Trailer Capture/Record Level 3")]
    private static void RecordLevel3() => StartQueue(new[] { "level03" });

    [MenuItem("Tools/Trailer Capture/Record Level 6")]
    private static void RecordLevel6() => StartQueue(new[] { "level06" });

    [MenuItem("Tools/Trailer Capture/Record Level 11")]
    private static void RecordLevel11() => StartQueue(new[] { "level11" });

    [MenuItem("Tools/Trailer Capture/Record Level 14")]
    private static void RecordLevel14() => StartQueue(new[] { "level14" });

    [MenuItem("Tools/Trailer Capture/Record Level 20")]
    private static void RecordLevel20() => StartQueue(new[] { "level20" });

    [MenuItem("Tools/Trailer Capture/Record Endless")]
    private static void RecordEndless() => StartQueue(new[] { "endless" });

    [MenuItem("Tools/Trailer Capture/Stop")]
    private static void StopCapture()
    {
        SessionState.SetString(QueueKey, string.Empty);
        SessionState.SetBool(TrailerCaptureKeys.IsFinished, true);

        if (!EditorApplication.isPlaying)
            Complete();
    }

    private static void StartQueue(IEnumerable<string> takeNames)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Trailer capture: exit Play Mode first.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SessionState.SetString(ReturnScenePathKey, SceneManager.GetActiveScene().path);
        SessionState.SetString(QueueKey, string.Join(",", takeNames));
        StartNextTake();
    }

    private static void StartNextTake()
    {
        List<string> queue = ReadQueue();

        if (queue.Count == 0)
        {
            Complete();
            return;
        }

        TrailerTake take = Takes.First(candidate => candidate.Name == queue[0]);
        queue.RemoveAt(0);
        SessionState.SetString(QueueKey, string.Join(",", queue));

        SessionState.SetString(CurrentTakeKey, take.Name);
        SessionState.SetBool(TrailerCaptureKeys.IsActive, true);
        SessionState.SetBool(TrailerCaptureKeys.IsFinished, false);
        SessionState.SetInt(TrailerCaptureKeys.LevelNumber, take.LevelNumber);
        SessionState.SetInt(TrailerCaptureKeys.MaximumMoves, take.MaximumMoves);
        SessionState.SetFloat(TrailerCaptureKeys.TailSeconds, take.TailSeconds);
        SessionState.SetString(TrailerCaptureKeys.EventLogPath, Path.Combine(OutputFolder, take.Name + ".json"));

        Debug.Log($"Trailer capture: starting take {take.Name}.");
        EditorSceneManager.OpenScene(take.ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(TrailerCaptureKeys.IsActive, false))
            return;

        if (change == PlayModeStateChange.EnteredPlayMode)
            StartRecording(SessionState.GetString(CurrentTakeKey, "take"));

        if (change == PlayModeStateChange.EnteredEditMode)
        {
            _isStopping = false;
            EditorApplication.delayCall += StartNextTake;
        }
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || _isStopping || !SessionState.GetBool(TrailerCaptureKeys.IsActive, false))
            return;

        if (!SessionState.GetBool(TrailerCaptureKeys.IsFinished, false))
            return;

        _isStopping = true;

        if (_recorderController != null && _recorderController.IsRecording())
            _recorderController.StopRecording();

        _recorderController = null;
        EditorApplication.isPlaying = false;
    }

    private static void StartRecording(string takeName)
    {
        Directory.CreateDirectory(OutputFolder);

        RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        MovieRecorderSettings movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieSettings.name = takeName;
        movieSettings.Enabled = true;
        movieSettings.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
        };
        movieSettings.CaptureAudio = true;
        movieSettings.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = OutputWidth,
            OutputHeight = OutputHeight
        };
        movieSettings.OutputFile = Path.Combine(OutputFolder, takeName);

        controllerSettings.AddRecorderSettings(movieSettings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
        controllerSettings.FrameRate = FrameRate;
        controllerSettings.CapFrameRate = true;

        _recorderController = new RecorderController(controllerSettings);
        _recorderController.PrepareRecording();
        _recorderController.StartRecording();
        SessionState.SetInt(TrailerCaptureKeys.RecordingStartFrame, Time.frameCount);
    }

    private static void Complete()
    {
        SessionState.SetBool(TrailerCaptureKeys.IsActive, false);
        string returnScenePath = SessionState.GetString(ReturnScenePathKey, string.Empty);

        if (!string.IsNullOrEmpty(returnScenePath) && SceneManager.GetActiveScene().path != returnScenePath)
            EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);

        Debug.Log("Trailer capture: finished.");
    }

    private static List<string> ReadQueue()
    {
        return SessionState.GetString(QueueKey, string.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private sealed class TrailerTake
    {
        public string Name { get; }
        public string ScenePath { get; }
        public int LevelNumber { get; }
        public int MaximumMoves { get; }
        public float TailSeconds { get; }

        public TrailerTake(string name, string scenePath, int levelNumber, int maximumMoves, float tailSeconds)
        {
            Name = name;
            ScenePath = scenePath;
            LevelNumber = levelNumber;
            MaximumMoves = maximumMoves;
            TailSeconds = tailSeconds;
        }
    }
}
