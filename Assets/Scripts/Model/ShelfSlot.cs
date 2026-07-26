using System;
using UnityEngine;

public class ShelfSlot : MonoBehaviour
{
    [SerializeField] private ShelfItem _shelfItem;
    [SerializeField] private Transform _itemAnchor;

    public ShelfItem Item => _shelfItem;
    public bool IsEmpty => _shelfItem == null;


    public ShelfItem TakeItem()
    {
        if (IsEmpty)
            throw new InvalidOperationException(nameof(_shelfItem));

        var item = _shelfItem;
        _shelfItem = null;
        return item;
    }

    public void PlaceItem(ShelfItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (!IsEmpty)
            throw new InvalidOperationException(nameof(_shelfItem));

        _shelfItem = item;
        item.transform.SetParent(_itemAnchor, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
    }
}
