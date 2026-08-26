using System.Collections;
using UnityEngine;

public class MatchEffectView : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private ParticleSystemRenderer _particleRenderer;
    [SerializeField] private Material[] _praiseMaterials;

    private Coroutine _particleCoroutine;

    private void OnDisable()
    {
        if (_particleCoroutine != null)
            StopCoroutine(_particleCoroutine);
    }

    public void Play(Vector3 worldPosition)
    {
        transform.position = worldPosition;

        _particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        SelectRandomPraiseMaterial();

        _particleSystem?.Play(true);
        _particleCoroutine = StartCoroutine(DestroyWhenFinished());
    }

    private IEnumerator DestroyWhenFinished()
    {
        yield return new WaitUntil(() => !_particleSystem.IsAlive(true));
        _particleCoroutine = null;
        Destroy(gameObject);
    }

    private void SelectRandomPraiseMaterial()
    {
        if (_particleRenderer == null || _praiseMaterials == null || _praiseMaterials.Length == 0)
            return;

        int materialIndex = UnityEngine.Random.Range(0, _praiseMaterials.Length);
        _particleRenderer.sharedMaterial = _praiseMaterials[materialIndex];
    }
}
