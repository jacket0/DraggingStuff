using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfItemPool : MonoBehaviour
{
    [SerializeField] private EndlessItemCatalog _catalog;
    [SerializeField] private Transform _poolRoot;

    private readonly Dictionary<ItemType, Queue<ShelfItem>> _itemsByType = new Dictionary<ItemType, Queue<ShelfItem>>();
    private readonly Dictionary<ShelfItem, ItemState> _states = new Dictionary<ShelfItem, ItemState>();

    private void Awake()
    {
        if (_catalog == null)
            throw new InvalidOperationException(nameof(_catalog));

        _catalog.Validate();

        foreach (EndlessItemCatalog.Entry entry in _catalog.Entries)
            _itemsByType.Add(entry.Type, new Queue<ShelfItem>());
    }

    public ShelfItem Get(ItemType type)
    {
        if (!_itemsByType.TryGetValue(type, out Queue<ShelfItem> items))
            throw new InvalidOperationException(type.ToString());

        ShelfItem item;

        if (items.Count > 0)
        {
            item = items.Dequeue();
        }
        else
        {
            item = Instantiate(_catalog.GetPrefab(type), _poolRoot);
            _states.Add(item, ItemState.Capture(item));
        }

        ItemState state = _states[item];
        state.Restore(item);
        item.PrepareForUse(Release);
        return item;
    }

    private void Release(ShelfItem item)
    {
        if (item == null || !_states.TryGetValue(item, out ItemState state))
            throw new InvalidOperationException(nameof(item));

        DOTween.Kill(item.transform);
        state.Restore(item);
        item.transform.SetParent(_poolRoot, false);
        item.gameObject.SetActive(false);
        _itemsByType[item.Type].Enqueue(item);
    }

    private sealed class ItemState
    {
        private readonly Renderer[] _renderers;
        private readonly Material[][] _materials;
        private readonly Collider[] _colliders;
        private readonly bool[] _colliderStates;

        private ItemState(Renderer[] renderers, Material[][] materials, Collider[] colliders, bool[] colliderStates)
        {
            _renderers = renderers;
            _materials = materials;
            _colliders = colliders;
            _colliderStates = colliderStates;
        }

        public static ItemState Capture(ShelfItem item)
        {
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            Material[][] materials = new Material[renderers.Length][];

            for (int index = 0; index < renderers.Length; index++)
                materials[index] = renderers[index].sharedMaterials;

            Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
            bool[] colliderStates = new bool[colliders.Length];

            for (int index = 0; index < colliders.Length; index++)
                colliderStates[index] = colliders[index].enabled;

            return new ItemState(renderers, materials, colliders, colliderStates);
        }

        public void Restore(ShelfItem item)
        {
            for (int index = 0; index < _renderers.Length; index++)
                _renderers[index].sharedMaterials = _materials[index];

            for (int index = 0; index < _colliders.Length; index++)
                _colliders[index].enabled = _colliderStates[index];

            Transform itemTransform = item.transform;
            itemTransform.localPosition = Vector3.zero;
            itemTransform.localRotation = Quaternion.identity;
            itemTransform.localScale = Vector3.one;
        }
    }
}
