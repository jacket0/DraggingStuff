using System.Collections;
using UnityEngine;
using YG;

public class MatchEffectView : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private ParticleSystemRenderer _particleRenderer;
    [SerializeField] private Material[] _praiseMaterials;
    [SerializeField] private Material[] _russianPraiseMaterials;
    [SerializeField] private Material[] _turkishPraiseMaterials;

    private Coroutine _particleCoroutine;
    private int _praiseIndex = -1;

    private void OnEnable()
    {
        YG2.onSwitchLang += ApplyLanguage;
        ApplyLanguage(YG2.lang);
    }

    private void OnDisable()
    {
        YG2.onSwitchLang -= ApplyLanguage;

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

        _praiseIndex = UnityEngine.Random.Range(0, _praiseMaterials.Length);
        ApplyLanguage(YG2.lang);
    }

    private void ApplyLanguage(string language)
    {
        if (_particleRenderer == null || _praiseMaterials == null ||
            _praiseIndex < 0 || _praiseIndex >= _praiseMaterials.Length)
            return;

        Material[] materials = language switch
        {
            "ru" => _russianPraiseMaterials,
            "tr" => _turkishPraiseMaterials,
            _ => _praiseMaterials
        };

        Material material = materials != null && _praiseIndex < materials.Length
            ? materials[_praiseIndex]
            : null;

        _particleRenderer.sharedMaterial = material != null ? material : _praiseMaterials[_praiseIndex];
    }
}
