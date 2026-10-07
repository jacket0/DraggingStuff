using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class ShelfFilterAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip _rejectClip;
    [SerializeField, Range(0f, 1f)] private float _rejectVolume = 0.25f;
    [SerializeField] private AudioClip _boxReminderClip;
    [SerializeField, Range(0f, 1f)] private float _boxReminderVolume = 0.5f;

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

    public void PlayBoxReminder()
    {
        if (_boxReminderClip == null)
            return;

        _audioSource.PlayOneShot(_boxReminderClip, _boxReminderVolume);
    }
}
