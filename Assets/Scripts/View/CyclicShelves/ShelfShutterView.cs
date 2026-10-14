using System;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfShutterView : MonoBehaviour
{
    [SerializeField] private Shelf _shelf;
    [SerializeField] private GameObject _housing;
    [SerializeField] private Transform _curtain;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float _openDuration = 0.3f;
    [SerializeField, Range(0.01f, 1f)] private float _rolledScale = 0.05f;
    [SerializeField, Min(0f)] private float _shudderStrength = 0.015f;
    [SerializeField, Min(0.01f)] private float _shudderDuration = 0.25f;

    private Vector3 _closedCurtainPosition;
    private Vector3 _closedCurtainScale;
    private Tween _switch;
    private Tween _shudder;

    public Shelf Shelf => _shelf;
    public bool IsSwitching => _switch != null;
    public bool IsCurtainDown => _curtain.gameObject.activeSelf;
    public Vector3 CurtainScale => _curtain.localScale;

    private void Awake()
    {
        if (_shelf == null)
            throw new InvalidOperationException(nameof(_shelf));

        if (_housing == null)
            throw new InvalidOperationException(nameof(_housing));

        if (_curtain == null)
            throw new InvalidOperationException(nameof(_curtain));

        _closedCurtainPosition = _curtain.localPosition;
        _closedCurtainScale = _curtain.localScale;
    }

    public void ShowOpen()
    {
        Cancel();
        _housing.SetActive(true);
        _curtain.gameObject.SetActive(false);
    }

    public void ShowClosed()
    {
        Cancel();
        _housing.SetActive(true);
        _curtain.localScale = _closedCurtainScale;
        _curtain.gameObject.SetActive(true);
    }

    public void HideForLevel()
    {
        Cancel();
        _housing.SetActive(false);
        _curtain.gameObject.SetActive(false);
    }

    public void PlayClose(Action completed)
    {
        Cancel();
        _curtain.localScale = GetRolledScale();
        _curtain.gameObject.SetActive(true);
        PlaySwitch(_curtain.DOScaleY(_closedCurtainScale.y, _closeDuration).SetEase(Ease.OutBounce), completed);
    }

    public void PlayOpen(Action completed)
    {
        Cancel();
        _curtain.localScale = _closedCurtainScale;
        _curtain.gameObject.SetActive(true);
        PlaySwitch(_curtain.DOScaleY(GetRolledScale().y, _openDuration).SetEase(Ease.InCubic), () =>
        {
            _curtain.gameObject.SetActive(false);
            completed?.Invoke();
        });
    }

    public void PlayShudder()
    {
        if (_switch != null || _shudder != null || !_curtain.gameObject.activeSelf)
            return;

        _shudder = _curtain.DOPunchPosition(Vector3.right * _shudderStrength, _shudderDuration, 10, 0.5f)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() =>
            {
                _shudder = null;
                _curtain.localPosition = _closedCurtainPosition;
            });
    }

    public void Cancel()
    {
        _shudder?.Kill();
        Tween switchTween = _switch;
        _switch = null;
        switchTween?.Kill();
    }

    private void PlaySwitch(Tween tween, Action completed)
    {
        _switch = tween;
        tween.SetLink(gameObject, LinkBehaviour.KillOnDestroy).OnComplete(() =>
        {
            if (_switch != tween)
                return;

            _switch = null;
            completed?.Invoke();
        });
    }

    private Vector3 GetRolledScale() => new Vector3(_closedCurtainScale.x, _closedCurtainScale.y * _rolledScale, _closedCurtainScale.z);

    private void OnDestroy() => Cancel();
}
