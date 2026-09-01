using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class ShelfLayerPool : MonoBehaviour
{
    [SerializeField] private ShelfLayer _oneSlotLayerPrefab;
    [SerializeField] private ShelfLayer _twoSlotLayerPrefab;
    [FormerlySerializedAs("_layerPrefab")]
    [SerializeField] private ShelfLayer _threeSlotLayerPrefab;
    [SerializeField] private ShelfLayer _fourSlotLayerPrefab;
    [SerializeField] private ShelfLayer _fiveSlotLayerPrefab;
    [SerializeField] private Transform _poolRoot;

    private readonly Dictionary<int, Queue<ShelfLayer>> _layersByCapacity = new Dictionary<int, Queue<ShelfLayer>>();
    private readonly HashSet<ShelfLayer> _pooledLayers = new HashSet<ShelfLayer>();

    public ShelfLayer Get(int capacity)
    {
        ShelfLayer layerPrefab = GetPrefab(capacity);
        Queue<ShelfLayer> layers = GetQueue(capacity);

        ShelfLayer layer;

        if (layers.Count > 0)
        {
            layer = layers.Dequeue();

            if (!_pooledLayers.Remove(layer))
                throw new InvalidOperationException(nameof(layer));
        }
        else
        {
            layer = Instantiate(layerPrefab, _poolRoot);
        }

        layer.ValidateCapacity(capacity);

        if (!layer.IsEmpty)
            throw new InvalidOperationException(nameof(layer));

        Transform layerTransform = layer.transform;
        layerTransform.SetParent(_poolRoot, false);
        layerTransform.localPosition = Vector3.zero;
        layerTransform.localRotation = Quaternion.identity;
        layerTransform.localScale = Vector3.one;
        layer.gameObject.SetActive(true);
        return layer;
    }

    public void Release(ShelfLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        if (!layer.IsEmpty)
            throw new InvalidOperationException(nameof(layer));

        int capacity = layer.Capacity;
        GetPrefab(capacity);

        ShelfLayerView layerView = layer.GetComponent<ShelfLayerView>();

        if (layerView == null)
            throw new InvalidOperationException(nameof(layerView));

        if (!_pooledLayers.Add(layer))
            throw new InvalidOperationException(nameof(layer));

        layerView.ResetForPool();
        Transform layerTransform = layer.transform;
        layerTransform.SetParent(_poolRoot, false);
        layerTransform.localPosition = Vector3.zero;
        layerTransform.localRotation = Quaternion.identity;
        layerTransform.localScale = Vector3.one;
        GetQueue(capacity).Enqueue(layer);
    }

    private Queue<ShelfLayer> GetQueue(int capacity)
    {
        if (!_layersByCapacity.TryGetValue(capacity, out Queue<ShelfLayer> layers))
        {
            layers = new Queue<ShelfLayer>();
            _layersByCapacity.Add(capacity, layers);
        }

        return layers;
    }

    private ShelfLayer GetPrefab(int capacity)
    {
        ShelfLayer layerPrefab = capacity switch
        {
            1 => _oneSlotLayerPrefab,
            2 => _twoSlotLayerPrefab,
            3 => _threeSlotLayerPrefab,
            4 => _fourSlotLayerPrefab,
            5 => _fiveSlotLayerPrefab,
            _ => throw new ArgumentOutOfRangeException(nameof(capacity))
        };

        if (layerPrefab == null)
            throw new InvalidOperationException($"Не назначен prefab слоя вместимостью {capacity}.");

        layerPrefab.ValidateCapacity(capacity);
        return layerPrefab;
    }
}
