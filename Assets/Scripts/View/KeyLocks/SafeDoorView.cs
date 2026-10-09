using System;
using DG.Tweening;
using UnityEngine;

public sealed class SafeDoorView : MonoBehaviour
{
    [SerializeField] private Shelf _shelf;
    [SerializeField] private GameObject _doorRoot;
    [SerializeField] private Transform _bars;
    [SerializeField] private Transform _lock;
    [SerializeField] private Transform _markPoint;
    [SerializeField, Min(0.01f)] private float _unlockDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float _liftDuration = 0.35f;
    [SerializeField, Min(0f)] private float _barsLift = 0.4f;

    private Vector3 _closedBarsPosition;
    private Vector3 _closedLockScale;
    private GameObject _mark;
    private Sequence _opening;

    public Shelf Shelf => _shelf;
    public Transform LockPoint => _lock;

    private void Awake()
    {
        if (_shelf == null)
            throw new InvalidOperationException(nameof(_shelf));

        if (_doorRoot == null)
            throw new InvalidOperationException(nameof(_doorRoot));

        if (_bars == null)
            throw new InvalidOperationException(nameof(_bars));

        if (_lock == null)
            throw new InvalidOperationException(nameof(_lock));

        if (_markPoint == null)
            throw new InvalidOperationException(nameof(_markPoint));

        _closedBarsPosition = _bars.localPosition;
        _closedLockScale = _lock.localScale;
    }

    public void ShowClosed(KeyLockMark mark)
    {
        if (mark == null)
            throw new ArgumentNullException(nameof(mark));

        Cancel();
        _bars.localPosition = _closedBarsPosition;
        _lock.localScale = _closedLockScale;

        if (_mark != null)
            Destroy(_mark);

        _mark = mark.Attach(_markPoint);
        _doorRoot.SetActive(true);
    }

    public void PlayOpen(Action completed)
    {
        Cancel();
        Sequence opening = DOTween.Sequence();
        _opening = opening;
        opening.Append(_lock.DOPunchRotation(new Vector3(0f, 0f, 90f), _unlockDuration, 6, 0.5f));
        opening.Append(_lock.DOScale(Vector3.zero, _unlockDuration).SetEase(Ease.InBack));
        opening.Append(_bars.DOLocalMove(_closedBarsPosition + Vector3.up * _barsLift, _liftDuration).SetEase(Ease.InCubic));
        opening.OnComplete(() =>
        {
            if (_opening != opening)
                return;

            _opening = null;
            ShowOpened();
            completed?.Invoke();
        });
        opening.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void ShowOpened()
    {
        Cancel();
        _doorRoot.SetActive(false);
    }

    public void HideForLevel() => ShowOpened();

    public void Cancel()
    {
        Sequence opening = _opening;
        _opening = null;
        opening?.Kill();
    }

    private void OnDestroy() => Cancel();
}
