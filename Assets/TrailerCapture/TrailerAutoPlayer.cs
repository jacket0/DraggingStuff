#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TrailerCaptureKeys
{
    public const string IsActive = "TrailerCapture.IsActive";
    public const string IsFinished = "TrailerCapture.IsFinished";
    public const string LevelNumber = "TrailerCapture.LevelNumber";
    public const string MaximumMoves = "TrailerCapture.MaximumMoves";
    public const string TailSeconds = "TrailerCapture.TailSeconds";
    public const string EventLogPath = "TrailerCapture.EventLogPath";
    public const string RecordingStartFrame = "TrailerCapture.RecordingStartFrame";
}

public sealed class TrailerAutoPlayer : MonoBehaviour
{
    private const float IntroSeconds = 1.2f;
    private const float ApproachSeconds = 0.3f;
    private const float HoverSeconds = 0.12f;
    private const float DragSeconds = 0.42f;
    private const float MinimumGapBetweenMovesSeconds = 0.2f;
    private const float WaitForMoveTimeoutSeconds = 8f;
    private const float TakeTimeoutSeconds = 150f;
    private const float MenuStartDelaySeconds = 1.5f;
    private const float MenuScrollSeconds = 3.5f;
    private const float PointerExitSeconds = 0.45f;
    private const float PointerSizeAtReferenceHeight = 72f;
    private const float ReferenceScreenHeight = 1080f;
    private const float PressedPointerScale = 0.8f;
    private const float MusicCheckIntervalSeconds = 0.25f;
    private const float MusicMinimumClipLengthSeconds = 45f;
    private const int PointerTextureSize = 128;

    private readonly List<TrailerCaptureEvent> _events = new List<TrailerCaptureEvent>();

    private GameSession _session;
    private ShelfBoard _shelfBoard;
    private ShelfItemDragController _dragController;
    private Camera _camera;
    private RectTransform _pointer;
    private Vector2 _pointerPosition;
    private bool _isPointerPressed;
    private IReadOnlyList<TimedLevelMove> _solution;
    private int _solutionStep;
    private ShelfColumnView _lastMoveSource;
    private ShelfColumnView _lastMoveTarget;
    private float _startTime;
    private float _nextMusicCheckTime;
    private bool _isFinished;

    private float PointerScale => Screen.height / ReferenceScreenHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!SessionState.GetBool(TrailerCaptureKeys.IsActive, false))
            return;

        SelectLevel(SessionState.GetInt(TrailerCaptureKeys.LevelNumber, 0));

        GameObject host = new GameObject(nameof(TrailerAutoPlayer));
        DontDestroyOnLoad(host);
        host.AddComponent<TrailerAutoPlayer>();
    }

    private static void SelectLevel(int levelNumber)
    {
        if (levelNumber <= 0)
            return;

        LevelEntry level = LoadAssets<LevelEntry>().FirstOrDefault(entry => entry.Number == levelNumber);

        if (level == null)
            throw new InvalidOperationException($"Trailer capture: level {levelNumber} is not found.");

        foreach (LevelSelectionState selection in LoadAssets<LevelSelectionState>())
            selection.Select(level);
    }

    private static IEnumerable<T> LoadAssets<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null);
    }

    private IEnumerator Start()
    {
        _startTime = Time.time;
        CreatePointer();

        yield return null;
        yield return null;

        _session = FindObjectOfType<GameSession>();

        if (_session == null)
            yield return PlayMenu();
        else
            yield return PlayBoard();

        yield return WaitSeconds(SessionState.GetFloat(TrailerCaptureKeys.TailSeconds, 2f));
        Finish();
    }

    private void Update()
    {
        if (Time.time >= _nextMusicCheckTime)
        {
            _nextMusicCheckTime = Time.time + MusicCheckIntervalSeconds;
            MuteMusic();
        }

        if (!_isFinished && Time.time - _startTime > TakeTimeoutSeconds)
        {
            StopAllCoroutines();
            LogEvent("timeout");
            Finish();
        }
    }

    private void LateUpdate()
    {
        if (_pointer == null)
            return;

        _pointer.position = _pointerPosition;
        float pressScale = _isPointerPressed ? PressedPointerScale : 1f;
        _pointer.localScale = Vector3.one * PointerScale * pressScale;
    }

    private IEnumerator PlayMenu()
    {
        _pointer.gameObject.SetActive(false);
        yield return WaitSeconds(MenuStartDelaySeconds);

        ScrollRect scroll = FindObjectsOfType<ScrollRect>()
            .Where(candidate => candidate.vertical && candidate.content != null)
            .OrderByDescending(candidate => candidate.content.rect.height)
            .FirstOrDefault();

        if (scroll == null)
            yield break;

        LogEvent("menu_scroll");
        float startPosition = scroll.verticalNormalizedPosition;
        float elapsed = 0f;

        while (elapsed < MenuScrollSeconds)
        {
            elapsed += Time.deltaTime;
            scroll.verticalNormalizedPosition = Mathf.Lerp(startPosition, 0f, EaseInOut(elapsed / MenuScrollSeconds));
            yield return null;
        }
    }

    private IEnumerator PlayBoard()
    {
        _shelfBoard = FindObjectOfType<ShelfBoard>();
        _dragController = FindObjectOfType<ShelfItemDragController>();
        _camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();

        foreach (ShelfPointerInput pointerInput in FindObjectsOfType<ShelfPointerInput>())
            pointerInput.enabled = false;

        SubscribeToGameEvents();
        yield return WaitUntilOrTimeout(() => _session.CanInteract, WaitForMoveTimeoutSeconds);

        _solution = LoadSolution();
        _pointerPosition = new Vector2(Screen.width * 1.05f, Screen.height * 0.2f);
        LogEvent("start", _solution != null ? "solution" : "greedy");
        yield return WaitSeconds(IntroSeconds);

        int maximumMoves = SessionState.GetInt(TrailerCaptureKeys.MaximumMoves, int.MaxValue);
        int moveCount = 0;

        while (moveCount < maximumMoves && _session.State == LevelState.Playing)
        {
            ShelfColumnView source = null;
            ShelfColumnView target = null;
            float waited = 0f;
            bool hasMove = false;

            while (_session.State == LevelState.Playing && waited < WaitForMoveTimeoutSeconds)
            {
                if (waited >= MinimumGapBetweenMovesSeconds && TryChooseMove(out source, out target))
                {
                    hasMove = true;
                    break;
                }

                waited += Time.deltaTime;
                yield return null;
            }

            if (!hasMove)
            {
                LogEvent("no_move");
                break;
            }

            yield return PerformMove(source, target);
            moveCount++;
        }

        LogEvent("moves_done", moveCount.ToString());
        yield return MovePointerTo(() => new Vector2(Screen.width * 1.1f, Screen.height * 0.1f), PointerExitSeconds);
    }

    private IReadOnlyList<TimedLevelMove> LoadSolution()
    {
        if (!(_session is LevelSession levelSession) || levelSession.CurrentLevel == null || levelSession.CurrentVariantIndex < 0)
            return null;

        TimedLevelVariant variant = levelSession.CurrentLevel.Definition.Variants[levelSession.CurrentVariantIndex];
        return variant.HasSolution ? variant.CreateSolution() : null;
    }

    private bool TryChooseMove(out ShelfColumnView source, out ShelfColumnView target)
    {
        source = null;
        target = null;

        if (!_session.CanInteract)
            return false;

        bool isSettled = _session.IsBoardSettled;

        if (_shelfBoard.HasConveyors && !isSettled)
            return false;

        if (_solution != null && _solutionStep < _solution.Count)
        {
            TimedLevelMove move = _solution[_solutionStep];
            ShelfColumnView solutionSource = _shelfBoard.Shelves[move.Source.ShelfIndex].ColumnViews[move.Source.ColumnIndex];
            ShelfColumnView solutionTarget = _shelfBoard.Shelves[move.Target.ShelfIndex].ColumnViews[move.Target.ColumnIndex];

            if (_shelfBoard.CanMove(solutionSource.Column, solutionTarget.Column))
            {
                _solutionStep++;
                source = solutionSource;
                target = solutionTarget;
                return true;
            }

            if (!isSettled)
                return false;

            LogEvent("solution_abandoned", _solutionStep.ToString());
            _solution = null;
        }

        return isSettled && TryFindGreedyMove(out source, out target);
    }

    private bool TryFindGreedyMove(out ShelfColumnView bestSource, out ShelfColumnView bestTarget)
    {
        bestSource = null;
        bestTarget = null;
        int bestScore = int.MinValue;

        foreach (Shelf sourceShelf in _shelfBoard.Shelves)
        {
            foreach (ShelfColumnView source in sourceShelf.ColumnViews)
            {
                if (!_shelfBoard.CanPickUp(source.Column))
                    continue;

                ItemType type = source.FrontItem.Type;
                int brokenPairs = CountFrontsOfType(sourceShelf, type, source);

                foreach (Shelf targetShelf in _shelfBoard.Shelves)
                {
                    foreach (ShelfColumnView target in targetShelf.ColumnViews)
                    {
                        if (target == source || IsReverseOfLastMove(source, target))
                            continue;

                        if (!_shelfBoard.TrySimulateMove(source.Column, target.Column, out BoardMoveSimulation simulation))
                            continue;

                        int groupGain = targetShelf == sourceShelf ? 0 : CountFrontsOfType(targetShelf, type, target);
                        int score = simulation.MatchCount * 100 + groupGain * 12 - brokenPairs * 8
                            + (target.IsEmpty ? 0 : 2) + UnityEngine.Random.Range(0, 4);

                        if (score <= bestScore)
                            continue;

                        bestScore = score;
                        bestSource = source;
                        bestTarget = target;
                    }
                }
            }
        }

        return bestSource != null;
    }

    private static int CountFrontsOfType(Shelf shelf, ItemType type, ShelfColumnView excludedColumn)
    {
        return shelf.ColumnViews.Count(view => view != excludedColumn && view.FrontItem != null && view.FrontItem.Type == type);
    }

    private bool IsReverseOfLastMove(ShelfColumnView source, ShelfColumnView target)
    {
        return source == _lastMoveTarget && target == _lastMoveSource;
    }

    private IEnumerator PerformMove(ShelfColumnView source, ShelfColumnView target)
    {
        ShelfItem item = source.FrontItem;
        _lastMoveSource = source;
        _lastMoveTarget = target;
        LogEvent("move", $"{DescribeColumn(source)}>{DescribeColumn(target)}");

        yield return MovePointerTo(() => ToScreenPoint(item.transform.position), ApproachSeconds);

        ShelfItemOutlineView outline = item.GetComponent<ShelfItemOutlineView>();

        if (outline != null)
            outline.Show();

        yield return WaitSeconds(HoverSeconds);

        if (outline != null)
            outline.Hide();

        _isPointerPressed = true;
        Vector2 pressPosition = _pointerPosition;

        if (!_session.CanInteract || !_dragController.TryBeginDrag(item, pressPosition))
        {
            _isPointerPressed = false;
            LogEvent("drag_rejected");
            yield break;
        }

        LogEvent("drag");
        float elapsed = 0f;

        while (elapsed < DragSeconds)
        {
            if (!_dragController.IsDragging)
            {
                _isPointerPressed = false;
                LogEvent("drag_cancelled");
                yield break;
            }

            elapsed += Time.deltaTime;
            Vector2 end = GetTargetScreenPoint(target);
            float arcHeight = Mathf.Min(140f * PointerScale, Vector2.Distance(pressPosition, end) * 0.25f);
            Vector2 control = (pressPosition + end) * 0.5f + Vector2.up * arcHeight;
            _pointerPosition = QuadraticBezier(pressPosition, control, end, EaseInOut(elapsed / DragSeconds));
            _dragController.UpdateDrag(_pointerPosition);
            yield return null;
        }

        _pointerPosition = GetTargetScreenPoint(target);
        _dragController.UpdateDrag(_pointerPosition);
        _dragController.EndDrag(_pointerPosition);
        _isPointerPressed = false;
        LogEvent("drop");
    }

    private Vector2 GetTargetScreenPoint(ShelfColumnView target)
    {
        Vector3 worldPosition = target.FrontItem != null ? target.FrontItem.transform.position : target.ItemAnchor.position;
        return ToScreenPoint(worldPosition);
    }

    private Vector2 ToScreenPoint(Vector3 worldPosition)
    {
        return _camera.WorldToScreenPoint(worldPosition);
    }

    private IEnumerator MovePointerTo(Func<Vector2> destination, float duration)
    {
        Vector2 start = _pointerPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _pointerPosition = Vector2.Lerp(start, destination(), EaseInOut(elapsed / duration));
            yield return null;
        }

        _pointerPosition = destination();
    }

    private void SubscribeToGameEvents()
    {
        _session.MatchSucceeded += _ => LogEvent("match");
        _session.StateChanged += state => LogEvent("state", state.ToString());

        ClosedShelvesController closedShelves = FindObjectOfType<ClosedShelvesController>();

        if (closedShelves != null)
            closedShelves.ShelfOpened += shelfIndex => LogEvent("shelf_opened", shelfIndex.ToString());

        ConveyorController conveyor = FindObjectOfType<ConveyorController>();

        if (conveyor != null)
            conveyor.ShiftStarted += () => LogEvent("conveyor_shift");
    }

    private void CreatePointer()
    {
        GameObject canvasObject = new GameObject("TrailerPointerCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        GameObject pointerObject = new GameObject("TrailerPointer");
        pointerObject.transform.SetParent(canvasObject.transform, false);
        Image image = pointerObject.AddComponent<Image>();
        image.sprite = CreatePointerSprite();
        image.raycastTarget = false;

        _pointer = image.rectTransform;
        _pointer.sizeDelta = Vector2.one * PointerSizeAtReferenceHeight;
        _pointerPosition = new Vector2(Screen.width * 1.1f, Screen.height * 0.2f);
    }

    private static Sprite CreatePointerSprite()
    {
        Texture2D texture = new Texture2D(PointerTextureSize, PointerTextureSize, TextureFormat.RGBA32, false);
        float radius = PointerTextureSize * 0.5f;

        for (int y = 0; y < PointerTextureSize; y++)
        {
            for (int x = 0; x < PointerTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * radius) / radius;
                texture.SetPixel(x, y, GetPointerColor(distance));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, PointerTextureSize, PointerTextureSize), Vector2.one * 0.5f);
    }

    private static Color GetPointerColor(float normalizedDistance)
    {
        Color fill = new Color(1f, 1f, 1f, 0.4f);
        Color ring = new Color(1f, 1f, 1f, 1f);
        Color shadow = new Color(0f, 0f, 0f, 0.35f);
        Color outside = new Color(0f, 0f, 0f, 0f);

        if (normalizedDistance < 0.68f)
            return fill;

        if (normalizedDistance < 0.72f)
            return Color.Lerp(fill, ring, Mathf.InverseLerp(0.68f, 0.72f, normalizedDistance));

        if (normalizedDistance < 0.84f)
            return ring;

        if (normalizedDistance < 0.88f)
            return Color.Lerp(ring, shadow, Mathf.InverseLerp(0.84f, 0.88f, normalizedDistance));

        if (normalizedDistance < 0.96f)
            return shadow;

        return Color.Lerp(shadow, outside, Mathf.InverseLerp(0.96f, 1f, normalizedDistance));
    }

    private static void MuteMusic()
    {
        foreach (AudioSource source in FindObjectsOfType<AudioSource>())
        {
            bool isMusicGroup = source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name.Contains("Music");
            bool isLongClip = source.clip != null && source.clip.length > MusicMinimumClipLengthSeconds;

            if (isMusicGroup || isLongClip)
                source.mute = true;
        }
    }

    private void LogEvent(string kind, string detail = "")
    {
        _events.Add(new TrailerCaptureEvent { frame = Time.frameCount, time = Time.time, kind = kind, detail = detail });
    }

    private void Finish()
    {
        if (_isFinished)
            return;

        _isFinished = true;
        _isPointerPressed = false;
        LogEvent("finish");
        WriteEventLog();
        SessionState.SetBool(TrailerCaptureKeys.IsFinished, true);
    }

    private void WriteEventLog()
    {
        string path = SessionState.GetString(TrailerCaptureKeys.EventLogPath, string.Empty);

        if (string.IsNullOrEmpty(path))
            return;

        TrailerCaptureLog log = new TrailerCaptureLog
        {
            recordingStartFrame = SessionState.GetInt(TrailerCaptureKeys.RecordingStartFrame, 0),
            events = _events
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(log, true));
    }

    private static IEnumerator WaitSeconds(float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds)
    {
        float elapsed = 0f;

        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static float EaseInOut(float progress)
    {
        progress = Mathf.Clamp01(progress);
        return progress * progress * (3f - 2f * progress);
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        float inverse = 1f - progress;
        return inverse * inverse * start + 2f * inverse * progress * control + progress * progress * end;
    }

    private static string DescribeColumn(ShelfColumnView view)
    {
        return $"{view.Shelf.name}/{view.name}";
    }

    [Serializable]
    private sealed class TrailerCaptureEvent
    {
        public int frame;
        public float time;
        public string kind;
        public string detail;
    }

    [Serializable]
    private sealed class TrailerCaptureLog
    {
        public int recordingStartFrame;
        public List<TrailerCaptureEvent> events;
    }
}
#endif
