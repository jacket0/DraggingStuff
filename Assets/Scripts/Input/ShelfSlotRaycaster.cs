using UnityEngine;

public class ShelfSlotRaycaster : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _slotLayerMask;

    public bool TryGetSlot(Vector2 pointerPosition, out ShelfSlot slot)
    {
        Ray ray = _camera.ScreenPointToRay(pointerPosition);

        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _slotLayerMask, QueryTriggerInteraction.Collide);

        if (!hasHit)
        {
            slot = null;
            return false;
        }

        slot = hit.collider.GetComponentInParent<ShelfSlot>();
        return slot != null;
    }
}
