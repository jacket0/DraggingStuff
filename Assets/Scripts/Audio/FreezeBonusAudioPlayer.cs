using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class FreezeBonusAudioPlayer : MonoBehaviour
{
    [SerializeField] private TimerFreezeBonusEffect _freezeEffect;
    [SerializeField] private AudioClip _activatedClip;
    [SerializeField, Range(0f, 1f)] private float _activatedVolume = 0.5f;

    private AudioSource _audioSource;

    private void Awake()
    {
        if (_freezeEffect == null || _activatedClip == null)
            throw new InvalidOperationException($"{name}: invalid freeze bonus audio wiring.");

        _audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable() => _freezeEffect.Activated += HandleActivated;

    private void OnDisable() => _freezeEffect.Activated -= HandleActivated;

    private void HandleActivated(IBonusEffect effect) => _audioSource.PlayOneShot(_activatedClip, _activatedVolume);
}
