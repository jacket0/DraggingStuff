using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YG;

public sealed class ShelfFilterIntroHint : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfFiltersController _shelfFilters;
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private Camera _camera;
    [SerializeField] private RectTransform _bubble;
    [SerializeField] private CanvasGroup _bubbleGroup;
    [SerializeField] private TMP_Text _text;
    [SerializeField, Min(0f)] private float _visibleDuration = 5f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    [SerializeField] private Vector2 _bubbleOffset = new Vector2(0f, -24f);
    [SerializeField] private LocalizedHintText _ruleText = new LocalizedHintText(
        "Предметы с таблички собираются только на этой полке",
        "Items on the sign only match on this shelf",
        "Tabeladaki eşyalar yalnızca bu rafta eşleşir");

    private ShelfFilterSignView _target;
    private Sequence _sequence;
    private bool _isHiding;

    public bool IsVisible => _bubble.gameObject.activeSelf;

    private void Awake()
    {
        if (_session == null || _shelfFilters == null || _catalog == null || _camera == null || _bubble == null || _bubbleGroup == null || _text == null)
            throw new InvalidOperationException($"{name}: invalid shelf filter intro hint wiring.");

        HideImmediately();
    }

    private void OnEnable()
    {
        _shelfFilters.SignsShown += HandleSignsShown;
        _session.StateChanged += HandleStateChanged;
        _session.Timer.StateChanged += HandleTimerStateChanged;
    }

    private void OnDisable()
    {
        _shelfFilters.SignsShown -= HandleSignsShown;
        _session.StateChanged -= HandleStateChanged;
        _session.Timer.StateChanged -= HandleTimerStateChanged;
        _sequence?.Kill();
    }

    private void LateUpdate()
    {
        if (_target == null || !IsVisible)
            return;

        HintBubblePlacement.PlaceAt(_bubble, _camera, _target.BottomCenter, _bubbleOffset);
    }

    public static bool IsChapterStart(LevelCatalog catalog, LevelEntry level)
    {
        return level.Definition.HasShelfFilters && catalog.Levels.TakeWhile(entry => entry != level).All(entry => !entry.Definition.HasShelfFilters);
    }

    private void HandleSignsShown(IReadOnlyList<ShelfFilterSignView> signs)
    {
        if (signs.Count == 0 || !IsChapterStart(_catalog, _session.CurrentLevel))
            return;

        _target = signs[0];
        _text.text = _ruleText.Get(YG2.lang);
        _isHiding = false;
        _bubble.gameObject.SetActive(true);
        _bubbleGroup.alpha = 0f;
        _sequence?.Kill();
        _sequence = DOTween.Sequence()
            .SetLink(gameObject)
            .Append(_bubbleGroup.DOFade(1f, _fadeDuration))
            .AppendInterval(_visibleDuration)
            .Append(_bubbleGroup.DOFade(0f, _fadeDuration))
            .OnComplete(HideImmediately);
    }

    private void HandleTimerStateChanged(CountdownTimerState state)
    {
        if (!IsVisible || _isHiding || !_session.Timer.HasStarted)
            return;

        _isHiding = true;
        _sequence?.Kill();
        _sequence = DOTween.Sequence()
            .SetLink(gameObject)
            .Append(_bubbleGroup.DOFade(0f, _fadeDuration))
            .OnComplete(HideImmediately);
    }

    private void HandleStateChanged(LevelState state)
    {
        if (state == LevelState.Playing || state == LevelState.Paused || !IsVisible)
            return;

        _sequence?.Kill();
        HideImmediately();
    }

    private void HideImmediately()
    {
        _target = null;
        _isHiding = false;
        _bubbleGroup.alpha = 0f;
        _bubble.gameObject.SetActive(false);
    }
}
