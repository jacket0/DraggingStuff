using System;
using DG.Tweening;
using UnityEngine;

public sealed class RefillStoppedToastView : MonoBehaviour
{
    [SerializeField] private ClosedShelvesController _closedShelves;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField, Min(0f)] private float _hiddenOffset = 220f;
    [SerializeField, Min(0.01f)] private float _showDuration = 0.35f;
    [SerializeField, Min(0f)] private float _holdDuration = 2f;
    [SerializeField, Min(0.01f)] private float _hideDuration = 0.3f;

    private Vector2 _shownPosition;
    private Sequence _sequence;

    private void Awake()
    {
        if (_closedShelves == null || _panel == null || _canvasGroup == null)
            throw new InvalidOperationException("Invalid refill toast wiring.");

        _shownPosition = _panel.anchoredPosition;
        SetHidden();
    }

    private void OnEnable() => _closedShelves.AllShelvesOpened += Show;

    private void OnDisable()
    {
        _closedShelves.AllShelvesOpened -= Show;
        _sequence?.Kill();
    }

    private void Show()
    {
        _sequence?.Kill();
        SetHidden();
        _panel.gameObject.SetActive(true);

        _sequence = DOTween.Sequence()
            .Append(_panel.DOAnchorPos(_shownPosition, _showDuration).SetEase(Ease.OutBack))
            .Join(_canvasGroup.DOFade(1f, _showDuration))
            .AppendInterval(_holdDuration)
            .Append(_panel.DOAnchorPos(HiddenPosition, _hideDuration).SetEase(Ease.InQuad))
            .Join(_canvasGroup.DOFade(0f, _hideDuration))
            .OnComplete(() => _panel.gameObject.SetActive(false))
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private Vector2 HiddenPosition => _shownPosition + Vector2.up * _hiddenOffset;

    private void SetHidden()
    {
        _panel.anchoredPosition = HiddenPosition;
        _canvasGroup.alpha = 0f;
        _panel.gameObject.SetActive(false);
    }
}
