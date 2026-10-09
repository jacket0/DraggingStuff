using UnityEngine;

public sealed class ShelfKey : MonoBehaviour
{
    public Shelf LockedShelf { get; internal set; }
    public ShelfColumn Column { get; internal set; }

    public void Delete() => Destroy(gameObject);
}
