using UnityEngine;

public class ShelfItem : MonoBehaviour
{
    [SerializeField] private ItemType _type;

    public ItemType Type => _type;

    public void Delete()
    {
        Destroy(gameObject);
    }
}
