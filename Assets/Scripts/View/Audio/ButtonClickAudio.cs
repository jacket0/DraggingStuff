using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ButtonClickAudio : MonoBehaviour
{
    [SerializeField] private AudioClip _clickClip;
    [SerializeField] private AudioMixerGroup _outputAudioMixerGroup;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.6f;

    private Button _button;
    private AudioSource _audioSource;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.outputAudioMixerGroup = _outputAudioMixerGroup;
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(PlayClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(PlayClick);
    }

    private void PlayClick()
    {
        if (_clickClip != null)
            _audioSource.PlayOneShot(_clickClip, _volume);
    }
}
