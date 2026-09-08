using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShelfDropTargetResolver
{
    private readonly Camera _camera;
    private readonly ShelfBoard _shelfBoard;
    private readonly LayerMask _slotLayerMask;
    private readonly float _shelfPaddingPixels;

    public ShelfDropTargetResolver(Camera camera, ShelfBoard shelfBoard, LayerMask slotLayerMask, float shelfPaddingPixels)
    {
        _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
        _shelfBoard = shelfBoard != null ? shelfBoard : throw new ArgumentNullException(nameof(shelfBoard));
        _slotLayerMask = slotLayerMask;
        _shelfPaddingPixels = Mathf.Max(0f, shelfPaddingPixels);
    }

    public bool TryResolve(ShelfSlot sourceSlot, Vector2 pointerPosition, out ShelfSlot targetSlot)
    {
        if (sourceSlot == null)
            throw new ArgumentNullException(nameof(sourceSlot));

        if (TryGetDirectShelf(pointerPosition, out Shelf directShelf))
            return TryGetNearestValidSlot(sourceSlot, directShelf, pointerPosition, out targetSlot);

        if (!TryGetNearestShelf(pointerPosition, out Shelf nearestShelf))
        {
            targetSlot = null;
            return false;
        }

        return TryGetNearestValidSlot(sourceSlot, nearestShelf, pointerPosition, out targetSlot);
    }

    private bool TryGetDirectShelf(Vector2 pointerPosition, out Shelf shelf)
    {
        Ray ray = _camera.ScreenPointToRay(pointerPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, _slotLayerMask, QueryTriggerInteraction.Collide);
        Array.Sort(hits, CompareHits);

        foreach (RaycastHit hit in hits)
        {
            ShelfSlot slot = hit.collider.GetComponentInParent<ShelfSlot>();
            Shelf candidate = slot != null ? slot.GetComponentInParent<Shelf>() : null;

            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsContainsActiveSlot(slot) || !ContainsShelf(candidate))
                continue;

            shelf = candidate;
            return true;
        }

        shelf = null;
        return false;
    }

    private bool TryGetNearestShelf(Vector2 pointerPosition, out Shelf shelf)
    {
        shelf = null;
        float bestDistance = float.PositiveInfinity;
        float bestDepth = float.PositiveInfinity;

        foreach (Shelf candidate in _shelfBoard.Shelves)
        {
            if (!candidate.isActiveAndEnabled || !candidate.HasActiveLayer || !TryGetScreenRect(candidate.ActiveLayer.Slots, out Rect screenRect))
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

    private bool TryGetNearestValidSlot(ShelfSlot sourceSlot, Shelf shelf, Vector2 pointerPosition, out ShelfSlot targetSlot)
    {
        targetSlot = null;

        if (!shelf.HasActiveLayer)
            return false;

        float bestDistance = float.PositiveInfinity;

        foreach (ShelfSlot candidate in shelf.ActiveLayer.Slots)
        {
            if (!_shelfBoard.CanMove(sourceSlot, candidate))
                continue;

            Vector3 screenPosition = _camera.WorldToScreenPoint(candidate.transform.position);

            if (screenPosition.z <= 0f)
                continue;

            float distance = Vector2.SqrMagnitude((Vector2)screenPosition - pointerPosition);

            if (distance >= bestDistance)
                continue;

            targetSlot = candidate;
            bestDistance = distance;
        }

        return targetSlot != null;
    }

    private bool TryGetScreenRect(IReadOnlyList<ShelfSlot> slots, out Rect screenRect)
    {
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasVisibleSlot = false;

        foreach (ShelfSlot slot in slots)
        {
            Vector3 screenPosition = _camera.WorldToScreenPoint(slot.transform.position);

            if (screenPosition.z <= 0f)
                continue;

            minimum = Vector2.Min(minimum, screenPosition);
            maximum = Vector2.Max(maximum, screenPosition);
            hasVisibleSlot = true;
        }

        screenRect = hasVisibleSlot ? Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y) : default;
        return hasVisibleSlot;
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
