using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfItemPool : MonoBehaviour
{
    [SerializeField] private ShelfItemCatalog _catalog;
    [SerializeField] private Transform _poolRoot;

    private readonly Dictionary<ItemType, Queue<ShelfItem>> _itemsByType = new Dictionary<ItemType, Queue<ShelfItem>>();
    private readonly HashSet<ShelfItem> _issuedItems = new HashSet<ShelfItem>();

    private void Awake()
    {
        _catalog.Validate();

        foreach (ShelfItemCatalog.Entry entry in _catalog.Entries)
            _itemsByType.Add(entry.Type, new Queue<ShelfItem>());
    }

    public ShelfItem Get(ItemType type)
    {
        if (!_itemsByType.TryGetValue(type, out Queue<ShelfItem> items))
            throw new InvalidOperationException($"Unknown pooled item type: {type}");

        ShelfItem item = items.Count > 0 ? items.Dequeue() : Instantiate(_catalog.GetPrefab(type), _poolRoot);
        ResetItem(item);
        _issuedItems.Add(item);
        item.PrepareForUse(Release);
        return item;
    }

    private void Release(ShelfItem item)
    {
        if (item == null || item.Column != null || !_issuedItems.Remove(item))
            throw new InvalidOperationException("Only an issued item without a column can return to the pool.");

        DOTween.Kill(item.transform);
        item.transform.SetParent(_poolRoot, false);
        ResetItem(item);
        item.gameObject.SetActive(false);
        _itemsByType[item.Type].Enqueue(item);
    }

    private static void ResetItem(ShelfItem item)
    {
        item.GetComponent<ShelfItemPresentation>().Reset();
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = Vector3.one;
    }
}
