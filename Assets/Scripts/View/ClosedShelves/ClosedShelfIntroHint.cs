using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YG;

public sealed class ClosedShelfIntroHint : MonoBehaviour
{
    [Serializable]
    private sealed class HintText
    {
        [SerializeField] private string _ru;
        [SerializeField] private string _en;
        [SerializeField] private string _tr;

        public HintText()
        {
        }

        public HintText(string ru, string en, string tr)
        {
            _ru = ru;
            _en = en;
            _tr = tr;
        }

        public string Get(string language) => language switch
        {
            "ru" => _ru,
            "tr" => _tr,
            _ => _en
        };
    }

    private readonly struct Step
    {
        public ClosedShelfCoverView Cover { get; }
        public HintText Text { get; }

        public Step(ClosedShelfCoverView cover, HintText text)
        {
            Cover = cover;
            Text = text;
        }
    }

    [SerializeField] private LevelSession _session;
    [SerializeField] private ClosedShelvesController _closedShelves;
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private Camera _camera;
    [SerializeField] private RectTransform _bubble;
    [SerializeField] private CanvasGroup _bubbleGroup;
    [SerializeField] private TMP_Text _text;
    [SerializeField, Min(0f)] private float _hopInterval = 0.3f;
    [SerializeField, Min(0f)] private float _stepDuration = 3.5f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    [SerializeField] private Vector2 _bubbleOffset = new Vector2(0f, -24f);
    [SerializeField] private HintText _collectText = new HintText(
        "Соберите предметы с бирки, чтобы открыть полку",
        "Collect the items shown on the tag to open the shelf",
        "Rafı açmak için etiketteki eşyaları topla");
    [SerializeField] private HintText _refillText = new HintText(
        "Пока полка закрыта, новые предметы будут появляться снова и снова",
        "While a shelf is closed, new items keep appearing",
        "Raf kapalıyken yeni eşyalar gelmeye devam eder");
    [SerializeField] private HintText _shelfMatchesText = new HintText(
        "Собирайте тройки на подсвеченной полке",
        "Make matches on the highlighted shelf",
        "Vurgulanan rafta üçlü yap");

    private ClosedShelfCoverView _target;
    private Sequence _sequence;

    private void Awake()
    {
        if (_session == null || _closedShelves == null || _catalog == null || _camera == null || _bubble == null || _bubbleGroup == null || _text == null)
            throw new InvalidOperationException("Invalid closed shelf intro hint wiring.");

        HideImmediately();
    }

    private void OnEnable()
    {
        _closedShelves.CoversCreated += HandleCoversCreated;
        _session.StateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        _closedShelves.CoversCreated -= HandleCoversCreated;
        _session.StateChanged -= HandleStateChanged;
        _sequence?.Kill();
    }

    private void LateUpdate()
    {
        if (_target == null || !_bubble.gameObject.activeSelf)
            return;

        RectTransform parent = (RectTransform)_bubble.parent;
        Canvas canvas = _bubble.GetComponentInParent<Canvas>().rootCanvas;
        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = _camera.WorldToScreenPoint(_target.TagBottom);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, canvasCamera, out Vector2 localPoint))
            return;

        _bubble.anchoredPosition = localPoint - AnchorReference(parent) + _bubbleOffset;
        KeepBubbleInside(parent);
    }

    // In narrow windows a tag near the screen edge would push the bubble out of view.
    private void KeepBubbleInside(RectTransform parent)
    {
        Vector3[] corners = new Vector3[4];
        _bubble.GetWorldCorners(corners);
        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        Rect bounds = parent.rect;
        Vector2 shift = new Vector2(
            Mathf.Max(0f, bounds.xMin - min.x) - Mathf.Max(0f, max.x - bounds.xMax),
            Mathf.Max(0f, bounds.yMin - min.y) - Mathf.Max(0f, max.y - bounds.yMax));
        _bubble.anchoredPosition += shift;
    }

    public static IReadOnlyList<ShelfUnlockConditionKind> GetNewConditionKinds(LevelCatalog catalog, LevelEntry level)
    {
        HashSet<ShelfUnlockConditionKind> seen = new HashSet<ShelfUnlockConditionKind>();

        foreach (LevelEntry entry in catalog.Levels)
        {
            if (entry == level)
                break;

            foreach (ClosedShelfDefinition shelf in entry.Definition.ClosedShelves)
                seen.Add(shelf.Condition);
        }

        return level.Definition.ClosedShelves.Select(shelf => shelf.Condition).Distinct().Where(kind => !seen.Contains(kind)).ToArray();
    }

    private static bool IsChapterStart(LevelCatalog catalog, LevelEntry level)
    {
        return catalog.Levels.TakeWhile(entry => entry != level).All(entry => !entry.Definition.HasClosedShelves);
    }

    private void HandleCoversCreated(IReadOnlyList<ClosedShelfCoverView> covers)
    {
        LevelEntry level = _session.CurrentLevel;
        List<Step> steps = new List<Step>();

        foreach (ShelfUnlockConditionKind kind in GetNewConditionKinds(_catalog, level))
        {
            int index = level.Definition.ClosedShelves.ToList().FindIndex(shelf => shelf.Condition == kind);
            steps.Add(new Step(covers[index], GetText(kind)));
        }

        if (steps.Count == 0)
            return;

        if (IsChapterStart(_catalog, level))
            steps.Add(new Step(covers[0], _refillText));

        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetLink(gameObject);

        for (int index = 0; index < covers.Count; index++)
        {
            ClosedShelfCoverView cover = covers[index];
            _sequence.InsertCallback(index * _hopInterval, cover.Hop);
        }

        _bubble.gameObject.SetActive(true);
        _bubbleGroup.alpha = 0f;
        float stepLength = _fadeDuration * 2f + _stepDuration;

        for (int index = 0; index < steps.Count; index++)
        {
            Step step = steps[index];
            float start = index * stepLength;
            _sequence.InsertCallback(start, () => ShowStep(step));
            _sequence.Insert(start, _bubbleGroup.DOFade(1f, _fadeDuration));
            _sequence.Insert(start + _fadeDuration + _stepDuration, _bubbleGroup.DOFade(0f, _fadeDuration));
        }

        _sequence.OnComplete(HideImmediately);
    }

    private void ShowStep(Step step)
    {
        _target = step.Cover;
        _text.text = step.Text.Get(YG2.lang);
    }

    private HintText GetText(ShelfUnlockConditionKind kind) => kind switch
    {
        ShelfUnlockConditionKind.CollectItems => _collectText,
        ShelfUnlockConditionKind.MatchesOnShelf => _shelfMatchesText,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown unlock condition kind.")
    };

    private void HandleStateChanged(LevelState state)
    {
        if (state == LevelState.Playing || state == LevelState.Paused || !_bubble.gameObject.activeSelf)
            return;

        _sequence?.Kill();
        HideImmediately();
    }

    private void HideImmediately()
    {
        _target = null;
        _bubbleGroup.alpha = 0f;
        _bubble.gameObject.SetActive(false);
    }

    private Vector2 AnchorReference(RectTransform parent)
    {
        Vector2 anchor = (_bubble.anchorMin + _bubble.anchorMax) * 0.5f;
        Rect rect = parent.rect;
        return new Vector2(rect.xMin + rect.width * anchor.x, rect.yMin + rect.height * anchor.y);
    }
}
