using UnityEngine;

public class ShelfSlotRaycaster : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _slotLayerMask;
    [SerializeField, Min(0f)] private float _shelfPaddingPixels = 64f;

    private ShelfDropTargetResolver _targetResolver;

    private void OnEnable()
    {
        ShelfBoard shelfBoard = GetComponent<ShelfBoard>();

        if (shelfBoard == null)
            throw new MissingComponentException(nameof(ShelfBoard));

        _targetResolver = new ShelfDropTargetResolver(_camera, shelfBoard, _slotLayerMask, _shelfPaddingPixels);
    }

    public bool TryGetSlot(ShelfSlot sourceSlot, Vector2 pointerPosition, out ShelfSlot targetSlot)
    {
        return _targetResolver.TryResolve(sourceSlot, pointerPosition, out targetSlot);
    }
}
