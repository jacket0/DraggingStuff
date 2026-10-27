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
    [SerializeField] private SlimeSplashView _splash;
    [SerializeField, Min(1)] private int _forecastCount = 3;
    [SerializeField] private Vector3 _spotOffset = new Vector3(0f, -0.095f, -0.07f);
    [SerializeField] private Vector3 _forecastOffset = new Vector3(0f, 0.004f, 0f);
    [SerializeField, Range(0f, 1f)] private float _spreadOriginFactor = 0.7f;
    [SerializeField] private Vector3 _splashOffset = new Vector3(0f, 0.03f, 0.04f);
    [SerializeField, Min(1)] private int _growSplashCount = 5;
    [SerializeField, Min(1)] private int _cleanSplashCount = 14;
    [SerializeField, Min(1)] private int _rejectSplashCount = 6;
    [SerializeField] private Vector3 _rejectedStretch = new Vector3(0.9f, 1.15f, 0.9f);
    [SerializeField, Min(0f)] private float _rejectedLift = 0.03f;
    [SerializeField, Min(0f)] private float _rejectedTwist = 8f;
    [SerializeField, Min(0.01f)] private float _rejectedPullDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float _rejectedReleaseDuration = 0.4f;
    [SerializeField, Min(0f)] private float _driedDelayPerMeter = 0.25f;

    private readonly Dictionary<ColumnPosition, SlimeSpotView> _spots = new Dictionary<ColumnPosition, SlimeSpotView>();
    private readonly Dictionary<ShelfItem, Sequence> _rejections = new Dictionary<ShelfItem, Sequence>();
    private readonly List<SlimeForecastView> _extraForecasts = new List<SlimeForecastView>();

    public SlimeForecastView Forecast => _forecast;
    public int ForecastCount => _forecastCount;
    public bool IsAnimating => _spots.Values.Any(spot => spot.IsAnimating);

    private void Awake()
    {
        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_spotPrefab == null)
            throw new InvalidOperationException(nameof(_spotPrefab));

        if (_forecast == null)
            throw new InvalidOperationException(nameof(_forecast));

        if (_splash == null)
            throw new InvalidOperationException(nameof(_splash));
    }

    public bool IsSpotVisible(ColumnPosition column) => _spots.TryGetValue(column, out SlimeSpotView spot) && spot.IsVisible;

    public Vector3 GetSpotPosition(ColumnPosition column) => GetAnchorPosition(column);

    public IReadOnlyList<ColumnPosition> GetVisibleSpots() => _spots.Where(pair => pair.Value.IsVisible).Select(pair => pair.Key).ToArray();

    public void ShowLevel(IReadOnlyList<ColumnPosition> stickyColumns)
    {
        if (stickyColumns == null)
            throw new ArgumentNullException(nameof(stickyColumns));

        HideForLevel();

        foreach (ColumnPosition column in stickyColumns)
            GetSpot(column).ShowInstant();

        RefreshBridges();
    }

    public void HideForLevel()
    {
        foreach (SlimeSpotView spot in _spots.Values)
            spot.Hide();

        HideForecast();
    }

    public void PlayGrowth(ColumnPosition column, Action completed)
    {
        SlimeSpotView spot = GetSpot(column);
        spot.PlayAppear(GetSpreadOrigin(column, spot), completed);
        _splash.Burst(spot.transform.position + _splashOffset, _growSplashCount);
        RefreshBridges();
    }

    public void PlayCleaned(IReadOnlyList<ColumnPosition> columns)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));

        foreach (ColumnPosition column in columns)
        {
            if (!_spots.TryGetValue(column, out SlimeSpotView spot))
                continue;

            spot.PlayVanish();
            _splash.Burst(spot.transform.position + _splashOffset, _cleanSplashCount);
        }

        RefreshBridges();
    }

    public void PlayDried()
    {
        Vector3 center = _shelfBoard.Shelves.Aggregate(Vector3.zero, (sum, shelf) => sum + shelf.transform.position) / _shelfBoard.Shelves.Count;

        foreach (KeyValuePair<ColumnPosition, SlimeSpotView> pair in _spots)
            pair.Value.PlayVanish(Vector3.Distance(GetAnchorPosition(pair.Key), center) * _driedDelayPerMeter);

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

        if (_shelfBoard.ItemAnimations.IsAnimating(item))
            return;

        foreach (KeyValuePair<ColumnPosition, SlimeSpotView> pair in _spots)
        {
            if (_shelfBoard.GetColumn(pair.Key) != item.Column)
                continue;

            pair.Value.PlayClench();
            _splash.Burst(pair.Value.transform.position + _splashOffset, _rejectSplashCount);
        }

        if (_rejections.TryGetValue(item, out Sequence running))
            running.Complete(true);

        Transform body = item.transform;
        Vector3 restScale = body.localScale;
        Sequence rejection = DOTween.Sequence().SetLink(item.gameObject, LinkBehaviour.KillOnDisable);
        rejection.Append(body.DOScale(Vector3.Scale(restScale, _rejectedStretch), _rejectedPullDuration).SetEase(Ease.OutQuad));
        rejection.Join(body.DOLocalMoveY(body.localPosition.y + _rejectedLift, _rejectedPullDuration).SetEase(Ease.OutQuad));
        rejection.Append(body.DOScale(restScale, _rejectedReleaseDuration).SetEase(Ease.OutElastic));
        rejection.Join(body.DOLocalMoveY(body.localPosition.y, _rejectedReleaseDuration).SetEase(Ease.OutBounce));
        rejection.Join(body.DOPunchRotation(new Vector3(0f, 0f, _rejectedTwist), _rejectedReleaseDuration, 8, 0.6f));
        rejection.OnKill(() =>
        {
            if (_rejections.TryGetValue(item, out Sequence current) && current == rejection)
                _rejections.Remove(item);
        });
        _rejections[item] = rejection;
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
        spot.SetWall(GetWallDirection(column, spot));
        _spots.Add(column, spot);
        return spot;
    }

    private float GetWallDirection(ColumnPosition column, SlimeSpotView spot)
    {
        Shelf shelf = _shelfBoard.Shelves[column.ShelfIndex];

        if (column.ColumnIndex != 0 && column.ColumnIndex != shelf.Capacity - 1)
            return 0f;

        Vector3 towardWall = GetAnchorPosition(column) - shelf.transform.position;
        return Mathf.Sign(spot.transform.InverseTransformVector(towardWall).x);
    }

    private Vector3 GetSpreadOrigin(ColumnPosition column, SlimeSpotView spot)
    {
        foreach (int step in new[] { -1, 1 })
        {
            ColumnPosition neighbour = new ColumnPosition(column.ShelfIndex, column.ColumnIndex + step);

            if (_spots.TryGetValue(neighbour, out SlimeSpotView neighbourSpot) && neighbourSpot.IsPresent && neighbourSpot != spot)
                return spot.transform.InverseTransformVector(GetAnchorPosition(neighbour) - GetAnchorPosition(column)) * _spreadOriginFactor;
        }

        return Vector3.zero;
    }

    private void RefreshBridges()
    {
        foreach (KeyValuePair<ColumnPosition, SlimeSpotView> pair in _spots)
        {
            ColumnPosition next = new ColumnPosition(pair.Key.ShelfIndex, pair.Key.ColumnIndex + 1);
            bool shown = pair.Value.IsPresent && _spots.TryGetValue(next, out SlimeSpotView nextSpot) && nextSpot.IsPresent;
            Vector3 center = shown ? pair.Value.transform.InverseTransformVector(GetAnchorPosition(next) - GetAnchorPosition(pair.Key)) * 0.5f : Vector3.zero;
            pair.Value.SetBridge(shown, center);
        }
    }

    private Vector3 GetAnchorPosition(ColumnPosition column)
    {
        return _shelfBoard.GetView(_shelfBoard.GetColumn(column)).ItemAnchor.position + _spotOffset;
    }
}
