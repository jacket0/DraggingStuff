using System;
using UnityEngine;

[Serializable]
public sealed class KeyLockMark
{
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    [SerializeField] private Color _color = Color.white;
    [SerializeField] private GameObject _shapePrefab;

    public Color Color => _color;
    public GameObject ShapePrefab => _shapePrefab;

    public GameObject Attach(Transform point)
    {
        if (point == null)
            throw new ArgumentNullException(nameof(point));

        GameObject shape = UnityEngine.Object.Instantiate(_shapePrefab, point, false);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(ColorProperty, _color);

        foreach (Renderer renderer in shape.GetComponentsInChildren<Renderer>())
            renderer.SetPropertyBlock(block);

        return shape;
    }
}
