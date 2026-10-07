using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ShelfItemOutlineView : MonoBehaviour
{
    private const string OutlineMaterialPath = "ShelfItemOutline";
    private const string MarkMaterialPath = "ShelfItemFilterMark";

    private readonly List<Renderer> _outlineRenderers = new List<Renderer>();

    private Material _outlineMaterial;
    private Material _markMaterial;
    private int _requestCount;
    private bool _isMarked;
    private bool _isInitialized;

    private void OnDisable()
    {
        _requestCount = 0;
        _isMarked = false;
        Refresh();
    }

    public static ShelfItemOutlineView GetRequired(ShelfItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        ShelfItemOutlineView outlineView = item.GetComponent<ShelfItemOutlineView>();

        if (outlineView == null)
            throw new MissingReferenceException($"{item.name}: {nameof(ShelfItemOutlineView)}");

        return outlineView;
    }

    public void Show()
    {
        EnsureInitialized();
        _requestCount++;
        Refresh();
    }

    public void Hide()
    {
        _requestCount = Mathf.Max(0, _requestCount - 1);
        Refresh();
    }

    public void SetMarked(bool isMarked)
    {
        if (isMarked)
            EnsureInitialized();

        _isMarked = isMarked;
        Refresh();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        _outlineMaterial = Resources.Load<Material>(OutlineMaterialPath);
        _markMaterial = Resources.Load<Material>(MarkMaterialPath);

        if (_outlineMaterial == null || _markMaterial == null)
            throw new InvalidOperationException($"{OutlineMaterialPath}, {MarkMaterialPath}");

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                CreateSkinnedOutline(skinnedMeshRenderer, _outlineMaterial);
                continue;
            }

            if (itemRenderer is MeshRenderer meshRenderer)
                CreateMeshOutline(meshRenderer, _outlineMaterial);
        }

        _isInitialized = true;
    }

    private void CreateMeshOutline(MeshRenderer sourceRenderer, Material outlineMaterial)
    {
        MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();

        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            return;

        GameObject outlineObject = new GameObject("Outline");
        outlineObject.layer = sourceRenderer.gameObject.layer;
        outlineObject.transform.SetParent(sourceRenderer.transform, false);

        MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        ApplyRendererSettings(outlineRenderer, sourceRenderer, outlineMaterial, sourceFilter.sharedMesh.subMeshCount);
        _outlineRenderers.Add(outlineRenderer);
    }

    private void CreateSkinnedOutline(SkinnedMeshRenderer sourceRenderer, Material outlineMaterial)
    {
        if (sourceRenderer.sharedMesh == null)
            return;

        GameObject outlineObject = new GameObject("Outline");
        outlineObject.layer = sourceRenderer.gameObject.layer;
        outlineObject.transform.SetParent(sourceRenderer.transform, false);

        SkinnedMeshRenderer outlineRenderer = outlineObject.AddComponent<SkinnedMeshRenderer>();
        outlineRenderer.sharedMesh = sourceRenderer.sharedMesh;
        outlineRenderer.bones = sourceRenderer.bones;
        outlineRenderer.rootBone = sourceRenderer.rootBone;
        outlineRenderer.localBounds = sourceRenderer.localBounds;
        outlineRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        ApplyRendererSettings(outlineRenderer, sourceRenderer, outlineMaterial, sourceRenderer.sharedMesh.subMeshCount);
        _outlineRenderers.Add(outlineRenderer);
    }

    private static void ApplyRendererSettings(Renderer target, Renderer source, Material outlineMaterial, int materialCount)
    {
        Material[] materials = new Material[Mathf.Max(1, materialCount)];

        for (int index = 0; index < materials.Length; index++)
            materials[index] = outlineMaterial;

        target.sharedMaterials = materials;
        target.shadowCastingMode = ShadowCastingMode.Off;
        target.receiveShadows = false;
        target.lightProbeUsage = LightProbeUsage.Off;
        target.reflectionProbeUsage = ReflectionProbeUsage.Off;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder - 1;
    }

    private void Refresh()
    {
        bool isVisible = _requestCount > 0 || _isMarked;
        Material material = _requestCount > 0 ? _outlineMaterial : _markMaterial;

        foreach (Renderer outlineRenderer in _outlineRenderers)
        {
            outlineRenderer.gameObject.SetActive(isVisible);

            if (isVisible && outlineRenderer.sharedMaterial != material)
                outlineRenderer.sharedMaterials = Enumerable.Repeat(material, outlineRenderer.sharedMaterials.Length).ToArray();
        }
    }
}
