using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class MatchAudioPlayer : MonoBehaviour
{
    private const int VoiceCount = 6;

    [SerializeField] private ComboSystem _comboSystem;
    [SerializeField] private AudioClip _mergeClip;
    [SerializeField] private AudioClip _explosionClip;

    [SerializeField, Range(0f, 1f)] private float _mergeVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _explosionVolume = 0.5f;
    [SerializeField, Range(-12f, 0f)] private float _firstComboSemitones = -2f;
    [SerializeField, Range(0f, 2f)] private float _semitonesPerComboStep = 0.5f;

    private AudioSource[] _voices;
    private int _nextVoice;

    public float CurrentComboPitch
    {
        get
        {
            int comboCount = _comboSystem != null ? Mathf.Max(1, _comboSystem.ComboState.Count) : 1;
            float semitones = _firstComboSemitones + (comboCount - 1) * _semitonesPerComboStep;
            return Mathf.Pow(2f, semitones / 12f);
        }
    }

    private void Awake()
    {
        AudioSource template = GetComponent<AudioSource>();
        _voices = new AudioSource[VoiceCount];
        _voices[0] = template;

        for (int index = 1; index < VoiceCount; index++)
        {
            AudioSource voice = gameObject.AddComponent<AudioSource>();
            voice.outputAudioMixerGroup = template.outputAudioMixerGroup;
            voice.playOnAwake = false;
            voice.spatialBlend = template.spatialBlend;
            _voices[index] = voice;
        }
    }

    public void PlayMerge(float pitch) => Play(_mergeClip, _mergeVolume, pitch);

    public void PlayExplosion(float pitch) => Play(_explosionClip, _explosionVolume, pitch);

    private void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null)
            return;

        AudioSource voice = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Length;
        voice.pitch = pitch;
        voice.PlayOneShot(clip, volume);
    }
}
