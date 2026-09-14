using UnityEngine;

public class ShelfColumnRaycaster : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _columnLayerMask;
    [SerializeField, Min(0f)] private float _shelfPaddingPixels = 64f;

    private ShelfDropTargetResolver _targetResolver;

    private void OnEnable()
    {
        ShelfBoard shelfBoard = GetComponent<ShelfBoard>();

        if (shelfBoard == null)
            throw new MissingComponentException(nameof(ShelfBoard));

        _targetResolver = new ShelfDropTargetResolver(_camera, shelfBoard, _columnLayerMask, _shelfPaddingPixels);
    }

    public bool TryGetColumn(ShelfColumnView sourceColumn, Vector2 pointerPosition, out ShelfColumnView targetColumn)
    {
        return _targetResolver.TryResolve(sourceColumn, pointerPosition, out targetColumn);
    }
}
