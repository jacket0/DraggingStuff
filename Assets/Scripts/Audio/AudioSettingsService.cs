using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioSettingsService : MonoBehaviour
{
    private const string SfxVolumeKey = "audio.sfx.volume";
    private const string MusicVolumeKey = "audio.music.volume";
    private const string SfxMutedKey = "audio.sfx.muted";
    private const string MusicMutedKey = "audio.music.muted";

    private const float MutedDecibels = -80f;
    private const float Epsilon = 0.0001f;

    private const float DefaultSfxVolume = 0.8f;
    private const float DefaultMusicVolume = 0.7f;

    private const int MutedState = 1;
    private const int UnmutedState = 0;

    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private string _sfxParameter = "SfxVolume";
    [SerializeField] private string _musicParameter = "MusicVolume";
    [SerializeField, Min(0f)] private float _sfxMuteFadeDuration = 0.2f;

    private float _sfxVolume;
    private float _musicVolume;
    private bool _isSfxMuted;
    private bool _isMusicMuted;
    private Coroutine _sfxMuteFade;

    public event Action SettingsChanged;

    public float SfxVolume => _sfxVolume;
    public float MusicVolume => _musicVolume;
    public bool IsSfxMuted => _isSfxMuted;
    public bool IsMusicMuted => _isMusicMuted;

    private void Awake()
    {
        _sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);

        _musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);

        _isSfxMuted = PlayerPrefs.GetInt(SfxMutedKey, UnmutedState) == MutedState;

        _isMusicMuted = PlayerPrefs.GetInt(MusicMutedKey, UnmutedState) == MutedState;
    }

    private void Start()
    {
        ApplySfx();
        ApplyMusic();
    }

    public void SetSfxVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        _isSfxMuted = _sfxVolume <= Epsilon;

        StopSfxMuteFade();
        ApplySfx();
        Save();
        SettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        _isMusicMuted = _musicVolume <= Epsilon;

        ApplyMusic();
        Save();
        SettingsChanged?.Invoke();
    }

    public void ToggleSfxMute()
    {
        _isSfxMuted = !_isSfxMuted;

        if (!_isSfxMuted && _sfxVolume <= Epsilon)
            _sfxVolume = DefaultSfxVolume;

        StopSfxMuteFade();

        if (_isSfxMuted)
            _sfxMuteFade = StartCoroutine(FadeSfxToMuted());
        else
            ApplySfx();

        Save();
        SettingsChanged?.Invoke();
    }

    public void ToggleMusicMute()
    {
        _isMusicMuted = !_isMusicMuted;

        if (!_isMusicMuted && _musicVolume <= Epsilon)
            _musicVolume = DefaultMusicVolume;

        ApplyMusic();
        Save();
        SettingsChanged?.Invoke();
    }

    private void ApplyMusic()
    {
        SetMixerVolume(_musicParameter, _musicVolume, _isMusicMuted);
    }

    private void ApplySfx()
    {
        SetMixerVolume(_sfxParameter, _sfxVolume, _isSfxMuted);
    }

    private IEnumerator FadeSfxToMuted()
    {
        if (_sfxMuteFadeDuration <= 0f)
        {
            ApplySfx();
            _sfxMuteFade = null;
            yield break;
        }

        if (!_audioMixer.GetFloat(_sfxParameter, out float initialDecibels))
            initialDecibels = Mathf.Log10(Mathf.Max(_sfxVolume, Epsilon)) * 20f;

        float elapsedTime = 0f;

        while (elapsedTime < _sfxMuteFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / _sfxMuteFadeDuration);
            _audioMixer.SetFloat(_sfxParameter, Mathf.Lerp(initialDecibels, MutedDecibels, progress * progress));
            yield return null;
        }

        _audioMixer.SetFloat(_sfxParameter, MutedDecibels);
        _sfxMuteFade = null;
    }

    private void StopSfxMuteFade()
    {
        if (_sfxMuteFade == null)
            return;

        StopCoroutine(_sfxMuteFade);
        _sfxMuteFade = null;
    }

    private void SetMixerVolume(string parameter, float volume, bool muted)
    {
        float decibels = muted || volume <= Epsilon ? MutedDecibels : Mathf.Log10(volume) * 20f;
        _audioMixer.SetFloat(parameter, decibels);
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(SfxVolumeKey, _sfxVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
        PlayerPrefs.SetInt(SfxMutedKey, _isSfxMuted ? MutedState : UnmutedState);
        PlayerPrefs.SetInt(MusicMutedKey, _isMusicMuted ? MutedState : UnmutedState);
        PlayerPrefs.Save();
    }
}
