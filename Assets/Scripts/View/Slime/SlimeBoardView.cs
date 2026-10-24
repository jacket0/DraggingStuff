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
    [SerializeField] private Vector3 _spotOffset = new Vector3(0f, -0.02f, 0f);
    [SerializeField] private Vector3 _forecastOffset = new Vector3(0f, 0.32f, 0f);
    [SerializeField] private Vector3 _rejectedPunch = new Vector3(0f, 0.05f, 0f);
    [SerializeField, Min(0.01f)] private float _rejectedDuration = 0.3f;

    private readonly Dictionary<ColumnPosition, SlimeSpotView> _spots = new Dictionary<ColumnPosition, SlimeSpotView>();

    public SlimeForecastView Forecast => _forecast;
    public bool IsAnimating => _spots.Values.Any(spot => spot.IsAnimating);

    private void Awake()
    {
        if (_shelfBoard == null)
            throw new InvalidOperationException(nameof(_shelfBoard));

        if (_spotPrefab == null)
            throw new InvalidOperationException(nameof(_spotPrefab));

        if (_forecast == null)
            throw new InvalidOperationException(nameof(_forecast));
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
    }

    public void HideForLevel()
    {
        foreach (SlimeSpotView spot in _spots.Values)
            spot.Hide();

        HideForecast();
    }

    public void PlayGrowth(ColumnPosition column, Action completed) => GetSpot(column).PlayAppear(completed);

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
        foreach (SlimeSpotView spot in _spots.Values)
            spot.PlayVanish();

        HideForecast();
    }

    public void ShowForecast(ColumnPosition column, int movesToGrowth)
    {
        _forecast.Show(GetAnchorPosition(column) + _forecastOffset, movesToGrowth);
    }

    public void HideForecast() => _forecast.Hide();

    public void PlayPickUpRejected(ShelfItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (_shelfBoard.ItemAnimations.IsAnimating(item) || DOTween.IsTweening(item.transform))
            return;

        item.transform.DOPunchPosition(_rejectedPunch, _rejectedDuration, 6, 0.5f).SetLink(item.gameObject, LinkBehaviour.KillOnDisable);
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
