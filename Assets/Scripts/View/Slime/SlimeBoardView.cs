using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public sealed class SlimeBoardView : MonoBehaviour
{
    [SerializeField] private ShelfBoard _shelfBoard;
    [SerializeField] private SlimeSpotView _spotPrefab;
    [SerializeField] private SlimeForecastView _forecast;
    [SerializeField] private SlimeSourceView _source;
    [SerializeField, Min(1)] private int _forecastCount = 3;
    [SerializeField] private Vector3 _spotOffset = new Vector3(0f, -0.02f, 0f);
    [SerializeField] private Vector3 _forecastOffset = new Vector3(0f, 0.32f, 0f);
    [SerializeField] private Vector3 _rejectedPunch = new Vector3(0f, 0.05f, 0f);
    [SerializeField, Min(0.01f)] private float _rejectedDuration = 0.3f;
    [SerializeField, Min(0f)] private float _driedDelayPerMeter = 0.25f;

    private readonly Dictionary<ColumnPosition, SlimeSpotView> _spots = new Dictionary<ColumnPosition, SlimeSpotView>();
    private readonly List<SlimeForecastView> _extraForecasts = new List<SlimeForecastView>();

    public SlimeForecastView Forecast => _forecast;
    public int ForecastCount => _forecastCount;
    public bool IsAnimating => _spots.Values.Any(spot => spot.IsAnimating);
    public bool IsSourceShown => _source.IsShown;

    private void Awake()
    {
        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_spotPrefab == null)
            throw new InvalidOperationException(nameof(_spotPrefab));

        if (_forecast == null)
            throw new InvalidOperationException(nameof(_forecast));

        if (_source == null)
            throw new InvalidOperationException(nameof(_source));
    }

    public bool IsSpotVisible(ColumnPosition column) => _spots.TryGetValue(column, out SlimeSpotView spot) && spot.IsVisible;

    public IReadOnlyList<ColumnPosition> GetVisibleSpots() => _spots.Where(pair => pair.Value.IsVisible).Select(pair => pair.Key).ToArray();

    public void ShowLevel(IReadOnlyList<ColumnPosition> stickyColumns)
    {
        if (stickyColumns == null)
            throw new ArgumentNullException(nameof(stickyColumns));

        HideForLevel();

        foreach (ColumnPosition column in stickyColumns)
            GetSpot(column).ShowInstant();

        _source.ShowInstant();
    }

    public void HideForLevel()
    {
        foreach (SlimeSpotView spot in _spots.Values)
            spot.Hide();

        _source.Hide();
        HideForecast();
    }

    public void PlayGrowth(ColumnPosition column, bool isFromSource, Action completed)
    {
        if (isFromSource)
            _source.PlayPulse(GetAnchorPosition(column));

        GetSpot(column).PlayAppear(completed);
    }

    public void PlayCleaned(IReadOnlyList<ColumnPosition> columns)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        foreach (ColumnPosition column in columns)
        {
            if (_spots.TryGetValue(column, out SlimeSpotView spot))
                spot.PlayVanish();
        }
    }

    public void PlayDried()
    {
        Vector3 center = _shelfBoard.Shelves.Aggregate(Vector3.zero, (sum, shelf) => sum + shelf.transform.position) / _shelfBoard.Shelves.Count;
        float lastDelay = 0f;

        foreach (KeyValuePair<ColumnPosition, SlimeSpotView> pair in _spots)
        {
            float delay = Vector3.Distance(GetAnchorPosition(pair.Key), center) * _driedDelayPerMeter;
            lastDelay = Mathf.Max(lastDelay, delay);
            pair.Value.PlayVanish(delay);
        }

        _source.PlayRetreat(lastDelay);
        HideForecast();
    }

    public void PlayShelfLost(Shelf shelf)
    {
        int shelfIndex = FindShelfIndex(shelf);

        foreach (KeyValuePair<ColumnPosition, SlimeSpotView> pair in _spots)
        {
            if (pair.Key.ShelfIndex == shelfIndex)
                pair.Value.PlayClench();
        }

        HideForecast();
    }

    public void PlayDeadlock()
    {
        foreach (SlimeSpotView spot in _spots.Values)
            spot.PlayClench();

        HideForecast();
    }

    public void ShowForecast(IReadOnlyList<ColumnPosition> columns, IReadOnlyList<int> movesToGrowth)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        if (movesToGrowth == null || movesToGrowth.Count != columns.Count)
            throw new ArgumentException("Every forecast column needs its moves to growth.", nameof(movesToGrowth));

        if (columns.Count == 0)
        {
            HideForecast();
            return;
        }

        _forecast.Show(GetAnchorPosition(columns[0]) + _forecastOffset, movesToGrowth[0]);

        for (int index = 1; index < _forecastCount; index++)
        {
            SlimeForecastView forecast = GetExtraForecast(index - 1);

            if (index < columns.Count)
                forecast.Show(GetAnchorPosition(columns[index]) + _forecastOffset, movesToGrowth[index]);
            else
                forecast.Hide();
        }
    }

    public void HideForecast()
    {
        _forecast.Hide();

        foreach (SlimeForecastView forecast in _extraForecasts)
            forecast.Hide();
    }

    public void PlayPickUpRejected(ShelfItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (_shelfBoard.ItemAnimations.IsAnimating(item) || DOTween.IsTweening(item.transform))
            return;

        item.transform.DOPunchPosition(_rejectedPunch, _rejectedDuration, 6, 0.5f).SetLink(item.gameObject, LinkBehaviour.KillOnDisable);
    }

    private SlimeForecastView GetExtraForecast(int index)
    {
        while (_extraForecasts.Count <= index)
        {
            SlimeForecastView forecast = Instantiate(_forecast, _forecast.transform.parent);
            forecast.name = $"{_forecast.name}_{_extraForecasts.Count + 2}";
            _extraForecasts.Add(forecast);
        }

        return _extraForecasts[index];
    }

    private int FindShelfIndex(Shelf shelf)
    {
        if (shelf == null)
            throw new ArgumentNullException(nameof(shelf));

        for (int shelfIndex = 0; shelfIndex < _shelfBoard.Shelves.Count; shelfIndex++)
        {
            if (_shelfBoard.Shelves[shelfIndex] == shelf)
                return shelfIndex;
        }

        throw new ArgumentException($"{shelf.name} is not on the slime board.", nameof(shelf));
    }

    private SlimeSpotView GetSpot(ColumnPosition column)
    {
        if (_spots.TryGetValue(column, out SlimeSpotView spot))
            return spot;

        ShelfColumnView columnView = _shelfBoard.GetView(_shelfBoard.GetColumn(column));
        spot = Instantiate(_spotPrefab, columnView.transform);
        spot.name = $"SlimeSpot_{column.ShelfIndex}_{column.ColumnIndex}";
        spot.transform.position = GetAnchorPosition(column);
        spot.transform.rotation = columnView.ItemAnchor.rotation;
        _spots.Add(column, spot);
        return spot;
    }

    private Vector3 GetAnchorPosition(ColumnPosition column)
    {
        return _shelfBoard.GetView(_shelfBoard.GetColumn(column)).ItemAnchor.position + _spotOffset;
    }
}
