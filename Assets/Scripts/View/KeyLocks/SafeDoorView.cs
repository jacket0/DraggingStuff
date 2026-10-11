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
    [SerializeField] private ParticleSystem _unlockFlash;
    [SerializeField] private ParticleSystem _barsDust;
    [SerializeField] private ParticleSystem _openSparkles;
    [SerializeField, Min(0.01f)] private float _unlockDuration = 0.12f;
    [SerializeField, Min(0f)] private float _unlockPunchScale = 0.25f;
    [SerializeField, Min(0.01f)] private float _lockDropDuration = 0.3f;
    [SerializeField, Min(0f)] private float _lockDrop = 0.2f;
    [SerializeField] private float _lockDropTilt = 30f;
    [SerializeField, Min(0.01f)] private float _liftDuration = 0.4f;
    [SerializeField] private float _barsTop = 0.235f;
    [SerializeField, Range(0.01f, 1f)] private float _rolledBarsScale = 0.08f;
    [SerializeField, Range(0f, 1f)] private float _liftStart = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _itemWaveStart = 0.6f;
    [SerializeField, Min(0f)] private float _itemHop = 0.04f;
    [SerializeField, Min(0.01f)] private float _itemHopDuration = 0.22f;
    [SerializeField, Min(0f)] private float _itemHopStagger = 0.06f;

    private Vector3 _closedBarsPosition;
    private Vector3 _closedBarsScale;
    private Vector3 _closedLockPosition;
    private Quaternion _closedLockRotation;
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

        if (_unlockFlash == null)
            throw new InvalidOperationException(nameof(_unlockFlash));

        if (_barsDust == null)
            throw new InvalidOperationException(nameof(_barsDust));

        if (_openSparkles == null)
            throw new InvalidOperationException(nameof(_openSparkles));

        _closedBarsPosition = _bars.localPosition;
        _closedBarsScale = _bars.localScale;
        _closedLockPosition = _lock.localPosition;
        _closedLockRotation = _lock.localRotation;
        _closedLockScale = _lock.localScale;
    }

    public void ShowClosed(KeyLockMark mark)
    {
        if (mark == null)
            throw new ArgumentNullException(nameof(mark));

        Cancel();
        _bars.localPosition = _closedBarsPosition;
        _bars.localScale = _closedBarsScale;
        _lock.localPosition = _closedLockPosition;
        _lock.localRotation = _closedLockRotation;
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
        opening.AppendCallback(() => _unlockFlash.Play(true));
        opening.Append(_lock.DOPunchScale(Vector3.one * _unlockPunchScale, _unlockDuration, 6, 0.5f));

        float dropStart = opening.Duration();
        opening.Insert(dropStart, _lock.DOLocalMove(_closedLockPosition + Vector3.down * _lockDrop, _lockDropDuration).SetEase(Ease.InQuad));
        opening.Insert(dropStart, _lock.DOLocalRotate(new Vector3(0f, 0f, _lockDropTilt), _lockDropDuration, RotateMode.LocalAxisAdd).SetEase(Ease.InQuad));
        opening.Insert(dropStart + _lockDropDuration * 0.5f, _lock.DOScale(Vector3.zero, _lockDropDuration * 0.5f).SetEase(Ease.InBack));

        float liftStart = dropStart + _lockDropDuration * _liftStart;
        opening.InsertCallback(liftStart, () => _barsDust.Play(true));
        Vector3 rolledPosition = _closedBarsPosition + Vector3.up * (_barsTop * _closedBarsScale.y * (1f - _rolledBarsScale));
        opening.Insert(liftStart, _bars.DOLocalMove(rolledPosition, _liftDuration).SetEase(Ease.OutCubic));
        opening.Insert(liftStart, _bars.DOScaleY(_closedBarsScale.y * _rolledBarsScale, _liftDuration).SetEase(Ease.OutCubic));

        float waveStart = liftStart + _liftDuration * _itemWaveStart;
        opening.InsertCallback(waveStart, () => _openSparkles.Play(true));
        InsertItemWave(opening, waveStart);

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

    private void InsertItemWave(Sequence opening, float startTime)
    {
        for (int columnIndex = 0; columnIndex < _shelf.Columns.Count; columnIndex++)
        {
            ShelfItem item = _shelf.Columns[columnIndex].FrontItem;

            if (item == null)
                continue;

            Transform itemTransform = item.transform;
            Vector3 punch = itemTransform.parent.InverseTransformVector(Vector3.up * _itemHop);
            opening.Insert(startTime + columnIndex * _itemHopStagger, itemTransform.DOPunchPosition(punch, _itemHopDuration, 1, 0f));
        }
    }

    private void OnDestroy() => Cancel();
}
