using System;
using System.Collections;
using UnityEngine;

public class MatchEffectView : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;

    private Coroutine _particleCoroutine;

    private void OnDisable()
    {
        if (_particleCoroutine != null)
            StopCoroutine(_particleCoroutine);
    }

    public void Play(Vector3 worldPosition, Action completed)
    {
        transform.position = worldPosition;

        _particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _particleSystem?.Play(true);
        _particleCoroutine = StartCoroutine(WaitForCompletion(completed));
    }

    private IEnumerator WaitForCompletion(Action completed)
    {
        yield return new WaitUntil(() => !_particleSystem.IsAlive(true));
        completed?.Invoke();
    }
}
