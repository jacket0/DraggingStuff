using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class DepartureBoardSlotView : MonoBehaviour
{
    private const string NoCycleText = "--";

    [SerializeField] private Shelf _shelf;
    [SerializeField] private TMP_Text _digits;
    [SerializeField] private Renderer _lamp;
    [SerializeField] private Material _openLampMaterial;
    [SerializeField] private Material _closedLampMaterial;
    [SerializeField] private Material _offLampMaterial;
    [SerializeField, Min(0.01f)] private float _flipDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float _warningDuration = 0.35f;
    [SerializeField, Min(1f)] private float _warningScale = 1.4f;

    private Vector3 _digitsScale;
    private Vector3 _lampScale;
    private Sequence _flip;
    private Tween _warning;

    public Shelf Shelf => _shelf;
    public string Text => _digits.text;

    private void Awake()
    {
        if (_shelf == null)
            throw new InvalidOperationException(nameof(_shelf));

        if (_digits == null)
            throw new InvalidOperationException(nameof(_digits));

        if (_lamp == null)
            throw new InvalidOperationException(nameof(_lamp));

        if (_openLampMaterial == null || _closedLampMaterial == null || _offLampMaterial == null)
            throw new InvalidOperationException($"{name}: lamp materials are required.");

        _digitsScale = _digits.transform.localScale;
        _lampScale = _lamp.transform.localScale;
    }

    public void Show(ShelfCycleSnapshot cycle, bool isOpen, bool animate)
    {
        gameObject.SetActive(true);
        string text = cycle == null ? NoCycleText : cycle.MovesToSwitch.ToString("00");
        bool isChanged = text != _digits.text;
        _lamp.sharedMaterial = cycle == null ? _offLampMaterial : isOpen ? _openLampMaterial : _closedLampMaterial;
        SetWarning(cycle != null && cycle.MovesToSwitch == 1);

        if (animate && isChanged)
            PlayFlip(text);
        else
            SetText(text);
    }

    public void HideForLevel()
    {
        SetWarning(false);
        KillFlip();
        gameObject.SetActive(false);
    }

    private void PlayFlip(string text)
    {
        KillFlip();
        Transform digits = _digits.transform;
        Sequence flip = DOTween.Sequence();
        _flip = flip;
        flip.Append(digits.DOScaleY(0f, _flipDuration * 0.5f).SetEase(Ease.InQuad));
        flip.AppendCallback(() => _digits.text = text);
        flip.Append(digits.DOScaleY(_digitsScale.y, _flipDuration * 0.5f).SetEase(Ease.OutQuad));
        flip.SetLink(gameObject, LinkBehaviour.KillOnDestroy).OnKill(() =>
        {
            if (_flip != flip)
                return;

            _flip = null;
            SetText(text);
        });
    }

    private void KillFlip()
    {
        Sequence flip = _flip;
        flip?.Kill();
    }

    private void SetText(string text)
    {
        _digits.text = text;
        _digits.transform.localScale = _digitsScale;
    }

    private void SetWarning(bool isWarning)
    {
        if (isWarning == (_warning != null))
            return;

        if (!isWarning)
        {
            _warning.Kill();
            _warning = null;
            _lamp.transform.localScale = _lampScale;
            return;
        }

        _warning = _lamp.transform.DOScale(_lampScale * _warningScale, _warningDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }
}
