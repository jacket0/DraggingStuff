using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YG;

public sealed class TutorialStepHint : MonoBehaviour
{
    [SerializeField] private CanvasGroup _bubbleGroup;
    [SerializeField] private TMP_Text _text;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    [SerializeField] private LocalizedHintText _moveText = new LocalizedHintText(
        "Перетащи предмет на свободное место и собери три одинаковых",
        "Drag an item to an empty spot to make three of a kind",
        "Aynı üç eşyayı toplamak için bir eşyayı boş yere sürükle");
    [SerializeField] private LocalizedHintText _swapText = new LocalizedHintText(
        "Перетащи предмет на другой, чтобы поменять их местами",
        "Drag an item onto another to swap them",
        "Yerlerini değiştirmek için bir eşyayı diğerinin üstüne sürükle");

    private bool _isShown;
    private bool _isSwapShown;
    private Sequence _sequence;

    private void Awake()
    {
        if (_bubbleGroup == null)
            throw new InvalidOperationException(nameof(_bubbleGroup));

        if (_text == null)
            throw new InvalidOperationException(nameof(_text));

        _bubbleGroup.alpha = 0f;
    }

    private void OnDisable()
    {
        _sequence?.Kill();
    }

    public void Show(bool isSwap)
    {
        if (_isShown && _isSwapShown == isSwap)
            return;

        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetLink(gameObject);

        if (_isShown)
            _sequence.Append(_bubbleGroup.DOFade(0f, _fadeDuration));

        LocalizedHintText text = isSwap ? _swapText : _moveText;
        _sequence.AppendCallback(() => _text.text = text.Get(YG2.lang));
        _sequence.Append(_bubbleGroup.DOFade(1f, _fadeDuration));
        _isShown = true;
        _isSwapShown = isSwap;
    }

    public void Hide()
    {
        if (!_isShown)
            return;

        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetLink(gameObject);
        _sequence.Append(_bubbleGroup.DOFade(0f, _fadeDuration));
        _isShown = false;
    }
}
