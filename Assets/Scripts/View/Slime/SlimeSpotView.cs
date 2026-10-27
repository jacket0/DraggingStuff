using System;
using DG.Tweening;
using UnityEngine;

public sealed class SlimeSpotView : MonoBehaviour
{
    [SerializeField] private Transform _blob;
    [SerializeField] private GameObject _puddle;
    [SerializeField] private Transform _wallPuddle;
    [SerializeField] private Transform _bridge;
    [SerializeField, Min(0.01f)] private float _appearDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float _vanishDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float _bridgeDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float _wobbleDuration = 1.2f;
    [SerializeField, Min(0f)] private float _wobbleStrength = 0.08f;
    [SerializeField, Min(0f)] private float _clenchStrength = 0.3f;
    [SerializeField, Min(0.01f)] private float _clenchDuration = 0.5f;

    private Vector3 _restScale;
    private Vector3 _bridgeRestScale;
    private Tween _transition;
    private Tween _wobble;
    private Tween _bridgeTween;
    private bool _isVanishing;
    private bool _isBridgeShown;

    public bool IsVisible => gameObject.activeSelf && _transition == null;
    public bool IsAnimating => _transition != null;
    public bool IsPresent => gameObject.activeSelf && !_isVanishing;

    private void Awake()
    {
        if (_blob == null)
            throw new InvalidOperationException(nameof(_blob));

        if (_puddle == null)
            throw new InvalidOperationException(nameof(_puddle));

        if (_wallPuddle == null)
            throw new InvalidOperationException(nameof(_wallPuddle));

        if (_bridge == null)
            throw new InvalidOperationException(nameof(_bridge));

        _restScale = _blob.localScale;
        _bridgeRestScale = _bridge.localScale;
        _bridge.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Cancel();
        ResetBridge();
    }

    public void SetWall(float direction)
    {
        bool hasWall = direction != 0f;
        _puddle.SetActive(!hasWall);
        _wallPuddle.gameObject.SetActive(hasWall);

        if (hasWall)
        {
            Vector3 scale = _wallPuddle.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction > 0f ? 1f : -1f);
            _wallPuddle.localScale = scale;
        }
    }

    public void SetBridge(bool shown, Vector3 localCenter)
    {
        if (shown == _isBridgeShown)
            return;

        _isBridgeShown = shown;
        _bridgeTween?.Kill();

        if (!shown)
        {
            _bridge.gameObject.SetActive(false);
            return;
        }

        _bridge.localPosition = localCenter;
        _bridge.gameObject.SetActive(true);
        _bridge.localScale = Vector3.zero;
        _bridgeTween = _bridge.DOScale(_bridgeRestScale, _bridgeDuration).SetEase(Ease.OutSine);
    }

    public void ShowInstant()
    {
        Cancel();
        ResetBridge();
        _isVanishing = false;
        gameObject.SetActive(true);
        _blob.localPosition = Vector3.zero;
        _blob.localScale = _restScale;
        StartWobble();
    }

    public void PlayAppear(Vector3 spreadFrom, Action completed)
    {
        Cancel();
        ResetBridge();
        _isVanishing = false;
        gameObject.SetActive(true);
        _blob.localPosition = spreadFrom;
        _blob.localScale = Vector3.zero;
        Sequence growth = DOTween.Sequence();
        growth.Join(_blob.DOScale(_restScale, _appearDuration).SetEase(Ease.OutBack));
        growth.Join(_blob.DOLocalMove(Vector3.zero, _appearDuration).SetEase(Ease.OutSine));
        _transition = growth.OnComplete(() =>
        {
            _transition = null;
            StartWobble();
            completed?.Invoke();
        });
    }

    public void PlayVanish(float delay = 0f)
    {
        if (!gameObject.activeSelf)
            return;

        Cancel();
        ResetBridge();
        _isVanishing = true;
        _transition = _blob.DOScale(Vector3.zero, _vanishDuration).SetDelay(delay).SetEase(Ease.InBack).OnComplete(() =>
        {
            _transition = null;
            _isVanishing = false;
            gameObject.SetActive(false);
        });
    }

    public void PlayClench()
    {
        if (!gameObject.activeSelf || _transition != null)
            return;

        Cancel();
        _blob.localScale = _restScale;
        _transition = _blob.DOPunchScale(_restScale * _clenchStrength, _clenchDuration, 5, 0.5f).OnComplete(() =>
        {
            _transition = null;
            StartWobble();
        });
    }

    public void Hide()
    {
        Cancel();
        ResetBridge();
        _isVanishing = false;
        gameObject.SetActive(false);
    }

    private void StartWobble()
    {
        Vector3 squashedScale = Vector3.Scale(_restScale, new Vector3(1f, 1f - _wobbleStrength, 1f));
        _wobble = _blob.DOScale(squashedScale, _wobbleDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    private void Cancel()
    {
        _transition?.Kill();
        _transition = null;
        _wobble?.Kill();
        _wobble = null;
    }

    private void ResetBridge()
    {
        _bridgeTween?.Kill();
        _bridgeTween = null;
        _isBridgeShown = false;

        if (_bridge != null)
            _bridge.gameObject.SetActive(false);
    }
}
