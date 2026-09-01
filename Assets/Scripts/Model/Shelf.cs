using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Shelf : MonoBehaviour
{
    public const int MinimumCapacity = 1;
    public const int MaximumCapacity = 5;
    public const int MinimumMatchCapacity = 3;

    [SerializeField, Range(MinimumCapacity, MaximumCapacity)] private int _capacity = 3;
    [SerializeField] private List<ShelfLayer> _shelfLayers;
    [SerializeField] private ShelfLayerStackView _layerStackView;
    [SerializeField] private Transform _dynamicLayerRoot;

    private bool _isViewInitialized;

    public int Capacity => _capacity;
    public IReadOnlyList<ShelfLayer> Layers => _shelfLayers;
    public ShelfLayer ActiveLayer => HasActiveLayer ? _shelfLayers[0] : null;

    public bool HasActiveLayer => _shelfLayers.Count > 0;
    public bool HasNextLayer => _shelfLayers.Count > 1;
    public bool IsCleared => _shelfLayers.All(layer => layer.IsEmpty);
    public bool CanRevealNextLayer => HasActiveLayer && HasNextLayer && ActiveLayer.IsEmpty;

    public event Action<ShelfLayer> LayerRemoved;

    public void InitializeView()
    {
        ValidateLayers();
        _layerStackView.Initialize(_shelfLayers);
        _isViewInitialized = true;
    }

    public static bool IsValidCapacity(int capacity)
    {
        return capacity >= MinimumCapacity && capacity <= MaximumCapacity;
    }

    public void ValidateLayers()
    {
        if (!IsValidCapacity(_capacity))
            throw new InvalidOperationException(nameof(_capacity));

        if (_shelfLayers == null)
            throw new InvalidOperationException(nameof(_shelfLayers));

        HashSet<ShelfLayer> uniqueLayers = new HashSet<ShelfLayer>();

        foreach (ShelfLayer layer in _shelfLayers)
        {
            if (layer == null || !uniqueLayers.Add(layer))
                throw new InvalidOperationException(nameof(_shelfLayers));

            layer.ValidateCapacity(_capacity);
        }
    }

    public IReadOnlyList<ShelfLayer> DetachLayers()
    {
        if (_isViewInitialized)
            throw new InvalidOperationException();

        ValidateLayers();

        ShelfLayer[] layers = _shelfLayers.ToArray();
        _shelfLayers.Clear();
        return layers;
    }

    public bool TryResolveMatch(out MatchResolution match)
    {
        match = null;

        if (!HasActiveLayer || !ActiveLayer.HasMatch())
            return false;

        match = ActiveLayer.TakeMatch();

        return true;
    }

    public void RevealNextLayer(Action completed)
    {
        if (!CanRevealNextLayer)
            throw new InvalidOperationException();

        ShelfLayer removedLayer = ActiveLayer;

        _layerStackView.Advance(_shelfLayers, () =>
        {
            _shelfLayers.RemoveAt(0);
            _layerStackView.UnregisterLayer(removedLayer);
            _layerStackView.Refresh(_shelfLayers);
            LayerRemoved?.Invoke(removedLayer);
            completed?.Invoke();
        });
    }

    public bool IsContainsActiveSlot(ShelfSlot slot)
    {
        return HasActiveLayer && ActiveLayer.IsContainsSlot(slot);
    }    

    public void HideActiveLayer()
    {
        if (!HasActiveLayer)
            throw new InvalidOperationException();

        _layerStackView.HideLayer(ActiveLayer);
    }

    public void AppendLayer(ShelfLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        if (_shelfLayers.Contains(layer))
            throw new InvalidOperationException(nameof(layer));

        layer.ValidateCapacity(_capacity);

        Transform layerRoot = _dynamicLayerRoot;

        if (layerRoot == null && HasActiveLayer)
            layerRoot = ActiveLayer.transform.parent;

        layer.transform.SetParent(layerRoot != null ? layerRoot : transform, false);
        _shelfLayers.Add(layer);

        if (_isViewInitialized)
        {
            _layerStackView.RegisterLayer(layer);
            _layerStackView.Refresh(_shelfLayers);
        }
    }
}
