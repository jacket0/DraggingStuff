using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class ClosedShelfAudioPlayer : MonoBehaviour
{
    private const int MaximumStarStep = 12;
    private const int VoiceCount = 6;

    [SerializeField] private AudioClip _starClip;
    [SerializeField] private AudioClip _chordClip;
    [SerializeField] private AudioClip _dustClip;
    [SerializeField] private AudioClip _popClip;
    [SerializeField] private AudioClip _rejectClip;

    [SerializeField, Range(0f, 1f)] private float _starVolume = 0.225f;
    [SerializeField, Range(0f, 1f)] private float _chordVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _dustVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _popVolume = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _rejectVolume = 0.25f;
    [SerializeField, Min(1f)] private float _finalChordPitch = 1.122f;

    private AudioSource[] _voices;
    private int _nextVoice;

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

    public void PlayStar(int step) => Play(_starClip, _starVolume, Mathf.Pow(2f, Mathf.Clamp(step, 0, MaximumStarStep) / 12f));

    public void PlayChord(bool isFinal) => Play(_chordClip, _chordVolume, isFinal ? _finalChordPitch : 1f);

    public void PlayDust() => Play(_dustClip, _dustVolume, 1f);

    public void PlayPop() => Play(_popClip, _popVolume, Random.Range(0.95f, 1.1f));

    public void PlayReject() => Play(_rejectClip, _rejectVolume, 1f);

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
