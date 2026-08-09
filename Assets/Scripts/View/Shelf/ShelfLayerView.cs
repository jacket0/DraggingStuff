using System;
using UnityEngine;

public class ShelfLayerView : MonoBehaviour
{
    [SerializeField] private Material _previewMaterial;

    private Renderer[] _renderers;
    private Material[][] _originalMaterials;

    public void Initialize()
    {
        if (_previewMaterial == null)
            throw new InvalidOperationException(nameof(_previewMaterial));

        _renderers = GetComponentsInChildren<Renderer>(true);
        _originalMaterials = new Material[_renderers.Length][];

        for (int i = 0; i < _originalMaterials.Length; i++)
        {
            _originalMaterials[i] = _renderers[i].sharedMaterials;
        }
    }

    public void ShowActive()
    {
        gameObject.SetActive(true);
        RestoreMaterials();
        SetInteractionEnabled(true);
    }

    public void ShowPreview()
    {
        gameObject.SetActive(true);
        ApplyPreviewMaterials();
        SetInteractionEnabled(false);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void SetInteractionEnabled(bool isEnabled)
    {
        Collider[] colliders = gameObject.GetComponentsInChildren<Collider>(true);

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
}
