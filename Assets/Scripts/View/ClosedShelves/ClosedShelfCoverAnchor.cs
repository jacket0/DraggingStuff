using System;
using UnityEngine;

[RequireComponent(typeof(Shelf))]
public sealed class ClosedShelfCoverAnchor : MonoBehaviour
{
    private const float DepthSearchPadding = 0.05f;

    [SerializeField] private Vector3 _localCenter;
    [SerializeField] private Vector2 _size = new Vector2(1f, 0.3f);
    [SerializeField] private Vector3 _localTagPoint;
    [SerializeField] private Vector3 _depthAxis = Vector3.back;
    [SerializeField, Min(0f)] private float _backDepth = 0.19f;
    [SerializeField, Min(0f)] private float _frontDepth = 0.1f;

    public Vector3 Center => transform.TransformPoint(_localCenter);
    public Vector2 Size => _size;
    public Vector3 TagPoint => transform.TransformPoint(_localTagPoint);
    public Vector3 DepthAxis => _depthAxis;
    public Vector3 BackCenter => Center + _depthAxis * _backDepth;
    public Vector3 FrontCenter => Center - _depthAxis * _frontDepth;

    public Quaternion FacingRotation(Camera camera)
    {
        return camera != null ? Quaternion.LookRotation(camera.transform.forward, transform.up) : transform.rotation;
    }

    public Quaternion DepthRotation => Quaternion.LookRotation(_depthAxis, Vector3.up);

    [ContextMenu("Fit To Columns")]
    public void FitToColumns()
    {
        Shelf shelf = GetComponent<Shelf>();

        if (shelf.ColumnViews.Count == 0)
            throw new InvalidOperationException($"{name}: shelf has no columns.");

        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        foreach (ShelfColumnView columnView in shelf.ColumnViews)
        {
            Vector3 local = transform.InverseTransformPoint(columnView.ItemAnchor.position);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        int count = shelf.ColumnViews.Count;
        float spacing = count > 1 ? (max.x - min.x) / (count - 1) : 1f;
        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        Vector2 localSize = new Vector2(max.x - min.x + spacing, spacing * 0.95f);

        _localCenter = (min + max) * 0.5f;
        _localCenter.y += localSize.y * 0.2f;
        _localTagPoint = _localCenter + Vector3.up * localSize.y * 0.5f;
        _size = new Vector2(localSize.x * scaleX, localSize.y * scaleY);
        FitDepth();
    }

    private void FitDepth()
    {
        Camera camera = Camera.main;

        if (camera != null)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);

            if (horizontal.sqrMagnitude > 0.0001f)
                _depthAxis = horizontal.normalized;
        }

        Vector3 center = Center;
        Bounds? housing = null;

        foreach (MeshRenderer renderer in FindObjectsOfType<MeshRenderer>())
        {
            Bounds bounds = renderer.bounds;
            bounds.Expand(DepthSearchPadding);

            if (!bounds.Contains(center) || renderer.GetComponentInParent<Shelf>() != null)
                continue;

            if (!housing.HasValue || Volume(renderer.bounds) < Volume(housing.Value))
                housing = renderer.bounds;
        }

        if (!housing.HasValue)
            return;

        _backDepth = Mathf.Max(0f, Extent(housing.Value, center, _depthAxis));
        _frontDepth = Mathf.Max(0f, Extent(housing.Value, center, -_depthAxis));
    }

    private static float Volume(Bounds bounds) => bounds.size.x * bounds.size.y * bounds.size.z;

    private static float Extent(Bounds bounds, Vector3 origin, Vector3 axis)
    {
        float extent = float.MinValue;

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = new Vector3(
                (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
            extent = Mathf.Max(extent, Vector3.Dot(point - origin, axis));
        }

        return extent;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.64f, 0.17f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(Center, FacingRotation(Camera.main), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(_size.x, _size.y, 0.001f));
        Gizmos.matrix = Matrix4x4.TRS((BackCenter + FrontCenter) * 0.5f, DepthRotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(_size.x, _size.y, _backDepth + _frontDepth));
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawSphere(TagPoint, 0.02f);
    }
}
