using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BonusMenuCardAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform _visualRoot;
    [SerializeField] private RectTransform _titlePanel;
    [SerializeField] private CanvasGroup _titleCanvasGroup;
    [SerializeField] private RectTransform _descriptionPanel;
    [SerializeField] private CanvasGroup _descriptionCanvasGroup;
    [SerializeField, Min(0f)] private float _titleOffset = 130f;
    [SerializeField, Min(0f)] private float _descriptionOffset = 140f;
    [SerializeField, Min(1f)] private float _expandedScale = 1.05f;
    [SerializeField, Min(0f)] private float _duration = 0.22f;

    private Vector2 _titleHiddenPosition;
    private Vector2 _descriptionHiddenPosition;
    private Sequence _animation;

    private void Awake()
    {
        _titleHiddenPosition = _titlePanel.anchoredPosition;
        _descriptionHiddenPosition = _descriptionPanel.anchoredPosition;
        ApplyCollapsedState();
    }

    private void OnDisable()
    {
        StopAnimation();
        ApplyCollapsedState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayExpandedState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PlayCollapsedState();
    }

    private void PlayExpandedState()
    {
        StopAnimation();

        _animation = DOTween.Sequence()
            .Join(_titlePanel.DOAnchorPosY(_titleHiddenPosition.y + _titleOffset, _duration).SetEase(Ease.OutCubic))
            .Join(_titleCanvasGroup.DOFade(1f, _duration))
            .Join(_descriptionPanel.DOAnchorPosY(_descriptionHiddenPosition.y - _descriptionOffset, _duration).SetEase(Ease.OutCubic))
            .Join(_descriptionCanvasGroup.DOFade(1f, _duration))
            .Join(_visualRoot.DOScale(_expandedScale, _duration).SetEase(Ease.OutBack));
    }

    private void PlayCollapsedState()
    {
        StopAnimation();

        _animation = DOTween.Sequence()
            .Join(_titlePanel.DOAnchorPos(_titleHiddenPosition, _duration).SetEase(Ease.InCubic))
            .Join(_titleCanvasGroup.DOFade(0f, _duration))
            .Join(_descriptionPanel.DOAnchorPos(_descriptionHiddenPosition, _duration).SetEase(Ease.InCubic))
            .Join(_descriptionCanvasGroup.DOFade(0f, _duration))
            .Join(_visualRoot.DOScale(1f, _duration).SetEase(Ease.OutCubic));
    }

    private void StopAnimation()
    {
        _animation?.Kill();
        _animation = null;
    }

    private void ApplyCollapsedState()
    {
        _titlePanel.anchoredPosition = _titleHiddenPosition;
        _titleCanvasGroup.alpha = 0f;

        _descriptionPanel.anchoredPosition = _descriptionHiddenPosition;
        _descriptionCanvasGroup.alpha = 0f;

        _visualRoot.localScale = Vector3.one;
    }
}
