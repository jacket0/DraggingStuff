using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ShelfItemPresentation : MonoBehaviour
{
    private Renderer[] _renderers;
    private Material[][] _materials;
    private bool[] _rendererStates;
    private ShadowCastingMode[] _shadowModes;
    private bool[] _receiveShadows;
    private Collider[] _colliders;
    private bool[] _colliderStates;

    private void Awake() => Capture();

    public void ShowFront()
    {
        Restore();
        gameObject.SetActive(true);
    }

    public void ShowPreview(Material material)
    {
        if (material == null)
            throw new ArgumentNullException(nameof(material));

        Capture();
        gameObject.SetActive(true);

        for (int index = 0; index < _renderers.Length; index++)
        {
            Material[] materials = new Material[_materials[index].Length];

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = material;

            _renderers[index].enabled = _rendererStates[index];
            _renderers[index].sharedMaterials = materials;
            _renderers[index].shadowCastingMode = ShadowCastingMode.Off;
            _renderers[index].receiveShadows = false;
        }

        foreach (Collider collider in _colliders)
            collider.enabled = false;
    }

    public void Hide()
    {
        Capture();

        foreach (Collider collider in _colliders)
            collider.enabled = false;

        gameObject.SetActive(false);
    }

    public void Reset() => Restore();

    private void Capture()
    {
        if (_renderers != null)
            return;

        _renderers = GetComponentsInChildren<Renderer>(true);
        _materials = new Material[_renderers.Length][];
        _rendererStates = new bool[_renderers.Length];
        _shadowModes = new ShadowCastingMode[_renderers.Length];
        _receiveShadows = new bool[_renderers.Length];

        for (int index = 0; index < _renderers.Length; index++)
        {
            Renderer renderer = _renderers[index];
            _materials[index] = renderer.sharedMaterials;
            _rendererStates[index] = renderer.enabled;
            _shadowModes[index] = renderer.shadowCastingMode;
            _receiveShadows[index] = renderer.receiveShadows;
        }

        _colliders = GetComponentsInChildren<Collider>(true);
        _colliderStates = new bool[_colliders.Length];

        for (int index = 0; index < _colliders.Length; index++)
            _colliderStates[index] = _colliders[index].enabled;
    }

    private void Restore()
    {
        Capture();

        for (int index = 0; index < _renderers.Length; index++)
        {
            _renderers[index].sharedMaterials = _materials[index];
            _renderers[index].enabled = _rendererStates[index];
            _renderers[index].shadowCastingMode = _shadowModes[index];
            _renderers[index].receiveShadows = _receiveShadows[index];
        }

        for (int index = 0; index < _colliders.Length; index++)
            _colliders[index].enabled = _colliderStates[index];
    }
}
