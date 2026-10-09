using System;
using UnityEngine;

public sealed class ShelfKey : MonoBehaviour
{
    [SerializeField] private Transform _markPoint;

    public Shelf LockedShelf { get; private set; }
    public ShelfColumn Column { get; internal set; }
    public Transform MarkPoint => _markPoint;

    public void Bind(Shelf lockedShelf)
    {
        if (LockedShelf != null)
            throw new InvalidOperationException($"{name}: the key already opens {LockedShelf.name}.");

        LockedShelf = lockedShelf != null ? lockedShelf : throw new ArgumentNullException(nameof(lockedShelf));
    }

    public void Delete() => Destroy(gameObject);
}
