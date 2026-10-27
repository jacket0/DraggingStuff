using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class SlimeAudioPlayer : MonoBehaviour
{
    private const int VoiceCount = 4;

    [SerializeField] private AudioClip _growClip;
    [SerializeField] private AudioClip _stuckClip;
    [SerializeField] private AudioClip _cleanClip;
    [SerializeField] private AudioClip _dryClip;
    [SerializeField] private AudioClip _lossClip;

    [SerializeField, Range(0f, 1f)] private float _growVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _stuckVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _cleanVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _dryVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _lossVolume = 0.45f;

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

    public void PlayGrow() => Play(_growClip, _growVolume, Random.Range(0.94f, 1.06f));

    public void PlayStuck() => Play(_stuckClip, _stuckVolume, Random.Range(0.92f, 1.08f));

    public void PlayClean() => Play(_cleanClip, _cleanVolume, Random.Range(0.95f, 1.05f));

    public void PlayDry() => Play(_dryClip, _dryVolume, 1f);

    public void PlayLoss() => Play(_lossClip, _lossVolume, 1f);

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
