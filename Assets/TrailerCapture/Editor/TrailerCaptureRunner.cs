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
    private const string LanguageQueueKey = "TrailerCapture.QueueLanguage";
    private const string ShotsQueueKey = "TrailerCapture.QueueShots";
    private const string OutputFolderName = "LocalOutput/Trailer";
    private const string CinematicFolderName = "LocalOutput/Cinematic";
    private const string ShotsFolderName = "LocalOutput/PromoShots";
    private const int OutputWidth = 1920;
    private const int OutputHeight = 1080;
    private const int CinematicWidth = 3840;
    private const int CinematicHeight = 2160;
    private const float CinematicIntroSeconds = 3f;
    private const float FrameRate = 60f;

    private static readonly TrailerTake[] Takes =
    {
        new TrailerTake("menu", "Assets/Scenes/MainMenu.unity", 0, 0, 0.5f),
        new TrailerTake("level03", "Assets/Scenes/SimpleLevel.unity", 3, int.MaxValue, 4f),
        new TrailerTake("level06", "Assets/Scenes/SecondLevel.unity", 6, 10, 1f),
        new TrailerTake("level11", "Assets/Scenes/ThirdLevel.unity", 11, 10, 1f),
        new TrailerTake("level14", "Assets/Scenes/FourthLevel.unity", 14, 18, 1.5f),
        new TrailerTake("level20", "Assets/Scenes/FifthLevel.unity", 20, 12, 1f),
        new TrailerTake("endless", "Assets/Scenes/EndlessLevel.unity", 0, 14, 1f),
        new TrailerTake("shot_chapter1", "Assets/Scenes/SimpleLevel.unity", 4, 16, 1f),
        new TrailerTake("shot_chapter2", "Assets/Scenes/ThirdLevel.unity", 8, 16, 1f),
        new TrailerTake("shot_chapter3", "Assets/Scenes/FourthLevel.unity", 13, 16, 1f),
        new TrailerTake("shot_chapter4", "Assets/Scenes/FifthLevel.unity", 18, 16, 1f),
        new TrailerTake("shot_chapter5", "Assets/Scenes/SixthLevel.unity", 24, 16, 2f),
        new TrailerTake("shot_endless", "Assets/Scenes/EndlessLevel.unity", 0, 20, 1f),
        new TrailerTake("shot_menu", "Assets/Scenes/MainMenu.unity", 0, 0, 0.5f),
        new TrailerTake("trailer_chapter1", "Assets/Scenes/SimpleLevel.unity", 4, 16, 0.5f),
        new TrailerTake("trailer_chapter2", "Assets/Scenes/ThirdLevel.unity", 8, 16, 0.5f),
        new TrailerTake("trailer_chapter3", "Assets/Scenes/FourthLevel.unity", 13, 21, 1f),
        new TrailerTake("trailer_chapter4", "Assets/Scenes/FifthLevel.unity", 18, 16, 0.5f),
        new TrailerTake("trailer_chapter5", "Assets/Scenes/SixthLevel.unity", 24, 16, 0.5f),
        new TrailerTake("trailer_endless", "Assets/Scenes/EndlessLevel.unity", 0, 20, 0.5f),
        new TrailerTake("trailer_menu", "Assets/Scenes/MainMenu.unity", 0, 0, 0.5f),
        new TrailerTake("cine_clear", "Assets/Scenes/SimpleLevel.unity", 3, int.MaxValue, 3f, isCinematic: true),
        new TrailerTake("cine_chapter1", "Assets/Scenes/SimpleLevel.unity", 5, 16, 1f, isCinematic: true),
        new TrailerTake("cine_chapter2", "Assets/Scenes/ThirdLevel.unity", 8, 16, 1f, isCinematic: true),
        new TrailerTake("cine_chapter3", "Assets/Scenes/FourthLevel.unity", 13, 21, 1f, isCinematic: true),
        new TrailerTake("cine_chapter4", "Assets/Scenes/FifthLevel.unity", 18, 16, 1f, isCinematic: true),
        new TrailerTake("cine_chapter5", "Assets/Scenes/SixthLevel.unity", 24, 16, 2f, isCinematic: true),
        new TrailerTake("cine_endless", "Assets/Scenes/EndlessLevel.unity", 0, 24, 1f, isCinematic: true, showsHud: true)
    };

    private static readonly string[] ChapterShotTakes =
        { "shot_chapter1", "shot_chapter2", "shot_chapter3", "shot_chapter4", "shot_chapter5" };

    private static RecorderController _recorderController;
    private static bool _isStopping;

    private static string ProjectFolder => Directory.GetParent(Application.dataPath).FullName;
    private static string OutputFolder => Path.Combine(ProjectFolder, OutputFolderName);

    static TrailerCaptureRunner()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update += Update;
    }

    private static bool IsWaitingForNextTake =>
        SessionState.GetBool(TrailerCaptureKeys.IsActive, false)
        && SessionState.GetBool(TrailerCaptureKeys.IsFinished, false)
        && !EditorApplication.isPlayingOrWillChangePlaymode;

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

    [MenuItem("Tools/Trailer Capture/Screenshots/Chapters RU")]
    private static void CaptureChapterShotsRussian() => StartQueue(ChapterShotTakes, "ru", true);

    [MenuItem("Tools/Trailer Capture/Screenshots/All RU")]
    private static void CaptureAllShotsRussian() => StartQueue(AllShotTakes, "ru", true);

    [MenuItem("Tools/Trailer Capture/Screenshots/All EN")]
    private static void CaptureAllShotsEnglish() => StartQueue(AllShotTakes, "en", true);

    [MenuItem("Tools/Trailer Capture/Screenshots/All TR")]
    private static void CaptureAllShotsTurkish() => StartQueue(AllShotTakes, "tr", true);

    private static IEnumerable<string> AllShotTakes =>
        Takes.Select(take => take.Name).Where(name => name.StartsWith("shot_"));

    [MenuItem("Tools/Trailer Capture/Cinematic/Test Clear RU")]
    private static void RecordCinematicTest() => StartQueue(new[] { "cine_clear" }, "ru");

    [MenuItem("Tools/Trailer Capture/Cinematic/All RU")]
    private static void RecordCinematicRussian() => StartQueue(CinematicTakes, "ru");

    [MenuItem("Tools/Trailer Capture/Cinematic/All EN")]
    private static void RecordCinematicEnglish() => StartQueue(CinematicTakes, "en");

    [MenuItem("Tools/Trailer Capture/Cinematic/All TR")]
    private static void RecordCinematicTurkish() => StartQueue(CinematicTakes, "tr");

    private static IEnumerable<string> CinematicTakes =>
        Takes.Where(take => take.IsCinematic).Select(take => take.Name);

    [MenuItem("Tools/Trailer Capture/Stop")]
    private static void StopCapture()
    {
        SessionState.SetString(QueueKey, string.Empty);
        SessionState.SetBool(TrailerCaptureKeys.IsFinished, true);

        if (!EditorApplication.isPlaying)
            Complete();
    }

    private static void StartQueue(IEnumerable<string> takeNames, string language = "", bool isShots = false)
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
        SessionState.SetString(LanguageQueueKey, language);
        SessionState.SetBool(ShotsQueueKey, isShots);
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

        SessionState.SetBool(TrailerCaptureKeys.IsActive, true);
        SessionState.SetBool(TrailerCaptureKeys.IsFinished, false);
        SessionState.SetInt(TrailerCaptureKeys.LevelNumber, take.LevelNumber);
        SessionState.SetInt(TrailerCaptureKeys.MaximumMoves, take.MaximumMoves);
        SessionState.SetFloat(TrailerCaptureKeys.TailSeconds, take.TailSeconds);
        SessionState.SetBool(TrailerCaptureKeys.IsCinematic, take.IsCinematic);
        SessionState.SetBool(TrailerCaptureKeys.ShowsHud, take.ShowsHud);
        SessionState.EraseFloat(TrailerCaptureKeys.IntroSeconds);

        if (take.IsCinematic)
            SessionState.SetFloat(TrailerCaptureKeys.IntroSeconds, CinematicIntroSeconds);

        string language = SessionState.GetString(LanguageQueueKey, string.Empty);
        string takeFileName = string.IsNullOrEmpty(language) ? take.Name : $"{take.Name}_{language}";
        bool isShots = SessionState.GetBool(ShotsQueueKey, false);
        string shotsFolder = isShots ? Path.Combine(ProjectFolder, ShotsFolderName, language, take.Name) : string.Empty;
        SessionState.SetString(TrailerCaptureKeys.Language, language);
        SessionState.SetString(TrailerCaptureKeys.ShotsFolder, shotsFolder);
        SessionState.SetString(CurrentTakeKey, takeFileName);
        SessionState.SetString(TrailerCaptureKeys.EventLogPath, Path.Combine(GetOutputFolder(take.IsCinematic), takeFileName + ".json"));

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

        if (change != PlayModeStateChange.EnteredEditMode)
            return;

        _isStopping = false;

        if (!SessionState.GetBool(TrailerCaptureKeys.IsFinished, false))
        {
            _recorderController = null;
            Debug.LogWarning("Trailer capture: take interrupted, queue stopped.");
            EditorApplication.delayCall += Complete;
        }
    }

    private static void Update()
    {
        if (IsWaitingForNextTake)
        {
            StartNextTake();
            return;
        }

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

    private static string GetOutputFolder(bool isCinematic) =>
        isCinematic ? Path.Combine(ProjectFolder, CinematicFolderName) : OutputFolder;

    private static void StartRecording(string takeName)
    {
        bool isCinematic = SessionState.GetBool(TrailerCaptureKeys.IsCinematic, false);
        string outputFolder = GetOutputFolder(isCinematic);
        Directory.CreateDirectory(outputFolder);

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
            OutputWidth = isCinematic ? CinematicWidth : OutputWidth,
            OutputHeight = isCinematic ? CinematicHeight : OutputHeight
        };
        movieSettings.OutputFile = Path.Combine(outputFolder, takeName);

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
        public bool IsCinematic { get; }
        public bool ShowsHud { get; }

        public TrailerTake(string name, string scenePath, int levelNumber, int maximumMoves, float tailSeconds,
            bool isCinematic = false, bool showsHud = false)
        {
            Name = name;
            ScenePath = scenePath;
            LevelNumber = levelNumber;
            MaximumMoves = maximumMoves;
            TailSeconds = tailSeconds;
            IsCinematic = isCinematic;
            ShowsHud = showsHud;
        }
    }
}
