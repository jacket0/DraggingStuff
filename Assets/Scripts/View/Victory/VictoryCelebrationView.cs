using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

public sealed class VictoryCelebrationView : MonoBehaviour
{
    [SerializeField] private ParticleSystem _leftBurst;
    [SerializeField] private ParticleSystem _rightBurst;
    [SerializeField] private ParticleSystem _extraBurst;
    [SerializeField] private AudioSource _levelMusic;
    [SerializeField, Min(0.01f)] private float _musicFadeDuration = 0.4f;
    [SerializeField] private AudioClip _fanfareClip;
    [SerializeField] private AudioMixerGroup _outputAudioMixerGroup;
    [SerializeField, Range(0f, 1f)] private float _fanfareVolume = 0.8f;

    private Tween _musicFadeTween;

    private void Awake()
    {
        ValidateDependencies();
    }

    private void OnDisable()
    {
        _musicFadeTween?.Kill();
        _musicFadeTween = null;
    }

    public void Play()
    {
        _musicFadeTween?.Kill();
        _musicFadeTween = _levelMusic
            .DOFade(0f, _musicFadeDuration)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        _leftBurst.Play(true);
        _rightBurst.Play(true);
    }

    public void PlayFanfare()
    {
        UiAudioPlayer.Play(_fanfareClip, _outputAudioMixerGroup, _fanfareVolume);
    }

    public void PlayExtraBurst()
    {
        _extraBurst.Play(true);
    }

    private void ValidateDependencies()
    {
        if (_leftBurst == null || _rightBurst == null || _extraBurst == null)
            throw new InvalidOperationException(nameof(ParticleSystem));

        if (_levelMusic == null)
            throw new InvalidOperationException(nameof(_levelMusic));

        if (_fanfareClip == null || _outputAudioMixerGroup == null)
            throw new InvalidOperationException(nameof(_fanfareClip));
    }
}
