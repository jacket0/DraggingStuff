using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YG;

public sealed class CycleRescueToastView : MonoBehaviour
{
    [SerializeField] private CyclicShelvesController _cyclicShelves;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _text;
    [SerializeField, Min(0f)] private float _hiddenOffset = 220f;
    [SerializeField, Min(0.01f)] private float _showDuration = 0.35f;
    [SerializeField, Min(0f)] private float _holdDuration = 2.5f;
    [SerializeField, Min(0.01f)] private float _hideDuration = 0.3f;
    [SerializeField] private LocalizedHintText _earlySwitchText = new LocalizedHintText(
        "Ходов нет: табло переключилось раньше",
        "No moves left: the board switched early",
        "Hamle kalmadı: tablo erken değişti");
    [SerializeField] private LocalizedHintText _allOpenText = new LocalizedHintText(
        "Все ячейки открыты до конца уровня",
        "All lockers stay open until the end",
        "Tüm dolaplar seviye sonuna kadar açık");

    private Vector2 _shownPosition;
    private Sequence _sequence;

    public bool IsVisible => _panel.gameObject.activeSelf;
    public string Text => _text.text;

    private void Awake()
    {
        if (_cyclicShelves == null || _panel == null || _canvasGroup == null || _text == null)
            throw new InvalidOperationException("Invalid cycle rescue toast wiring.");

        _shownPosition = _panel.anchoredPosition;
        SetHidden();
    }

    private void OnEnable() => _cyclicShelves.RescueApplied += Show;

    private void OnDisable()
    {
        _cyclicShelves.RescueApplied -= Show;
        _sequence?.Kill();
    }

    private void Show(bool isRepeated)
    {
        _sequence?.Kill();
        SetHidden();
        _text.text = (isRepeated ? _allOpenText : _earlySwitchText).Get(YG2.lang);
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
