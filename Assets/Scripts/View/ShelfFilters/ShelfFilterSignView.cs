using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ShelfFilterSignView : MonoBehaviour
{
    [SerializeField] private Shelf _shelf;
    [SerializeField] private GameObject _root;
    [SerializeField] private SpriteRenderer _plate;
    [SerializeField] private SpriteRenderer _frame;
    [SerializeField] private SpriteRenderer _leftNail;
    [SerializeField] private SpriteRenderer _rightNail;
    [SerializeField] private List<SpriteRenderer> _icons = new List<SpriteRenderer>();
    [SerializeField] private Color _dimmedTint = new Color(0.55f, 0.55f, 0.55f, 0.7f);
    [SerializeField, Min(0.01f)] private float _iconSize = 0.085f;
    [SerializeField, Min(0.01f)] private float _iconSpacing = 0.1f;
    [SerializeField, Min(0f)] private float _platePadding = 0.02f;
    [SerializeField, Min(0f)] private float _nailInset = 0.014f;
    [SerializeField, Min(0.01f)] private float _dimDuration = 0.12f;
    [SerializeField] private float _refusalHintAngle = 6f;
    [SerializeField, Min(0.01f)] private float _refusalHintDuration = 0.25f;
    [SerializeField] private float _rejectedAngle = 10f;
    [SerializeField, Min(0.01f)] private float _rejectedDuration = 0.35f;
    [SerializeField, Min(0f)] private float _iconHopHeight = 0.012f;
    [SerializeField, Min(0f)] private float _pulseStrength = 0.45f;
    [SerializeField, Min(0.01f)] private float _pulseDuration = 0.45f;
    [SerializeField, Min(1f)] private float _highlightScale = 1.3f;
    [SerializeField, Min(0.01f)] private float _highlightDuration = 0.12f;

    private readonly Dictionary<SpriteRenderer, Color> _baseColors = new Dictionary<SpriteRenderer, Color>();
    private readonly List<Tween> _colorTweens = new List<Tween>();
    private Vector3 _baseScale;
    private Vector3 _rootBaseScale;
    private Tween _swing;
    private Sequence _iconHop;
    private Tween _pulse;
    private Tween _highlight;

    public Shelf Shelf => _shelf;
    public bool IsDimmed { get; private set; }
    public bool IsHighlighted { get; private set; }
    public bool IsShown => _root.activeSelf;
    public Vector3 BottomCenter => new Vector3(_plate.bounds.center.x, _plate.bounds.min.y, _plate.bounds.center.z);

    private void Awake()
    {
        if (_shelf == null || _root == null || _plate == null || _frame == null || _leftNail == null || _rightNail == null || _icons.Count == 0 || _icons.Contains(null))
            throw new InvalidOperationException($"{name}: invalid shelf filter sign wiring.");

        foreach (SpriteRenderer spriteRenderer in new[] { _plate, _frame, _leftNail, _rightNail })
            _baseColors.Add(spriteRenderer, spriteRenderer.color);

        foreach (SpriteRenderer icon in _icons)
            _baseColors.Add(icon, icon.color);

        _baseScale = transform.localScale;
        _rootBaseScale = _root.transform.localScale;
    }

    public void Show(IReadOnlyList<Sprite> icons)
    {
        if (icons == null || icons.Count == 0 || icons.Count > _icons.Count)
            throw new ArgumentException($"{name}: expected 1 to {_icons.Count} icons.", nameof(icons));

        _root.SetActive(true);
        float plateWidth = icons.Count * _iconSpacing + _platePadding * 2f;
        SetWidth(_plate, plateWidth);
        SetWidth(_frame, plateWidth);
        SetNailX(_leftNail, -plateWidth * 0.5f + _nailInset);
        SetNailX(_rightNail, plateWidth * 0.5f - _nailInset);
        float firstIconX = -(icons.Count - 1) * _iconSpacing * 0.5f;

        for (int index = 0; index < _icons.Count; index++)
        {
            SpriteRenderer icon = _icons[index];
            bool isUsed = index < icons.Count;
            icon.gameObject.SetActive(isUsed);

            if (!isUsed)
                continue;

            icon.sprite = icons[index] != null ? icons[index] : throw new ArgumentException($"{name}: icon {index} is missing.", nameof(icons));
            Vector2 spriteSize = icon.sprite.bounds.size;
            icon.transform.localScale = Vector3.one * (_iconSize / Mathf.Max(spriteSize.x, spriteSize.y));
            icon.transform.localPosition = new Vector3(firstIconX + index * _iconSpacing, _plate.transform.localPosition.y, icon.transform.localPosition.z);
        }

        ApplyDimmed(false, 0f);
    }

    public void Hide()
    {
        KillTweens();
        IsDimmed = false;
        IsHighlighted = false;
        transform.localScale = _baseScale;
        _root.transform.localScale = _rootBaseScale;
        _root.transform.localRotation = Quaternion.identity;
        _root.SetActive(false);
    }

    public void SetDimmed(bool isDimmed)
    {
        if (IsDimmed == isDimmed || !IsShown)
            return;

        ApplyDimmed(isDimmed, _dimDuration);
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (IsHighlighted == isHighlighted || !IsShown)
            return;

        IsHighlighted = isHighlighted;
        _highlight?.Kill();
        _highlight = transform.DOScale(isHighlighted ? _baseScale * _highlightScale : _baseScale, _highlightDuration).SetLink(gameObject);
    }

    public void PlayPulse()
    {
        if (!IsShown)
            return;

        _pulse?.Kill();
        _root.transform.localScale = _rootBaseScale;
        _pulse = _root.transform.DOPunchScale(_rootBaseScale * _pulseStrength, _pulseDuration, 4, 0.5f).SetLink(gameObject);
    }

    public void PlayRefusalHint()
    {
        if (!IsShown || _swing != null && _swing.IsActive() && _swing.IsPlaying())
            return;

        PlaySwing(_refusalHintAngle, _refusalHintDuration);
    }

    public void PlayRejected()
    {
        if (!IsShown)
            return;

        _swing?.Complete();
        PlaySwing(_rejectedAngle, _rejectedDuration);
    }

    private static void SetWidth(SpriteRenderer spriteRenderer, float width)
    {
        spriteRenderer.size = new Vector2(width / spriteRenderer.transform.localScale.x, spriteRenderer.size.y);
    }

    private static void SetNailX(SpriteRenderer nail, float x)
    {
        Vector3 position = nail.transform.localPosition;
        nail.transform.localPosition = new Vector3(x, position.y, position.z);
    }

    private void ApplyDimmed(bool isDimmed, float duration)
    {
        IsDimmed = isDimmed;
        KillColorTweens();
        Color tint = isDimmed ? _dimmedTint : Color.white;

        foreach (KeyValuePair<SpriteRenderer, Color> baseColor in _baseColors)
            TweenColor(baseColor.Key, baseColor.Value * tint, duration);
    }

    private void TweenColor(SpriteRenderer target, Color color, float duration)
    {
        if (duration <= 0f)
        {
            target.color = color;
            return;
        }

        _colorTweens.Add(target.DOColor(color, duration).SetLink(gameObject));
    }

    private void PlaySwing(float angle, float duration)
    {
        _root.transform.localRotation = Quaternion.identity;
        _swing = _root.transform.DOPunchRotation(new Vector3(0f, 0f, angle), duration, 6, 0.5f).SetLink(gameObject);
        _iconHop?.Complete();
        _iconHop = DOTween.Sequence().SetLink(gameObject);

        foreach (SpriteRenderer icon in _icons)
        {
            if (icon.gameObject.activeSelf)
                _iconHop.Join(icon.transform.DOPunchPosition(Vector3.up * _iconHopHeight, duration, 4, 0.5f));
        }
    }

    private void KillColorTweens()
    {
        foreach (Tween tween in _colorTweens)
            tween.Kill();

        _colorTweens.Clear();
    }

    private void KillTweens()
    {
        KillColorTweens();
        _swing?.Kill();
        _iconHop?.Kill(true);
        _pulse?.Kill();
        _highlight?.Kill();
        _swing = null;
        _iconHop = null;
        _pulse = null;
        _highlight = null;
    }
}
