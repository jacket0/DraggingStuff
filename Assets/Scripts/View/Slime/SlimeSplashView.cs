using System;
using UnityEngine;

public sealed class SlimeSplashView : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particles;

    private void Awake()
    {
        if (_particles == null)
            throw new InvalidOperationException(nameof(_particles));
    }

    public void Burst(Vector3 position, int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        _particles.transform.position = position;
        _particles.Emit(count);
    }
}
