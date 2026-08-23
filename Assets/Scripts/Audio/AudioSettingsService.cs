using System;
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

    private float _sfxVolume;
    private float _musicVolume;
    private bool _isSfxMuted;
    private bool _isMusicMuted;

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
