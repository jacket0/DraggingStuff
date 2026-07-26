using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Shelf : MonoBehaviour
{
    [SerializeField] private List<ShelfLayer> _shelfLayers;

    private int _activeLayerIndex = 0;

    public bool HasActiveLayer => _activeLayerIndex < _shelfLayers.Count;
    public ShelfLayer ActiveLayer => HasActiveLayer ? _shelfLayers[_activeLayerIndex] : null;

    private void Awake()
    {
        for (int i = 0; i < _shelfLayers.Count; i++)
            _shelfLayers[i].gameObject.SetActive(i == _activeLayerIndex);
    }

    public bool TryResolveMatch()
    {
        if (!HasActiveLayer)
            return false;

        bool hasMatch = ActiveLayer.HasMatch();

        if (hasMatch)
            ActiveLayer.ReleaseItems();
        
        if (ActiveLayer.IsEmpty)
            AdvanceToNextLayer();

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
