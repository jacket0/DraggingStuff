using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Shelf : MonoBehaviour
{
    [SerializeField] private List<ShelfLayer> _shelfLayers;

    private int _activeLayerIndex = 0;

    public bool HasActiveLayer => _activeLayerIndex < _shelfLayers.Count;
    public bool IsCleared => _shelfLayers.All(layer => layer.IsEmpty);
    public ShelfLayer ActiveLayer => HasActiveLayer ? _shelfLayers[_activeLayerIndex] : null;
    public bool HasNextLayer => _activeLayerIndex + 1 < _shelfLayers.Count;

    private void Awake()
    {
        for (int i = 0; i < _shelfLayers.Count; i++)
            _shelfLayers[i].gameObject.SetActive(i == _activeLayerIndex);
    }

    public bool TryRevealNextLayer()
    {
        if (!HasActiveLayer)
            return false;

        if (!ActiveLayer.IsEmpty)
            return false;

        if (!HasNextLayer)
            return false;

        AdvanceToNextLayer();
        return true;
    }

    public bool TryResolveMatch()
    {
        if (!HasActiveLayer)
            return false;

        bool hasMatch = ActiveLayer.HasMatch();

        if (hasMatch)
            ActiveLayer.ReleaseItems();
        
        return hasMatch;
    }

    private void AdvanceToNextLayer()
    {
        ShelfLayer completedLayer = ActiveLayer;
        completedLayer.gameObject.SetActive(false);

        _activeLayerIndex++;

        if (HasActiveLayer)
            ActiveLayer.gameObject.SetActive(true);
    }
}
