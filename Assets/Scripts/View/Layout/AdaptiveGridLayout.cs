using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public sealed class AdaptiveGridLayout : MonoBehaviour
{
    [SerializeField, Min(1)] private int _compactColumnCount = 2;
    [SerializeField, Min(1)] private int _regularColumnCount = 3;
    [SerializeField, Min(0.1f)] private float _compactAspectThreshold = 1.6f;

    private GridLayoutGroup _gridLayout;
    private int _columnCount;
    private float _width;

    private void Awake()
    {
        _gridLayout = GetComponent<GridLayoutGroup>();
        ApplyLayout();
    }

    private void OnEnable()
    {
        ApplyLayout();
    }

    private void Update()
    {
        int columnCount = GetColumnCount();

        if (columnCount != _columnCount || !Mathf.Approximately(_width, ((RectTransform)transform).rect.width))
            ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_gridLayout == null)
            return;

        _columnCount = GetColumnCount();
        _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _gridLayout.constraintCount = _columnCount;
        _width = ((RectTransform)transform).rect.width;
        float availableWidth = _width - _gridLayout.padding.horizontal - _gridLayout.spacing.x * (_columnCount - 1);
        float cellWidth = Mathf.Max(1f, availableWidth / _columnCount);
        _gridLayout.cellSize = new Vector2(cellWidth, Mathf.Clamp(cellWidth * 170f / 240f, 140f, 200f));
        LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
    }

    private int GetColumnCount()
    {
        if (Screen.height <= 0)
            return _regularColumnCount;

        float aspect = (float)Screen.width / Screen.height;
        return aspect <= _compactAspectThreshold ? _compactColumnCount : _regularColumnCount;
    }
}
