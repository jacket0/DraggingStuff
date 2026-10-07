using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ShelfItemOutlineView : MonoBehaviour
{
    private const string OutlineMaterialPath = "ShelfItemOutline";

    private readonly List<GameObject> _outlineObjects = new List<GameObject>();

    private int _requestCount;
    private bool _isInitialized;

    private void OnDisable()
    {
        _requestCount = 0;
        SetVisible(false);
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
        SetVisible(true);
    }

    public void Hide()
    {
        _requestCount = Mathf.Max(0, _requestCount - 1);

        if (_requestCount == 0)
            SetVisible(false);
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        Material outlineMaterial = Resources.Load<Material>(OutlineMaterialPath);

        if (outlineMaterial == null)
            throw new InvalidOperationException(OutlineMaterialPath);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                CreateSkinnedOutline(skinnedMeshRenderer, outlineMaterial);
                continue;
            }

            if (itemRenderer is MeshRenderer meshRenderer)
                CreateMeshOutline(meshRenderer, outlineMaterial);
        }

        _isInitialized = true;
        SetVisible(false);
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
        _outlineObjects.Add(outlineObject);
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
        _outlineObjects.Add(outlineObject);
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

    private void SetVisible(bool isVisible)
    {
        foreach (GameObject outlineObject in _outlineObjects)
            outlineObject.SetActive(isVisible);
    }
}
