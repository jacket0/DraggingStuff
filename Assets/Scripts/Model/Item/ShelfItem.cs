using System;
using UnityEngine;

public class ShelfItem : MonoBehaviour
{
    [SerializeField] private ItemType _type;

    private Action<ShelfItem> _releaseAction;

    public ItemType Type => _type;

    public void Delete()
    {
        Action<ShelfItem> releaseAction = _releaseAction;
        _releaseAction = null;

        if (releaseAction != null)
        {
            releaseAction.Invoke(this);
            return;
        }

        Destroy(gameObject);
    }

    public void PrepareForUse(Action<ShelfItem> releaseAction)
    {
        _releaseAction = releaseAction ?? throw new ArgumentNullException(nameof(releaseAction));
        gameObject.SetActive(true);
    }
}
