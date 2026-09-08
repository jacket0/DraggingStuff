using UnityEngine;
using UnityEngine.UI;

public sealed class MenuPanelLayout : HorizontalOrVerticalLayoutGroup
{
    [SerializeField] private bool _isVertical;

    public void SetVertical(bool isVertical)
    {
        if (_isVertical == isVertical)
            return;

        _isVertical = isVertical;
        SetDirty();
    }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        CalcAlongAxis(0, _isVertical);
    }

    public override void CalculateLayoutInputVertical()
    {
        CalcAlongAxis(1, _isVertical);
    }

    public override void SetLayoutHorizontal()
    {
        SetChildrenAlongAxis(0, _isVertical);
    }

    public override void SetLayoutVertical()
    {
        SetChildrenAlongAxis(1, _isVertical);
    }
}
