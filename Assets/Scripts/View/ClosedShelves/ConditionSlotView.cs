using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ConditionSlotView : MonoBehaviour
{
    [SerializeField] private Image _ringFill;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _count;
    [SerializeField, Min(0.01f)] private float _fillDuration = 0.25f;

    private int _current;
    private int _required;
    private Tween _fillTween;

    public Vector3 TargetPosition => _icon.rectTransform.position;

    public void Setup(Sprite icon, int current, int required, Color? iconTint)
    {
        _icon.sprite = icon;
        _icon.preserveAspect = true;
        _current = current;
        _required = required;

        _icon.color = iconTint ?? Color.white;

        _ringFill.fillAmount = Progress;
        UpdateCount();
    }

    public void Add(int amount)
    {
        if (amount <= 0)
            return;

        _current = Mathf.Min(_required, _current + amount);
        UpdateCount();
        _fillTween?.Kill();
        _fillTween = _ringFill.DOFillAmount(Progress, _fillDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    private float Progress => _required > 0 ? (float)_current / _required : 1f;

    private void UpdateCount() => _count.text = $"{_current}/{_required}";
}
