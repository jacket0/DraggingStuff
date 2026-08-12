using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MatchAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip _mergeClip;
    [SerializeField] private AudioClip _explosionClip;

    [SerializeField, Range(0f, 1f)] private float _mergeVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float _explosionVolume = 0.5f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void PlayMerge()
    {
        PlayClip(_mergeClip, _mergeVolume);
    }

    public void PlayExplosion()
    {
        PlayClip(_explosionClip, _explosionVolume);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null) return;

        _audioSource.PlayOneShot(clip, volume);
    }
}
