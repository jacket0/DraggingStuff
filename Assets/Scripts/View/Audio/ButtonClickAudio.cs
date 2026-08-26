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

    private void Awake()
    {
        _button = GetComponent<Button>();
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
        UiAudioPlayer.Play(_clickClip, _outputAudioMixerGroup, _volume);
    }
}
