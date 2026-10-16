using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class CyclicShelfAudioPlayer : MonoBehaviour
{
    private const int VoiceCount = 4;

    [SerializeField] private AudioClip _flipClip;
    [SerializeField] private AudioClip _warningClip;
    [SerializeField] private AudioClip _shutterDownClip;
    [SerializeField] private AudioClip _shutterUpClip;

    [SerializeField, Range(0f, 1f)] private float _flipVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _warningVolume = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _shutterDownVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _shutterUpVolume = 0.3f;

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

    public void PlayFlip() => Play(_flipClip, _flipVolume, Random.Range(0.95f, 1.08f));

    public void PlayWarning() => Play(_warningClip, _warningVolume, 1f);

    public void PlayShutterDown() => Play(_shutterDownClip, _shutterDownVolume, Random.Range(0.97f, 1.03f));

    public void PlayShutterUp() => Play(_shutterUpClip, _shutterUpVolume, Random.Range(0.97f, 1.03f));

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
