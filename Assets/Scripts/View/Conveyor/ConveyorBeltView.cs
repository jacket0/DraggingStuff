using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ConveyorBeltView : MonoBehaviour
{
    private static readonly int MainTextureTransformId = Shader.PropertyToID("_MainTex_ST");

    [SerializeField] private Shelf _shelf;
    [SerializeField] private List<Renderer> _belts = new List<Renderer>();
    [SerializeField] private float _beltStepUv = 2f;
    [SerializeField] private Transform _shelfColumns;
    [SerializeField] private Vector3 _stoppedColumnsOffset = new Vector3(0f, -0.065f, 0f);
    [SerializeField] private Vector3 _stoppedPreviewOffset = new Vector3(0f, 0f, -0.2f);
    [SerializeField, Min(1)] private int _stoppedVisibleDepth = 2;
    [SerializeField, Range(0.1f, 1f)] private float _stoppedDepthScaleFactor = 1f;

    private MaterialPropertyBlock _propertyBlock;
    private float[] _beltOffsets;
    private Vector3 _runningColumnsPosition;
    private Tween _shift;

    public Shelf Shelf => _shelf;

    private void Awake()
    {
        if (_shelf == null)
            throw new InvalidOperationException($"{name}: {nameof(_shelf)} is required.");

        if (_shelfColumns == null)
            throw new InvalidOperationException($"{name}: {nameof(_shelfColumns)} is required.");

        if (_belts.Count != _shelf.Capacity || _belts.Contains(null))
            throw new InvalidOperationException($"{name}: one belt renderer per shelf column is required.");

        _propertyBlock = new MaterialPropertyBlock();
        _beltOffsets = new float[_belts.Count];
        _runningColumnsPosition = _shelfColumns.localPosition;
    }

    public void SetRunning(bool isRunning)
    {
        gameObject.SetActive(isRunning);

        if (isRunning)
        {
            _shelfColumns.localPosition = _runningColumnsPosition;
            return;
        }

        _shelfColumns.localPosition = _runningColumnsPosition + _stoppedColumnsOffset;
        _shelf.View.ApplyQueueLayout(_stoppedPreviewOffset, _stoppedVisibleDepth, _stoppedDepthScaleFactor);
    }

    public void PlayShift(IReadOnlyList<int> lanes, float duration, Action completed)
    {
        if (lanes == null)
            throw new ArgumentNullException(nameof(lanes));

        _shift?.Kill(true);
        float[] startOffsets = (float[])_beltOffsets.Clone();
        Tween shift = null;
        shift = DOTween.To(() => 0f, progress => ApplyShift(lanes, startOffsets, progress), 1f, duration)
            .SetEase(Ease.InOutSine)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                if (_shift == shift)
                    _shift = null;

                completed?.Invoke();
            });
        _shift = shift;
    }

    private void ApplyShift(IReadOnlyList<int> lanes, float[] startOffsets, float progress)
    {
        foreach (int lane in lanes)
        {
            _beltOffsets[lane] = Mathf.Repeat(startOffsets[lane] + _beltStepUv * progress, 1f);
            ApplyBeltOffset(_belts[lane], _beltOffsets[lane]);
        }
    }

    private void ApplyBeltOffset(Renderer belt, float offset)
    {
        Vector2 scale = belt.sharedMaterial != null ? belt.sharedMaterial.mainTextureScale : Vector2.one;
        belt.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetVector(MainTextureTransformId, new Vector4(scale.x, scale.y, 0f, offset));
        belt.SetPropertyBlock(_propertyBlock);
    }
}
