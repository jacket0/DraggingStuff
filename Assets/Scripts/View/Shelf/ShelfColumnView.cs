using System;
using UnityEngine;

public sealed class ShelfColumnView : MonoBehaviour
{
    [SerializeField] private Transform _itemAnchor;
    [SerializeField] private Collider _dropCollider;

    public Transform ItemAnchor => _itemAnchor;
    public Collider DropCollider => _dropCollider;
    public ShelfColumn Column { get; private set; }
    public Shelf Shelf { get; private set; }
    public ShelfItem FrontItem => Column?.FrontItem;
    public bool IsEmpty => Column != null && Column.IsEmpty;

    public void Bind(Shelf shelf, ShelfColumn column)
    {
        if (_itemAnchor == null || _dropCollider == null)
            throw new InvalidOperationException($"{name}: column anchor and drop collider are required.");

        if (Column != null)
            throw new InvalidOperationException($"{name}: column is already bound.");

        Shelf = shelf != null ? shelf : throw new ArgumentNullException(nameof(shelf));
        Column = column ?? throw new ArgumentNullException(nameof(column));
    }

    public void AttachFront(ShelfItem item)
    {
        if (item == null || item != FrontItem)
            throw new InvalidOperationException("Only the column's front item can be attached.");

        item.transform.SetParent(_itemAnchor, true);
    }
}
