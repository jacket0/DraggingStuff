using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YG;

public class AudioSettingsPanel : MonoBehaviour
{
    private const float MutedIconAlpha = 0.45f;
    private const float ActiveIconAlpha = 1f;
    private const float FractionToPercentMultiplier = 100f;
    private const string PercentTextTemplate = "{0}%";

    [FormerlySerializedAs("_mutedValueText")]
    [SerializeField] private string _russianMutedValueText = "ВЫКЛ";
    [SerializeField] private string _englishMutedValueText = "OFF";
    [SerializeField] private string _turkishMutedValueText = "KAPALI";

    [SerializeField] private AudioSettingsService _audioSettings;
    [SerializeField] private Button _soundMuteButton;
    [SerializeField] private Button _musicMuteButton;

    [SerializeField] private Image _soundIcon;
    [SerializeField] private Image _musicIcon;

    [SerializeField] private Slider _soundSlider;
    [SerializeField] private Slider _musicSlider;

    [SerializeField] private TMP_Text _soundValueText;
    [SerializeField] private TMP_Text _musicValueText;

    private void Awake()
    {
        if (_audioSettings == null)
            throw new InvalidOperationException(nameof(_audioSettings));
    }

    private void OnEnable()
    {
        _soundMuteButton.onClick.AddListener(ToggleSoundMute);
        _musicMuteButton.onClick.AddListener(ToggleMusicMute);

        _soundSlider.onValueChanged.AddListener(ChangeSoundVolume);
        _musicSlider.onValueChanged.AddListener(ChangeMusicVolume);

        _audioSettings.SettingsChanged += Refresh;
        YG2.onSwitchLang += HandleLanguageChanged;

        Refresh();
    }

    private void OnDisable()
    {
        _soundMuteButton.onClick.RemoveListener(ToggleSoundMute);
        _musicMuteButton.onClick.RemoveListener(ToggleMusicMute);

        _soundSlider.onValueChanged.RemoveListener(ChangeSoundVolume);
        _musicSlider.onValueChanged.RemoveListener(ChangeMusicVolume);

        _audioSettings.SettingsChanged -= Refresh;
        YG2.onSwitchLang -= HandleLanguageChanged;
    }

    private void ToggleSoundMute()
    {
        _audioSettings.ToggleSfxMute();
    }

    private void ToggleMusicMute()
    {
        _audioSettings.ToggleMusicMute();
    }

    private void ChangeSoundVolume(float volume)
    {
        _audioSettings.SetSfxVolume(volume);
    }

    private void ChangeMusicVolume(float volume)
    {
        _audioSettings.SetMusicVolume(volume);
    }

    private void HandleLanguageChanged(string language)
    {
        Refresh();
    }

    private void Refresh()
    {
        _soundSlider.SetValueWithoutNotify(_audioSettings.SfxVolume);
        _musicSlider.SetValueWithoutNotify(_audioSettings.MusicVolume);

        SetVolumeText(
            _soundValueText,
            _audioSettings.SfxVolume,
            _audioSettings.IsSfxMuted);

        SetVolumeText(
            _musicValueText,
            _audioSettings.MusicVolume,
            _audioSettings.IsMusicMuted);

        SetIconAlpha(_soundIcon, _audioSettings.IsSfxMuted);
        SetIconAlpha(_musicIcon, _audioSettings.IsMusicMuted);
    }

    private void SetVolumeText(TMP_Text target, float normalizedVolume, bool muted)
    {
        if (muted)
        {
            target.SetText(GetMutedValueText());
            return;
        }

        int percent = Mathf.RoundToInt(normalizedVolume * FractionToPercentMultiplier);

        target.SetText(PercentTextTemplate, percent);
    }

    private string GetMutedValueText()
    {
        switch (YG2.lang)
        {
            case EnableLanguages.RussianLanguageCode:
                return _russianMutedValueText;

            case EnableLanguages.TurkishLanguageCode:
                return _turkishMutedValueText;

            default:
                return _englishMutedValueText;
        }
    }

    private static void SetIconAlpha(Image icon, bool muted)
    {
        Color color = icon.color;
        color.a = muted ? MutedIconAlpha : ActiveIconAlpha;
        icon.color = color;
    }
}
