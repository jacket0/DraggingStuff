using System;
using UnityEngine;

public sealed class ShelfItemTargetResolver
{
    private readonly Camera _camera;
    private readonly ShelfBoard _shelfBoard;
    private readonly float _selectionPaddingPixels;

    public ShelfItemTargetResolver(Camera camera, ShelfBoard shelfBoard, float selectionPaddingPixels)
    {
        _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
        _shelfBoard = shelfBoard != null ? shelfBoard : throw new ArgumentNullException(nameof(shelfBoard));
        _selectionPaddingPixels = Mathf.Max(0f, selectionPaddingPixels);
    }

    public bool TryResolve(Vector2 pointerPosition, out ShelfItem item)
    {
        item = null;
        float bestBoundsDistance = float.PositiveInfinity;
        float bestDepth = float.PositiveInfinity;

        foreach (Shelf shelf in _shelfBoard.Shelves)
        {
            if (!shelf.isActiveAndEnabled || !shelf.HasActiveLayer)
                continue;

            foreach (ShelfSlot slot in shelf.ActiveLayer.Slots)
            {
                if (slot.IsEmpty || !slot.Item.gameObject.activeInHierarchy || !TryGetScreenRect(slot.Item, out Rect screenRect, out float depth))
                    continue;

                Vector2 nearestPoint = new Vector2(
                    Mathf.Clamp(pointerPosition.x, screenRect.xMin, screenRect.xMax),
                    Mathf.Clamp(pointerPosition.y, screenRect.yMin, screenRect.yMax));
                float boundsDistance = Vector2.SqrMagnitude(nearestPoint - pointerPosition);

                if (boundsDistance > _selectionPaddingPixels * _selectionPaddingPixels)
                    continue;

                if (!IsBetterCandidate(boundsDistance, depth, bestBoundsDistance, bestDepth))
                    continue;

                item = slot.Item;
                bestBoundsDistance = boundsDistance;
                bestDepth = depth;
            }
        }

        return item != null;
    }

    private bool TryGetScreenRect(ShelfItem item, out Rect screenRect, out float depth)
    {
        ShelfItemTargetView targetView = item.GetComponent<ShelfItemTargetView>();

        if (targetView == null)
            throw new MissingReferenceException($"{item.name}: {nameof(ShelfItemTargetView)}");

        if (!targetView.TryGetBounds(out Bounds bounds))
        {
            screenRect = default;
            depth = 0f;
            return false;
        }

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        depth = float.PositiveInfinity;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 screenPosition = _camera.WorldToScreenPoint(corner);

                    if (screenPosition.z <= 0f)
                        continue;

                    minimum = Vector2.Min(minimum, screenPosition);
                    maximum = Vector2.Max(maximum, screenPosition);
                    depth = Mathf.Min(depth, screenPosition.z);
                }
            }
        }

        if (float.IsPositiveInfinity(depth))
        {
            screenRect = default;
            return false;
        }

        screenRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        return true;
    }

    private static bool IsBetterCandidate(
        float boundsDistance,
        float depth,
        float bestBoundsDistance,
        float bestDepth)
    {
        if (!Mathf.Approximately(boundsDistance, bestBoundsDistance))
            return boundsDistance < bestBoundsDistance;

        return depth < bestDepth;
    }
}
