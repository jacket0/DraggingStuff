using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ConveyorBeltView : MonoBehaviour
{
    private static readonly int MainTextureTransformId = Shader.PropertyToID("_MainTex_ST");

    [SerializeField] private Shelf _shelf;
    [SerializeField] private List<Renderer> _belts = new List<Renderer>();
    [SerializeField] private List<ConveyorRoller> _rollers = new List<ConveyorRoller>();
    [SerializeField] private GameObject _idleDeck;
    [SerializeField] private float _beltStepUv = 0.25f;
    [SerializeField] private float _rollerStepDegrees = 90f;

    private MaterialPropertyBlock _propertyBlock;
    private float[] _beltOffsets;
    private Tween _shift;

    public Shelf Shelf => _shelf;

    private void Awake()
    {
        if (_shelf == null)
            throw new InvalidOperationException($"{name}: {nameof(_shelf)} is required.");

        if (_belts.Count != 0 && _belts.Count != _shelf.Capacity || _belts.Contains(null))
            throw new InvalidOperationException($"{name}: one belt renderer per shelf column is required.");

        if (_rollers.Exists(roller => roller == null || !roller.IsValidFor(_belts.Count)))
            throw new InvalidOperationException($"{name}: every roller needs a transform and an existing lane.");

        _propertyBlock = new MaterialPropertyBlock();
        _beltOffsets = new float[_belts.Count];
    }

    public void SetRunning(bool isRunning)
    {
        foreach (Renderer belt in _belts)
            belt.gameObject.SetActive(isRunning);

        foreach (ConveyorRoller roller in _rollers)
            roller.Transform.gameObject.SetActive(isRunning);

        if (_idleDeck != null)
            _idleDeck.SetActive(!isRunning);
    }

    public void PlayShift(IReadOnlyList<int> lanes, float duration, Action completed)
    {
        if (lanes == null)
            throw new ArgumentNullException(nameof(lanes));

        _shift?.Kill(true);
        float[] startOffsets = (float[])_beltOffsets.Clone();
        Quaternion[] startRotations = _rollers.ConvertAll(roller => roller.Transform.localRotation).ToArray();
        Tween shift = null;
        shift = DOTween.To(() => 0f, progress => ApplyShift(lanes, startOffsets, startRotations, progress), 1f, duration)
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

    private void ApplyShift(IReadOnlyList<int> lanes, float[] startOffsets, Quaternion[] startRotations, float progress)
    {
        foreach (int lane in lanes)
        {
            if (lane >= _belts.Count)
                continue;

            _beltOffsets[lane] = Mathf.Repeat(startOffsets[lane] + _beltStepUv * progress, 1f);
            ApplyBeltOffset(_belts[lane], _beltOffsets[lane]);
        }

        for (int index = 0; index < _rollers.Count; index++)
        {
            if (!Contains(lanes, _rollers[index].Lane))
                continue;

            _rollers[index].Transform.localRotation = startRotations[index] * Quaternion.AngleAxis(_rollerStepDegrees * progress, Vector3.right);
        }
    }

    private void ApplyBeltOffset(Renderer belt, float offset)
    {
        Vector2 scale = belt.sharedMaterial != null ? belt.sharedMaterial.mainTextureScale : Vector2.one;
        belt.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetVector(MainTextureTransformId, new Vector4(scale.x, scale.y, 0f, offset));
        belt.SetPropertyBlock(_propertyBlock);
    }

    private static bool Contains(IReadOnlyList<int> lanes, int lane)
    {
        for (int index = 0; index < lanes.Count; index++)
        {
            if (lanes[index] == lane)
                return true;
        }

        return false;
    }
}

[Serializable]
public sealed class ConveyorRoller
{
    [SerializeField] private Transform _transform;
    [SerializeField, Min(0)] private int _lane;

    public Transform Transform => _transform;
    public int Lane => _lane;

    public bool IsValidFor(int laneCount) => _transform != null && _lane < laneCount;
}
