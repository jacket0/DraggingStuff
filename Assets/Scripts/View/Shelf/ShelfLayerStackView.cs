using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ShelfLayerStackView : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _moveDuration = 0.3f;
    [SerializeField] private Ease _moveEase = Ease.OutCubic;
    [SerializeField] private bool _useConfiguredPositions;
    [SerializeField] private Vector3 _configuredActiveLocalPosition;
    [SerializeField] private Vector3 _configuredPreviewLocalPosition;

    private readonly Dictionary<ShelfLayer, ShelfLayerView> _layerViews = new Dictionary<ShelfLayer, ShelfLayerView>();

    private Vector3 _activeLocalPosition;
    private Vector3 _previewLocalPosition;
    private bool _isInitialized;
    private Sequence _activeTransition;

    public void Initialize(IReadOnlyList<ShelfLayer> layers)
    {
        if (layers == null)
            throw new ArgumentNullException(nameof(layers));

        if (layers.Count == 0)
            throw new InvalidOperationException();

        _activeLocalPosition = _useConfiguredPositions
            ? _configuredActiveLocalPosition
            : layers[0].transform.localPosition;

        _previewLocalPosition = _useConfiguredPositions
            ? _configuredPreviewLocalPosition
            : layers.Count > 1
                ? layers[1].transform.localPosition
                : _activeLocalPosition;

        _layerViews.Clear();

        foreach (ShelfLayer layer in layers)
            RegisterLayer(layer);

        _isInitialized = true;
        Refresh(layers);
    }

    public void RegisterLayer(ShelfLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        if (_layerViews.ContainsKey(layer))
            throw new InvalidOperationException(nameof(layer));

        ShelfLayerView layerView = layer.GetComponent<ShelfLayerView>();

        if (layerView == null)
            throw new InvalidOperationException(nameof(layerView));

        layerView.Initialize();
        _layerViews.Add(layer, layerView);

        if (_isInitialized)
            layerView.Hide();
    }

    public void UnregisterLayer(ShelfLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        _layerViews.Remove(layer);
    }

    public void Refresh(IReadOnlyList<ShelfLayer> layers)
    {
        if (layers == null)
            throw new ArgumentNullException(nameof(layers));

        for (int index = 0; index < layers.Count; index++)
        {
            ShelfLayer layer = layers[index];

            if (!_layerViews.TryGetValue(layer, out ShelfLayerView layerView))
                throw new InvalidOperationException(nameof(layer));

            if (index == 0)
            {
                layer.transform.localPosition = _activeLocalPosition;
                layerView.ShowActive();
            }
            else if (index == 1)
            {
                layer.transform.localPosition = _previewLocalPosition;
                layerView.ShowPreview();
            }
            else
            {
                layerView.Hide();
            }
        }
    }

    public void Advance(IReadOnlyList<ShelfLayer> layers, Action completed)
    {
        if (layers == null)
            throw new ArgumentNullException(nameof(layers));

        if (layers.Count < 2)
            throw new InvalidOperationException();

        _activeTransition?.Kill(true);

        ShelfLayer activeLayer = layers[0];
        ShelfLayer nextLayer = layers[1];

        HideLayer(activeLayer);
        _layerViews[nextLayer].ShowActive();

        Sequence transition = DOTween.Sequence();
        transition.Append(nextLayer.transform.DOLocalMove(_activeLocalPosition, _moveDuration).SetEase(_moveEase));

        transition.OnComplete(() =>
        {
            if (_activeTransition == transition)
                _activeTransition = null;

            completed?.Invoke();
        });

        _activeTransition = transition;
        transition.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void HideLayer(ShelfLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        if (!_layerViews.TryGetValue(layer, out ShelfLayerView layerView))
            throw new InvalidOperationException(nameof(layer));

        layerView.Hide();
    }
}
