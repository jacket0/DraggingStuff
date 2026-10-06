using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class ShelfFilterAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip _rejectClip;
    [SerializeField, Range(0f, 1f)] private float _rejectVolume = 0.25f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void PlayReject()
    {
        if (_rejectClip == null)
            return;

        _audioSource.PlayOneShot(_rejectClip, _rejectVolume);
    }
}
