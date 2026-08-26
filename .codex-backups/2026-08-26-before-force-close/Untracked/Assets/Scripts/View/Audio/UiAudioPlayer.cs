using UnityEngine;
using UnityEngine.Audio;

public sealed class UiAudioPlayer : MonoBehaviour
{
    private static UiAudioPlayer _instance;

    private AudioSource _audioSource;

    public static void Play(AudioClip clip, AudioMixerGroup outputAudioMixerGroup, float volume)
    {
        if (clip == null)
            return;

        UiAudioPlayer player = GetOrCreate();
        player._audioSource.outputAudioMixerGroup = outputAudioMixerGroup;
        player._audioSource.PlayOneShot(clip, volume);
    }

    private static UiAudioPlayer GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        GameObject gameObject = new GameObject(nameof(UiAudioPlayer));
        DontDestroyOnLoad(gameObject);

        _instance = gameObject.AddComponent<UiAudioPlayer>();
        _instance._audioSource = gameObject.AddComponent<AudioSource>();
        _instance._audioSource.playOnAwake = false;
        _instance._audioSource.loop = false;
        _instance._audioSource.spatialBlend = 0f;

        return _instance;
    }
}
