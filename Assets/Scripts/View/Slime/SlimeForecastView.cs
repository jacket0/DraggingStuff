using System;
using DG.Tweening;
using UnityEngine;

public sealed class SlimeForecastView : MonoBehaviour
{
    [SerializeField] private Transform _drop;
    [SerializeField] private MeshFilter _digit;
    [SerializeField] private MeshFilter _digitOutline;
    [SerializeField] private Mesh[] _digitMeshes;
    [SerializeField, Min(0.01f)] private float _pulseDuration = 0.5f;
    [SerializeField, Min(0f)] private float _pulseStrength = 0.25f;

    private Vector3 _restScale;
    private Tween _pulse;

    public int MovesToGrowth { get; private set; }
    public int MaxMovesToGrowth => _digitMeshes.Length;

    private void Awake()
    {
        if (_drop == null)
            throw new InvalidOperationException(nameof(_drop));

        if (_digit == null)
            throw new InvalidOperationException(nameof(_digit));

        if (_digitOutline == null)
            throw new InvalidOperationException(nameof(_digitOutline));

        if (_digitMeshes == null || _digitMeshes.Length == 0)
            throw new InvalidOperationException(nameof(_digitMeshes));

        _restScale = _drop.localScale;
    }

    private void OnDestroy() => _pulse?.Kill();

    public void Show(Vector3 position, int movesToGrowth)
    {
        if (movesToGrowth < 1 || movesToGrowth > MaxMovesToGrowth)
            throw new ArgumentOutOfRangeException(nameof(movesToGrowth));

        transform.position = position;
        gameObject.SetActive(true);
        MovesToGrowth = movesToGrowth;
        Mesh mesh = _digitMeshes[movesToGrowth - 1];
        _digit.sharedMesh = mesh;
        _digitOutline.sharedMesh = mesh;
        StopPulse();

        if (movesToGrowth == 1)
            _pulse = _drop.DOScale(_restScale * (1f + _pulseStrength), _pulseDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    public void Hide()
    {
        StopPulse();
        MovesToGrowth = 0;
        gameObject.SetActive(false);
    }

    private void StopPulse()
    {
        _pulse?.Kill();
        _pulse = null;
        _drop.localScale = _restScale;
    }
}
