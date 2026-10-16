using System;
using DG.Tweening;
using UnityEngine;

public sealed class DepartureBoardSlotView : MonoBehaviour
{
    private const string NoCycleText = "--";

    [SerializeField] private Shelf _shelf;
    [SerializeField] private SplitFlapDisplay _display;
    [SerializeField] private Renderer _lamp;
    [SerializeField] private Material _openLampMaterial;
    [SerializeField] private Material _closedLampMaterial;
    [SerializeField] private Material _offLampMaterial;
    [SerializeField, Min(0.01f)] private float _warningDuration = 0.35f;
    [SerializeField, Min(1f)] private float _warningScale = 1.4f;

    private Vector3 _lampScale;
    private Tween _warning;

    public Shelf Shelf => _shelf;
    public string Text => _display.Text;

    private void Awake()
    {
        if (_shelf == null)
            throw new InvalidOperationException(nameof(_shelf));

        if (_display == null)
            throw new InvalidOperationException(nameof(_display));

        if (_lamp == null)
            throw new InvalidOperationException(nameof(_lamp));

        if (_openLampMaterial == null || _closedLampMaterial == null || _offLampMaterial == null)
            throw new InvalidOperationException($"{name}: lamp materials are required.");

        _lampScale = _lamp.transform.localScale;
    }

    public bool Show(ShelfCycleSnapshot cycle, bool isOpen, bool animate)
    {
        gameObject.SetActive(true);
        string text = cycle == null ? NoCycleText : cycle.MovesToSwitch.ToString("00");
        bool isChanged = text != _display.Text;
        _lamp.sharedMaterial = cycle == null ? _offLampMaterial : isOpen ? _openLampMaterial : _closedLampMaterial;
        SetWarning(cycle != null && cycle.MovesToSwitch == 1);
        _display.Show(text, GetIcon(cycle, isOpen), animate);
        return isChanged;
    }

    public void HideForLevel()
    {
        SetWarning(false);
        gameObject.SetActive(false);
    }

    private static SplitFlapIcon GetIcon(ShelfCycleSnapshot cycle, bool isOpen)
    {
        if (cycle == null)
            return SplitFlapIcon.None;

        return isOpen ? SplitFlapIcon.Lock : SplitFlapIcon.ArrowUp;
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
