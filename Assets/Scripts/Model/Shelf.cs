using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Shelf : MonoBehaviour
{
    [SerializeField] private List<ShelfLayer> _shelfLayers;
    [SerializeField] private ShelfLayerStackView _layerStackView;

    private int _activeLayerIndex = 0;

    public IReadOnlyList<ShelfLayer> Layers => _shelfLayers;
    public ShelfLayer ActiveLayer => HasActiveLayer ? _shelfLayers[_activeLayerIndex] : null;

    public bool HasActiveLayer => _activeLayerIndex < _shelfLayers.Count;
    public bool IsCleared => _shelfLayers.All(layer => layer.IsEmpty);
    public bool HasNextLayer => _activeLayerIndex + 1 < _shelfLayers.Count;
    public bool CanRevealNextLayer => HasActiveLayer && HasNextLayer && ActiveLayer.IsEmpty;

    public void InitializeView()
    {
        _layerStackView.Initialize(_shelfLayers, _activeLayerIndex);
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

        _activeLayerIndex++;

        _layerStackView.Advance(_activeLayerIndex, completed);
    }

    public bool IsContainsActiveSlot(ShelfSlot slot)
    {
        return HasActiveLayer && ActiveLayer.IsContainsSlot(slot);
    }    
}
