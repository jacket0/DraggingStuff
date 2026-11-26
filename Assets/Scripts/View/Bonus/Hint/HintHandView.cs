using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class HintHandView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _hand;
    [SerializeField] private Image _shadow;
    [SerializeField] private Image _pressRing;
    [SerializeField] private Sprite _openSprite;
    [SerializeField] private Sprite _grabSprite;
    [SerializeField] private Camera _camera;
    [SerializeField] private Vector2 _screenOffset;
    [SerializeField, Min(0.01f)] private float _appearDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float _pressDuration = 0.15f;
    [SerializeField, Range(0.5f, 1f)] private float _pressScale = 0.85f;
    [SerializeField, Range(0.1f, 1f)] private float _appearScale = 0.7f;
    [SerializeField, Min(0f)] private float _dropHeight = 40f;
    [SerializeField] private Vector2 _shadowOffset = new Vector2(10f, -12f);
    [SerializeField] private Vector2 _pressedShadowOffset = new Vector2(4f, -5f);
    [SerializeField, Min(0.01f)] private float _ringDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float _ringScale = 1.4f;
    [SerializeField, Min(0f)] private float _maxTilt = 15f;
    [SerializeField, Min(0f)] private float _tiltPerSpeed = 0.012f;
    [SerializeField, Min(0f)] private float _tiltSmoothing = 12f;

    private RectTransform _root;
    private RectTransform _handTransform;
    private RectTransform _shadowTransform;
    private RectTransform _ringTransform;
    private Transform _target;
    private Vector2 _currentShadowOffset;
    private Vector2 _lastRootPosition;
    private float _tilt;
    private Color _ringColor;

    public float AppearDuration => _appearDuration;
    public float PressDuration => _pressDuration;

    private void Awake()
    {
        if (_canvasGroup == null)
            throw new InvalidOperationException(nameof(_canvasGroup));

        if (_hand == null)
            throw new InvalidOperationException(nameof(_hand));

        if (_shadow == null)
            throw new InvalidOperationException(nameof(_shadow));

        if (_pressRing == null)
            throw new InvalidOperationException(nameof(_pressRing));

        if (_openSprite == null)
            throw new InvalidOperationException(nameof(_openSprite));

        if (_grabSprite == null)
            throw new InvalidOperationException(nameof(_grabSprite));

        if (_camera == null)
            throw new InvalidOperationException(nameof(_camera));

        _root = (RectTransform)transform;
        _handTransform = _hand.rectTransform;
        _shadowTransform = _shadow.rectTransform;
        _ringTransform = _pressRing.rectTransform;
        _ringColor = _pressRing.color;
        HideImmediately();
    }

    private void OnDisable()
    {
        HideImmediately();
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        HintBubblePlacement.PlaceAt(_root, _camera, _target.position, _screenOffset);
        UpdateTilt();
        UpdateShadow();
    }

    public void Show(Transform target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        HideImmediately();
        _target = target;
        HintBubblePlacement.PlaceAt(_root, _camera, _target.position, _screenOffset);
        _lastRootPosition = _root.anchoredPosition;
        _hand.sprite = _openSprite;
        _hand.enabled = true;
        _shadow.enabled = true;
        _handTransform.anchoredPosition = new Vector2(0f, _dropHeight);
        _handTransform.localScale = Vector3.one * _appearScale;
        _currentShadowOffset = _shadowOffset;
        UpdateShadow();

        Animate(_canvasGroup.DOFade(1f, _appearDuration));
        Animate(_handTransform.DOAnchorPos(Vector2.zero, _appearDuration).SetEase(Ease.OutQuad));
        Animate(_handTransform.DOScale(1f, _appearDuration).SetEase(Ease.OutBack));
    }

    public void Grab()
    {
        _hand.sprite = _grabSprite;
        Animate(_handTransform.DOScale(_pressScale, _pressDuration).SetEase(Ease.OutQuad));
        AnimateShadowOffset(_pressedShadowOffset);
        PlayRing();
    }

    public void Release()
    {
        _hand.sprite = _openSprite;
        Animate(_handTransform.DOScale(1f, _pressDuration).SetEase(Ease.OutBack));
        AnimateShadowOffset(_shadowOffset);
        PlayRing();
    }

    public void Hide()
    {
        Animate(_canvasGroup.DOFade(0f, _appearDuration).OnComplete(HideImmediately));
        Animate(_handTransform.DOAnchorPos(new Vector2(0f, _dropHeight * 0.6f), _appearDuration).SetEase(Ease.InQuad));
        Animate(_handTransform.DOScale(_appearScale, _appearDuration).SetEase(Ease.InQuad));
    }

    public void HideImmediately()
    {
        DOTween.Kill(this);
        _target = null;
        _tilt = 0f;
        _canvasGroup.alpha = 0f;
        _hand.enabled = false;
        _shadow.enabled = false;
        _pressRing.enabled = false;
        _handTransform.anchoredPosition = Vector2.zero;
        _handTransform.localScale = Vector3.one;
        _handTransform.localRotation = Quaternion.identity;
    }

    private void UpdateTilt()
    {
        Vector2 position = _root.anchoredPosition;

        if (Time.deltaTime > 0f)
        {
            float speed = (position.x - _lastRootPosition.x) / Time.deltaTime;
            float targetTilt = Mathf.Clamp(-speed * _tiltPerSpeed, -_maxTilt, _maxTilt);
            _tilt = Mathf.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-_tiltSmoothing * Time.deltaTime));
        }

        _lastRootPosition = position;
        _handTransform.localRotation = Quaternion.Euler(0f, 0f, _tilt);
    }

    private void UpdateShadow()
    {
        _shadow.sprite = _hand.sprite;
        _shadowTransform.anchoredPosition = _handTransform.anchoredPosition + _currentShadowOffset;
        _shadowTransform.localScale = _handTransform.localScale;
        _shadowTransform.localRotation = _handTransform.localRotation;
    }

    private void AnimateShadowOffset(Vector2 offset)
    {
        Animate(DOTween.To(() => _currentShadowOffset, value => _currentShadowOffset = value, offset, _pressDuration));
    }

    private void PlayRing()
    {
        _pressRing.enabled = true;
        _pressRing.color = _ringColor;
        _ringTransform.localScale = Vector3.one * 0.3f;
        Animate(_ringTransform.DOScale(_ringScale, _ringDuration).SetEase(Ease.OutQuad));
        Animate(_pressRing.DOFade(0f, _ringDuration).SetEase(Ease.InQuad));
    }

    private void Animate(Tween tween)
    {
        tween.SetId(this).SetLink(gameObject);
    }
}
