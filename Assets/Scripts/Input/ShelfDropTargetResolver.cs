using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShelfDropTargetResolver
{
    private readonly Camera _camera;
    private readonly ShelfBoard _shelfBoard;
    private readonly LayerMask _columnLayerMask;
    private readonly float _shelfPaddingPixels;

    public ShelfDropTargetResolver(Camera camera, ShelfBoard shelfBoard, LayerMask columnLayerMask, float shelfPaddingPixels)
    {
        _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
        _shelfBoard = shelfBoard != null ? shelfBoard : throw new ArgumentNullException(nameof(shelfBoard));
        _columnLayerMask = columnLayerMask;
        _shelfPaddingPixels = Mathf.Max(0f, shelfPaddingPixels);
    }

    public bool TryResolve(ShelfColumnView sourceColumn, Vector2 pointerPosition, out ShelfColumnView targetColumn)
    {
        if (sourceColumn == null)
            throw new ArgumentNullException(nameof(sourceColumn));

        if (TryGetDirectColumn(pointerPosition, out ShelfColumnView directColumn))
        {
            targetColumn = _shelfBoard.CanMove(sourceColumn.Column, directColumn.Column) ? directColumn : null;
            return targetColumn != null;
        }

        if (!TryGetNearestShelf(pointerPosition, out Shelf nearestShelf))
        {
            targetColumn = null;
            return false;
        }

        return TryGetNearestValidColumn(sourceColumn, nearestShelf, pointerPosition, out targetColumn);
    }

    private bool TryGetDirectColumn(Vector2 pointerPosition, out ShelfColumnView column)
    {
        Ray ray = _camera.ScreenPointToRay(pointerPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, _columnLayerMask, QueryTriggerInteraction.Collide);
        Array.Sort(hits, CompareHits);

        foreach (RaycastHit hit in hits)
        {
            ShelfColumnView candidate = hit.collider.GetComponentInParent<ShelfColumnView>();
            Shelf shelf = candidate != null ? candidate.GetComponentInParent<Shelf>() : null;

            if (shelf == null || !shelf.isActiveAndEnabled || candidate.Shelf != shelf || _shelfBoard.IsShelfLocked(shelf) || !ContainsShelf(shelf))
                continue;

            column = candidate;
            return true;
        }

        column = null;
        return false;
    }

    private bool TryGetNearestShelf(Vector2 pointerPosition, out Shelf shelf)
    {
        shelf = null;
        float bestDistance = float.PositiveInfinity;
        float bestDepth = float.PositiveInfinity;

        foreach (Shelf candidate in _shelfBoard.Shelves)
        {
            if (!candidate.isActiveAndEnabled || _shelfBoard.IsShelfLocked(candidate) || candidate.Capacity == 0 || !TryGetScreenRect(candidate.ColumnViews, out Rect screenRect))
                continue;

            screenRect.xMin -= _shelfPaddingPixels;
            screenRect.xMax += _shelfPaddingPixels;
            screenRect.yMin -= _shelfPaddingPixels;
            screenRect.yMax += _shelfPaddingPixels;

            if (!screenRect.Contains(pointerPosition))
                continue;

            float distance = Vector2.SqrMagnitude(screenRect.center - pointerPosition);
            float depth = _camera.WorldToScreenPoint(candidate.transform.position).z;

            if (depth <= 0f || depth > bestDepth || Mathf.Approximately(depth, bestDepth) && distance >= bestDistance)
                continue;

            shelf = candidate;
            bestDistance = distance;
            bestDepth = depth;
        }

        return shelf != null;
    }

    private bool TryGetNearestValidColumn(ShelfColumnView sourceColumn, Shelf shelf, Vector2 pointerPosition, out ShelfColumnView targetColumn)
    {
        targetColumn = null;

        if (shelf.Capacity == 0)
            return false;

        float bestDistance = float.PositiveInfinity;

        foreach (ShelfColumnView candidate in shelf.ColumnViews)
        {
            if (!_shelfBoard.CanMove(sourceColumn.Column, candidate.Column))
                continue;

            Vector3 screenPosition = _camera.WorldToScreenPoint(candidate.ItemAnchor.position);

            if (screenPosition.z <= 0f)
                continue;

            float distance = Vector2.SqrMagnitude((Vector2)screenPosition - pointerPosition);

            if (distance >= bestDistance)
                continue;

            targetColumn = candidate;
            bestDistance = distance;
        }

        return targetColumn != null;
    }

    private bool TryGetScreenRect(IReadOnlyList<ShelfColumnView> columnViews, out Rect screenRect)
    {
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasVisibleColumn = false;

        foreach (ShelfColumnView columnView in columnViews)
        {
            Vector3 screenPosition = _camera.WorldToScreenPoint(columnView.ItemAnchor.position);

            if (screenPosition.z <= 0f)
                continue;

            minimum = Vector2.Min(minimum, screenPosition);
            maximum = Vector2.Max(maximum, screenPosition);
            hasVisibleColumn = true;
        }

        screenRect = hasVisibleColumn ? Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y) : default;
        return hasVisibleColumn;
    }

    private bool ContainsShelf(Shelf shelf)
    {
        foreach (Shelf candidate in _shelfBoard.Shelves)
        {
            if (candidate == shelf)
                return true;
        }

        return false;
    }

    private static int CompareHits(RaycastHit first, RaycastHit second)
    {
        return first.distance.CompareTo(second.distance);
    }
}
