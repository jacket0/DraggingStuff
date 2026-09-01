using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EndlessItemCatalog", menuName = "Game/Endless/Item Catalog")]
public sealed class EndlessItemCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private ShelfItem _prefab;
        [SerializeField, Min(0.01f)] private float _weight = 1f;

        public ItemType Type => _type;
        public ShelfItem Prefab => _prefab;
        public float Weight => _weight;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => _entries;

    public ShelfItem GetPrefab(ItemType type)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.Type == type)
                return entry.Prefab;
        }

        throw new InvalidOperationException(type.ToString());
    }

    public void Validate()
    {
        if (_entries.Count == 0)
            throw new InvalidOperationException(nameof(_entries));

        HashSet<ItemType> types = new HashSet<ItemType>();

        foreach (Entry entry in _entries)
        {
            if (entry == null || entry.Prefab == null || entry.Weight <= 0f)
                throw new InvalidOperationException(nameof(_entries));

            if (entry.Prefab.Type != entry.Type || !types.Add(entry.Type))
                throw new InvalidOperationException(entry.Type.ToString());
        }
    }
}
