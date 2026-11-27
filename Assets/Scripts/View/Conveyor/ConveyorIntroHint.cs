using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YG;

public sealed class ConveyorIntroHint : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private List<ConveyorBeltView> _beltViews = new List<ConveyorBeltView>();
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private Camera _camera;
    [SerializeField] private RectTransform _bubble;
    [SerializeField] private CanvasGroup _bubbleGroup;
    [SerializeField] private TMP_Text _text;
    [SerializeField, Min(0f)] private float _stepDuration = 4.5f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    [SerializeField] private Vector2 _bubbleOffset = new Vector2(0f, -56f);
    [SerializeField] private LocalizedHintText _shiftText = new LocalizedHintText(
        "После каждой тройки ленты сдвигают предметы на шаг",
        "After every match the belts move items one step",
        "Her üçlüden sonra bantlar eşyaları bir adım kaydırır");

    private ConveyorBeltView _target;
    private Sequence _sequence;

    public bool IsVisible => _bubble.gameObject.activeSelf;
    public string Text => _text.text;

    private void Awake()
    {
        if (_session == null || _shelfBoard == null || _catalog == null || _camera == null || _bubble == null || _bubbleGroup == null || _text == null)
            throw new InvalidOperationException($"{name}: invalid conveyor intro hint wiring.");

        if (_beltViews.Count == 0 || _beltViews.Contains(null))
            throw new InvalidOperationException(nameof(_beltViews));

        HideImmediately();
    }

    private void OnEnable()
    {
        _session.SessionStarted += HandleSessionStarted;
        _session.StateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        _session.SessionStarted -= HandleSessionStarted;
        _session.StateChanged -= HandleStateChanged;
        _sequence?.Kill();
    }

    private void LateUpdate()
    {
        if (_target == null || !IsVisible)
            return;

        HintBubblePlacement.PlaceAt(_bubble, _camera, _target.transform.position, _bubbleOffset);
    }

    public static bool IsChapterStart(LevelCatalog catalog, LevelEntry level)
    {
        return level.Definition.HasConveyors && catalog.Levels.TakeWhile(entry => entry != level).All(entry => !entry.Definition.HasConveyors);
    }

    private void HandleSessionStarted()
    {
        _sequence?.Kill();
        HideImmediately();

        if (!IsChapterStart(_catalog, _session.CurrentLevel))
            return;

        Shelf firstConveyorShelf = _shelfBoard.ConveyorShelves.First();
        _target = _beltViews.First(view => view.Shelf == firstConveyorShelf);
        _text.text = _shiftText.Get(YG2.lang);
        _bubble.gameObject.SetActive(true);
        _sequence = DOTween.Sequence().SetLink(gameObject);
        _sequence.Append(_bubbleGroup.DOFade(1f, _fadeDuration));
        _sequence.AppendInterval(_stepDuration);
        _sequence.Append(_bubbleGroup.DOFade(0f, _fadeDuration));
        _sequence.OnComplete(HideImmediately);
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
        _bubbleGroup.alpha = 0f;
        _bubble.gameObject.SetActive(false);
    }
}
