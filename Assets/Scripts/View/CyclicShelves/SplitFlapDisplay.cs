using System;
using System.Linq;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class SplitFlapDisplay : MonoBehaviour
{
    public const string Glyphs = "0123456789-";
    public const int LockCell = 11;
    public const int ArrowUpCell = 12;
    public const int AtlasColumns = 4;
    public const int AtlasRows = 4;

    private const int QuadsPerDigit = 4;
    private const float FlapOffset = 0.001f;

    private enum CardPart
    {
        Whole,
        Top,
        Bottom
    }

    [SerializeField, Min(1)] private int _digitCount = 2;
    [SerializeField] private Vector2 _cardSize = new Vector2(0.135f, 0.166f);
    [SerializeField, Min(0f)] private float _digitSpacing = 0.15f;
    [SerializeField] private Vector3 _iconPosition = new Vector3(0.25f, 0f, 0.0225f);
    [SerializeField] private Vector2 _iconSize = new Vector2(0.046f, 0.057f);
    [SerializeField, Min(0.01f)] private float _flipDuration = 0.22f;

    private Mesh _mesh;
    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private string _text;
    private string _previousText;
    private SplitFlapIcon _icon;
    private float _progress = 1f;
    private Tween _flip;

    public string Text => _text;
    public bool IsFlipping => _flip != null;

    private void Awake()
    {
        int quadCount = _digitCount * QuadsPerDigit + 1;
        _vertices = new Vector3[quadCount * 4];
        _uvs = new Vector2[quadCount * 4];
        int[] triangles = new int[quadCount * 6];

        for (int quad = 0; quad < quadCount; quad++)
        {
            int vertex = quad * 4;
            int triangle = quad * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        _mesh = new Mesh { name = "SplitFlapDisplay" };
        _mesh.MarkDynamic();
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.triangles = triangles;
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        _text = new string('-', _digitCount);
        _previousText = _text;
        Rebuild();
    }

    public void Show(string text, SplitFlapIcon icon, bool animate)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        if (text.Length != _digitCount || text.Any(character => Glyphs.IndexOf(character) < 0))
            throw new ArgumentOutOfRangeException(nameof(text), text, "The split-flap display cannot show this text.");

        _icon = icon;
        KillFlip();

        if (!animate || text == _text)
        {
            _text = text;
            _previousText = text;
            Rebuild();
            return;
        }

        _previousText = _text;
        _text = text;
        _progress = 0f;
        Tween flip = DOTween.To(() => _progress, SetProgress, 1f, _flipDuration).SetEase(Ease.Linear);
        _flip = flip;
        flip.SetLink(gameObject, LinkBehaviour.KillOnDestroy).OnKill(() =>
        {
            if (_flip != flip)
                return;

            _flip = null;
            _previousText = _text;
            SetProgress(1f);
        });
    }

    private void KillFlip()
    {
        Tween flip = _flip;
        flip?.Kill();
    }

    private void SetProgress(float progress)
    {
        _progress = progress;
        Rebuild();
    }

    private void Rebuild()
    {
        float fallingAngle = _progress < 0.5f ? 90f * DOVirtual.EasedValue(0f, 1f, _progress * 2f, Ease.InQuad) : 90f;
        float landingAngle = _progress < 0.5f ? 90f : 90f * (1f - DOVirtual.EasedValue(0f, 1f, (_progress - 0.5f) * 2f, Ease.OutBounce));

        for (int digit = 0; digit < _digitCount; digit++)
        {
            int quad = digit * QuadsPerDigit;
            float centerX = ((_digitCount - 1) * 0.5f - digit) * _digitSpacing;
            int newCell = Glyphs.IndexOf(_text[digit]);
            int oldCell = Glyphs.IndexOf(_previousText[digit]);
            bool isFlipping = newCell != oldCell;
            Vector3 center = new Vector3(centerX, 0f, 0f);
            SetCard(quad, newCell, CardPart.Top, center, _cardSize, 0f);
            SetCard(quad + 1, isFlipping ? oldCell : newCell, CardPart.Bottom, center, _cardSize, 0f);
            SetCard(quad + 2, isFlipping && _progress < 0.5f ? oldCell : -1, CardPart.Top, center + Vector3.forward * FlapOffset, _cardSize, fallingAngle);
            SetCard(quad + 3, isFlipping && _progress >= 0.5f ? newCell : -1, CardPart.Bottom, center + Vector3.forward * FlapOffset, _cardSize, landingAngle);
        }

        int iconCell = _icon == SplitFlapIcon.Lock ? LockCell : _icon == SplitFlapIcon.ArrowUp ? ArrowUpCell : -1;
        SetCard(_digitCount * QuadsPerDigit, iconCell, CardPart.Whole, _iconPosition, _iconSize, 0f);
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.RecalculateBounds();
    }

    private void SetCard(int quad, int cell, CardPart part, Vector3 center, Vector2 size, float angle)
    {
        int vertex = quad * 4;

        if (cell < 0)
        {
            for (int index = 0; index < 4; index++)
                _vertices[vertex + index] = center;

            return;
        }

        float halfWidth = size.x * 0.5f;
        float top = part == CardPart.Bottom ? 0f : size.y * 0.5f;
        float bottom = part == CardPart.Top ? 0f : -size.y * 0.5f;
        float cos = Mathf.Cos(angle * Mathf.Deg2Rad);
        float sin = Mathf.Sin(angle * Mathf.Deg2Rad);

        Vector3 Place(float screenX, float y) => center + new Vector3(-screenX, y * cos, Mathf.Abs(y) * sin);

        _vertices[vertex] = Place(-halfWidth, top);
        _vertices[vertex + 1] = Place(halfWidth, top);
        _vertices[vertex + 2] = Place(halfWidth, bottom);
        _vertices[vertex + 3] = Place(-halfWidth, bottom);

        float cellWidth = 1f / AtlasColumns;
        float cellHeight = 1f / AtlasRows;
        float left = cell % AtlasColumns * cellWidth;
        float cellTop = 1f - cell / AtlasColumns * cellHeight;
        float cellMiddle = cellTop - cellHeight * 0.5f;
        float vTop = part == CardPart.Bottom ? cellMiddle : cellTop;
        float vBottom = part == CardPart.Top ? cellMiddle : cellTop - cellHeight;
        _uvs[vertex] = new Vector2(left, vTop);
        _uvs[vertex + 1] = new Vector2(left + cellWidth, vTop);
        _uvs[vertex + 2] = new Vector2(left + cellWidth, vBottom);
        _uvs[vertex + 3] = new Vector2(left, vBottom);
    }

    private void OnDestroy()
    {
        KillFlip();
        Destroy(_mesh);
    }
}
