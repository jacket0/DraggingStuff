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
    }

    public void Advance(int completedLayerIndex, int activeLayerIndex, Action completed)
    {
        _layers[completedLayerIndex].gameObject.SetActive(false);
        _layers[activeLayerIndex].gameObject.SetActive(true);
        Sequence sequence = DOTween.Sequence();

        for (int i = activeLayerIndex; i < _layers.Count; i++)
        {
            int positionIndex = i - activeLayerIndex;

            Transform layerTransform = _layers[i].transform;
            Vector3 targetPosition = _initialLocalPositions[positionIndex];

            sequence.Join(layerTransform.DOLocalMove(targetPosition, _moveDuration).SetEase(_moveEase));
        }

        sequence.OnComplete(() => completed?.Invoke());
    }
}
