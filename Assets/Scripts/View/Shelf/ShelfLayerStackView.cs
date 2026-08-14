using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ShelfLayerStackView : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _moveDuration = 0.3f;
    [SerializeField] private Ease _moveEase = Ease.OutCubic;

    private IReadOnlyList<ShelfLayer> _layers;
    private Vector3[] _initialLocalPositions;
    private ShelfLayerView[] _layerViews;
    private Sequence _activeTransition;

    public void Initialize(IReadOnlyList<ShelfLayer> layers, int activeLayerIndex)
    {
        if (layers == null)
            throw new ArgumentNullException(nameof(layers));

        if (layers.Count == 0)
            throw new InvalidOperationException();

        _layers = layers;
        _initialLocalPositions = new Vector3[layers.Count];

        for (int i = 0; i < layers.Count; i++)
        {
            ShelfLayer layer = layers[i];

            if (layer is null)
                throw new InvalidOperationException(nameof(layer));

            _initialLocalPositions[i] = layer.transform.localPosition;
            layer.gameObject.SetActive(i == activeLayerIndex);
        }

        _layerViews = new ShelfLayerView[layers.Count];

        for (int i = 0; i < layers.Count; i++)
        {
            ShelfLayerView layerView = _layers[i].GetComponent<ShelfLayerView>();

            if (layerView == null)
                throw new InvalidOperationException(nameof(layerView));

            _layerViews[i] = layerView;
            layerView.Initialize();
        }

        ApplyLayerStates(activeLayerIndex);
    }

    public void Advance(int activeLayerIndex, Action completed)
    {
        _activeTransition?.Complete();

        ApplyLayerStates(activeLayerIndex);

        Sequence transition = DOTween.Sequence();

        for (int i = activeLayerIndex; i < _layers.Count; i++)
        {
            int positionIndex = i - activeLayerIndex;

            transition.Join(_layers[i].transform.DOLocalMove(_initialLocalPositions[positionIndex], _moveDuration).SetEase(_moveEase));
        }

        _activeTransition = transition;

        transition.OnComplete(() =>
        {
            if (_activeTransition == transition)
                _activeTransition = null;

            completed?.Invoke();
        });

        transition.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void HideLayer(int index)
    {
        if (index < 0 || index >= _layerViews.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        _layerViews[index].Hide();
    }


    private void ApplyLayerStates(int activeLayerIndex)
    {
        for (int i = 0; i < _layerViews.Length; i++)
        {
            if (activeLayerIndex == i)
            {
                _layerViews[i].ShowActive();
                continue;
            }

            if (activeLayerIndex + 1 == i)
            {
                _layerViews[i].ShowPreview();
                continue;
            }

            _layerViews[i].Hide();
        }
    }
}
