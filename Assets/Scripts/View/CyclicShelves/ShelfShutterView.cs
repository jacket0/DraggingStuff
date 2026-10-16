using System;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfShutterView : MonoBehaviour
{
    private static readonly int ClipHeightId = Shader.PropertyToID("_ClipHeight");

    [SerializeField] private Shelf _shelf;
    [SerializeField] private GameObject _housing;
    [SerializeField] private Transform _curtain;
    [SerializeField] private Renderer[] _curtainRenderers = Array.Empty<Renderer>();
    [SerializeField, Min(0.01f)] private float _curtainHeight = 0.38f;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float _openDuration = 0.3f;
    [SerializeField, Min(0f)] private float _closingWarningDrop = 0.07f;
    [SerializeField, Min(0f)] private float _openingWarningLift = 0.018f;
    [SerializeField, Min(0.01f)] private float _warningDuration = 0.25f;
    [SerializeField, Min(0f)] private float _shudderStrength = 0.015f;
    [SerializeField, Min(0.01f)] private float _shudderDuration = 0.25f;

    private Vector3 _closedCurtainPosition;
    private float _lift;
    private bool _isClosed;
    private bool _isWarning;
    private Tween _switch;
    private Tween _rest;
    private Tween _shudder;

    public Shelf Shelf => _shelf;
    public bool IsSwitching => _switch != null;
    public bool IsClosed => _isClosed;
    public float ClosedFraction => _curtain.gameObject.activeSelf ? 1f - _lift / _curtainHeight : 0f;

    private void Awake()
    {
        if (_shelf == null)
            throw new InvalidOperationException(nameof(_shelf));

        if (_housing == null)
            throw new InvalidOperationException(nameof(_housing));

        if (_curtain == null)
            throw new InvalidOperationException(nameof(_curtain));

        if (_curtainRenderers.Length == 0 || Array.IndexOf(_curtainRenderers, null) >= 0)
            throw new InvalidOperationException(nameof(_curtainRenderers));

        _closedCurtainPosition = _curtain.localPosition;
        SetClipHeight();
    }

    public void ShowOpen()
    {
        Cancel();
        _housing.SetActive(true);
        _isClosed = false;
        _isWarning = false;
        SetLift(_curtainHeight);
    }

    public void ShowClosed()
    {
        Cancel();
        _housing.SetActive(true);
        _isClosed = true;
        _isWarning = false;
        SetLift(0f);
    }

    public void HideForLevel()
    {
        Cancel();
        _housing.SetActive(false);
        _isClosed = false;
        _isWarning = false;
        _curtain.gameObject.SetActive(false);
    }

    public void SetWarning(bool isWarning)
    {
        if (_isWarning == isWarning || !_housing.activeSelf)
            return;

        _isWarning = isWarning;

        if (_switch != null)
            return;

        _rest?.Kill();
        _rest = AnimateLift(GetRestLift(), _warningDuration, Ease.OutQuad).OnKill(() => _rest = null);
    }

    public void PlayClose(Action completed)
    {
        Cancel();
        _isClosed = true;
        _isWarning = false;
        PlaySwitch(AnimateLift(0f, _closeDuration, Ease.OutBounce), completed);
    }

    public void PlayOpen(Action completed)
    {
        Cancel();
        _isClosed = false;
        _isWarning = false;
        PlaySwitch(AnimateLift(_curtainHeight, _openDuration, Ease.InCubic), completed);
    }

    public void PlayShudder()
    {
        if (_switch != null || _rest != null || _shudder != null || !_isClosed)
            return;

        _shudder = _curtain.DOPunchPosition(Vector3.right * _shudderStrength, _shudderDuration, 10, 0.5f)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() =>
            {
                _shudder = null;
                SetLift(_lift);
            });
    }

    public void Cancel()
    {
        _shudder?.Kill();
        _rest?.Kill();
        Tween switchTween = _switch;
        _switch = null;
        switchTween?.Kill();
    }

    private Tween AnimateLift(float target, float duration, Ease ease)
    {
        _curtain.gameObject.SetActive(true);
        return DOTween.To(() => _lift, SetLift, target, duration)
            .SetEase(ease)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void PlaySwitch(Tween tween, Action completed)
    {
        _switch = tween;
        tween.OnComplete(() =>
        {
            if (_switch != tween)
                return;

            _switch = null;
            completed?.Invoke();
        });
    }

    private float GetRestLift()
    {
        if (_isClosed)
            return _isWarning ? _openingWarningLift : 0f;

        return _isWarning ? _curtainHeight - _closingWarningDrop : _curtainHeight;
    }

    private void SetLift(float lift)
    {
        _lift = lift;
        _curtain.localPosition = _closedCurtainPosition + Vector3.up * lift;
        _curtain.gameObject.SetActive(lift < _curtainHeight);
    }

    private void SetClipHeight()
    {
        MaterialPropertyBlock properties = new MaterialPropertyBlock();

        foreach (Renderer curtainRenderer in _curtainRenderers)
        {
            curtainRenderer.GetPropertyBlock(properties);
            properties.SetFloat(ClipHeightId, _curtain.parent.TransformPoint(_closedCurtainPosition).y);
            curtainRenderer.SetPropertyBlock(properties);
        }
    }

    private void OnDestroy() => Cancel();
}
