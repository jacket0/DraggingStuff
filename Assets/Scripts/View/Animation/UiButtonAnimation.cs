using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class UiButtonAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform _visualRoot;
    [SerializeField, Min(1f)] private float _hoverScale = 1.03f;
    [SerializeField, Range(0.5f, 1f)] private float _pressedScale = 0.96f;
    [SerializeField, Min(0.01f)] private float _duration = 0.1f;

    private Vector3 _initialScale = Vector3.one;
    private Tween _animation;
    private bool _isPointerInside;
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();

        if (_visualRoot == null)
            throw new MissingReferenceException(nameof(_visualRoot));

        _initialScale = _visualRoot.localScale;
    }

    private void OnDisable()
    {
        StopAnimation();
        _isPointerInside = false;

        if (_visualRoot != null)
            _visualRoot.localScale = _initialScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerInside = true;

        if (!IsTouch(eventData))
            AnimateTo(_hoverScale, Ease.OutCubic);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerInside = false;
        AnimateTo(1f, Ease.OutCubic);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            AnimateTo(_pressedScale, Ease.OutCubic);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        AnimateTo(_isPointerInside && !IsTouch(eventData) ? _hoverScale : 1f, Ease.OutCubic);
    }

    private void AnimateTo(float scaleMultiplier, Ease ease)
    {
        StopAnimation();

        if (_button != null && !_button.IsInteractable())
        {
            _visualRoot.localScale = _initialScale;
            return;
        }

        _animation = _visualRoot
            .DOScale(_initialScale * scaleMultiplier, _duration)
            .SetEase(ease)
            .SetUpdate(true);
    }

    private void StopAnimation()
    {
        _animation?.Kill();
        _animation = null;
    }

    private static bool IsTouch(PointerEventData eventData)
    {
        return eventData.pointerId >= 0;
    }
}
