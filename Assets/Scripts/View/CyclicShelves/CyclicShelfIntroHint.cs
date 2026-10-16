using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YG;

public sealed class CyclicShelfIntroHint : MonoBehaviour
{
    private const int QueueCellCount = 3;

    [SerializeField] private LevelSession _session;
    [SerializeField] private DepartureBoardView _departureBoard;
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private Camera _camera;
    [SerializeField] private RectTransform _bubble;
    [SerializeField] private CanvasGroup _bubbleGroup;
    [SerializeField] private TMP_Text _text;
    [SerializeField, Min(0f)] private float _stepDuration = 3.5f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    [SerializeField] private Vector2 _bubbleOffset = new Vector2(0f, -56f);
    [SerializeField] private LocalizedHintText _countText = new LocalizedHintText(
        "Табло считает ходы, в которых собрана тройка",
        "The board counts the moves where you make a match",
        "Tablo, üçlü yaptığın hamleleri sayar");
    [SerializeField] private LocalizedHintText _closeText = new LocalizedHintText(
        "На нуле ячейка закроется, предметы останутся внутри",
        "At zero the locker closes and the items stay inside",
        "Sıfırda dolap kapanır, eşyalar içinde kalır");
    [SerializeField] private LocalizedHintText _hurryText = new LocalizedHintText(
        "Успей забрать нужное, пока ячейка открыта",
        "Take what you need while the locker is open",
        "Dolap açıkken ihtiyacın olanı al");
    [SerializeField] private LocalizedHintText _queueText = new LocalizedHintText(
        "Теперь ячейки открываются по очереди",
        "Now the lockers open one after another",
        "Artık dolaplar sırayla açılıyor");

    private Sequence _sequence;

    public bool IsVisible => _bubble.gameObject.activeSelf;
    public string Text => _text.text;

    private void Awake()
    {
        if (_session == null || _departureBoard == null || _catalog == null || _camera == null || _bubble == null || _bubbleGroup == null || _text == null)
            throw new InvalidOperationException($"{name}: invalid cyclic shelf intro hint wiring.");

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
        if (!IsVisible)
            return;

        HintBubblePlacement.PlaceAt(_bubble, _camera, _departureBoard.transform.position, _bubbleOffset);
    }

    public static bool IsChapterStart(LevelCatalog catalog, LevelEntry level)
    {
        return level.Definition.HasCyclicShelves && catalog.Levels.TakeWhile(entry => entry != level).All(entry => !entry.Definition.HasCyclicShelves);
    }

    public static bool IsQueueStart(LevelCatalog catalog, LevelEntry level)
    {
        return HasQueue(level) && !catalog.Levels.TakeWhile(entry => entry != level).Any(HasQueue);
    }

    private static bool HasQueue(LevelEntry level) => level.Definition.CyclicShelves.Count >= QueueCellCount;

    private IReadOnlyList<LocalizedHintText> GetSteps(LevelEntry level)
    {
        if (IsChapterStart(_catalog, level))
            return new[] { _countText, _closeText, _hurryText };

        if (IsQueueStart(_catalog, level))
            return new[] { _queueText };

        return Array.Empty<LocalizedHintText>();
    }

    private void HandleSessionStarted()
    {
        _sequence?.Kill();
        HideImmediately();
        IReadOnlyList<LocalizedHintText> steps = GetSteps(_session.CurrentLevel);

        if (steps.Count == 0)
            return;

        _bubble.gameObject.SetActive(true);
        _sequence = DOTween.Sequence().SetLink(gameObject);
        float stepLength = _fadeDuration * 2f + _stepDuration;

        for (int index = 0; index < steps.Count; index++)
        {
            LocalizedHintText step = steps[index];
            float start = index * stepLength;
            _sequence.InsertCallback(start, () => _text.text = step.Get(YG2.lang));
            _sequence.Insert(start, _bubbleGroup.DOFade(1f, _fadeDuration));
            _sequence.Insert(start + _fadeDuration + _stepDuration, _bubbleGroup.DOFade(0f, _fadeDuration));
        }

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
        _bubbleGroup.alpha = 0f;
        _bubble.gameObject.SetActive(false);
    }
}
