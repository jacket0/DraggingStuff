using System;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YG;

public sealed class SlimeIntroHint : MonoBehaviour
{
    private static readonly Vector2 BubbleAbovePivot = new Vector2(0.5f, 0f);
    private static readonly Vector2 BubbleBelowPivot = new Vector2(0.5f, 1f);

    private readonly struct Step
    {
        public LocalizedHintText Text { get; }
        public bool PointsAtForecast { get; }

        public Step(LocalizedHintText text, bool pointsAtForecast)
        {
            Text = text;
            PointsAtForecast = pointsAtForecast;
        }
    }

    [SerializeField] private LevelSession _session;
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private SlimeBoardView _view;
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private Camera _camera;
    [SerializeField] private RectTransform _bubble;
    [SerializeField] private CanvasGroup _bubbleGroup;
    [SerializeField] private TMP_Text _text;
    [SerializeField, Min(0f)] private float _stepDuration = 3.5f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    [SerializeField] private Vector2 _spotBubbleOffset = new Vector2(0f, 48f);
    [SerializeField] private Vector2 _forecastBubbleOffset = new Vector2(0f, -76f);
    [SerializeField] private LocalizedHintText _stuckText = new LocalizedHintText(
        "Предметы в слизи прилипли, их нельзя взять",
        "Items in the slime are stuck and can't be picked up",
        "Balçıktaki eşyalar yapıştı, onları alamazsın");
    [SerializeField] private LocalizedHintText _cleanText = new LocalizedHintText(
        "Соберите тройку на этой полке, чтобы смыть слизь",
        "Make a match on this shelf to wash the slime away",
        "Balçığı temizlemek için bu rafta üçlü yap");
    [SerializeField] private LocalizedHintText _forecastText = new LocalizedHintText(
        "Слизь растекается: цифра показывает, через сколько ходов она будет здесь",
        "The slime spreads: the number shows how many moves until it gets here",
        "Balçık yayılıyor: sayı, buraya kaç hamlede geleceğini gösterir");

    private Step[] _steps = Array.Empty<Step>();
    private int _currentStep;
    private Sequence _sequence;

    public bool IsVisible => _bubble.gameObject.activeSelf;
    public string Text => _text.text;

    private void Awake()
    {
        if (_session == null)
            throw new InvalidOperationException(nameof(_session));

        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_view == null)
            throw new InvalidOperationException(nameof(_view));

        if (_catalog == null)
            throw new InvalidOperationException(nameof(_catalog));

        if (_camera == null)
            throw new InvalidOperationException(nameof(_camera));

        if (_bubble == null)
            throw new InvalidOperationException(nameof(_bubble));

        if (_bubbleGroup == null)
            throw new InvalidOperationException(nameof(_bubbleGroup));

        if (_text == null)
            throw new InvalidOperationException(nameof(_text));

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

        if (_steps[_currentStep].PointsAtForecast && _view.Forecast.gameObject.activeSelf)
        {
            _bubble.pivot = BubbleBelowPivot;
            HintBubblePlacement.PlaceAt(_bubble, _camera, _view.Forecast.transform.position, _forecastBubbleOffset);
            return;
        }

        if (!TryGetSpotPosition(out Vector3 spotPosition))
            return;

        _bubble.pivot = BubbleAbovePivot;
        HintBubblePlacement.PlaceAt(_bubble, _camera, spotPosition, _spotBubbleOffset);
    }

    public static bool IsChapterStart(LevelCatalog catalog, LevelEntry level)
    {
        return level.Definition.HasSlime && catalog.Levels.TakeWhile(entry => entry != level).All(entry => !entry.Definition.HasSlime);
    }

    private bool TryGetSpotPosition(out Vector3 position)
    {
        if (!_shelfBoard.HasSlime || !_shelfBoard.Slime.HasStickyColumns)
        {
            position = Vector3.zero;
            return false;
        }

        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        position = _shelfBoard.Slime.StickyColumns
            .Select(column => _view.GetSpotPosition(column))
            .OrderBy(position => Vector2.Distance(_camera.WorldToScreenPoint(position), screenCenter))
            .First();
        return true;
    }

    private void HandleSessionStarted()
    {
        _sequence?.Kill();
        HideImmediately();

        if (!IsChapterStart(_catalog, _session.CurrentLevel))
            return;

        _steps = new[]
        {
            new Step(_stuckText, false),
            new Step(_cleanText, false),
            new Step(_forecastText, true)
        };
        _currentStep = 0;
        _bubble.gameObject.SetActive(true);
        _sequence = DOTween.Sequence().SetLink(gameObject);
        float stepLength = _fadeDuration * 2f + _stepDuration;

        for (int index = 0; index < _steps.Length; index++)
        {
            int stepIndex = index;
            float start = index * stepLength;
            _sequence.InsertCallback(start, () => ShowStep(stepIndex));
            _sequence.Insert(start, _bubbleGroup.DOFade(1f, _fadeDuration));
            _sequence.Insert(start + _fadeDuration + _stepDuration, _bubbleGroup.DOFade(0f, _fadeDuration));
        }

        _sequence.OnComplete(HideImmediately);
    }

    private void ShowStep(int index)
    {
        _currentStep = index;
        _text.text = _steps[index].Text.Get(YG2.lang);
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
