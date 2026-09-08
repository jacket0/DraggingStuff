using UnityEngine;

public sealed class ShelfItemTargetView : MonoBehaviour
{
    private Renderer[] _renderers;
    private Collider[] _colliders;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    public bool TryGetBounds(out Bounds bounds)
    {
        if (_renderers == null || _colliders == null)
            Awake();

        bool hasBounds = false;
        bounds = default;

        foreach (Renderer itemRenderer in _renderers)
        {
            if (itemRenderer == null || !itemRenderer.enabled || !itemRenderer.gameObject.activeInHierarchy ||
                !(itemRenderer is MeshRenderer || itemRenderer is SkinnedMeshRenderer))
                continue;

            if (!hasBounds)
            {
                bounds = itemRenderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(itemRenderer.bounds);
        }

        if (hasBounds)
            return true;

        foreach (Collider itemCollider in _colliders)
        {
            if (itemCollider == null || !itemCollider.enabled || !itemCollider.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = itemCollider.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(itemCollider.bounds);
        }

        return hasBounds;
    }
}
