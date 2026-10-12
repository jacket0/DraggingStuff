using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class KeyLockAudioPlayer : MonoBehaviour
{
    private const int VoiceCount = 4;

    [SerializeField] private AudioClip _keyPickupClip;
    [SerializeField] private AudioClip _lockClickClip;
    [SerializeField] private AudioClip _barsClip;

    [SerializeField, Range(0f, 1f)] private float _keyPickupVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _lockClickVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _barsVolume = 0.2f;

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

    public void PlayKeyPickup() => Play(_keyPickupClip, _keyPickupVolume, Random.Range(0.97f, 1.05f));

    public void PlayLockClick() => Play(_lockClickClip, _lockClickVolume, 1f);

    public void PlayBars() => Play(_barsClip, _barsVolume, Random.Range(0.95f, 1.05f));

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
