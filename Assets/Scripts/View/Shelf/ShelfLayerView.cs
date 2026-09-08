using System;
using UnityEngine;
using UnityEngine.Rendering;

public class ShelfLayerView : MonoBehaviour
{
    [SerializeField] private Material _previewMaterial;

    private Renderer[] _renderers;
    private Material[][] _originalMaterials;
    private ShadowCastingMode[] _originalShadowCastingModes;
    private bool[] _originalReceiveShadows;

    public void Initialize()
    {
        if (_previewMaterial == null)
            throw new InvalidOperationException(nameof(_previewMaterial));

        _renderers = GetComponentsInChildren<Renderer>(true);

        _originalMaterials = new Material[_renderers.Length][];
        _originalShadowCastingModes = new ShadowCastingMode[_renderers.Length];
        _originalReceiveShadows = new bool[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];

            _originalMaterials[i] = renderer.sharedMaterials;
            _originalShadowCastingModes[i] = renderer.shadowCastingMode;
            _originalReceiveShadows[i] = renderer.receiveShadows;
        }
    }

    public void ShowActive()
    {
        gameObject.SetActive(true);

        RestoreMaterials();
        RestoreShadows();

        SetInteractionEnabled(true);
    }

    public void ShowPreview()
    {
        gameObject.SetActive(true);

        ApplyPreviewMaterials();
        DisableShadows();

        SetInteractionEnabled(false);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void ResetForPool()
    {
        if (_renderers != null)
        {
            RestoreMaterials();
            RestoreShadows();
        }

        SetInteractionEnabled(true);
        gameObject.SetActive(false);
    }

    private void SetInteractionEnabled(bool isEnabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            collider.enabled = isEnabled;
        }
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].sharedMaterials = _originalMaterials[i];
        }
    }

    private void ApplyPreviewMaterials()
    {
        for (int r = 0; r < _renderers.Length; r++)
        {
            Material[] originalMaterials = _originalMaterials[r];
            Material[] previewMaterials = new Material[originalMaterials.Length];

            for (int m = 0; m < previewMaterials.Length; m++)
            {
                previewMaterials[m] = _previewMaterial;
            }

            _renderers[r].sharedMaterials = previewMaterials;
        }
    }

    private void DisableShadows()
    {
        foreach (Renderer renderer in _renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void RestoreShadows()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].shadowCastingMode = _originalShadowCastingModes[i];
            _renderers[i].receiveShadows = _originalReceiveShadows[i];
        }
    }
}