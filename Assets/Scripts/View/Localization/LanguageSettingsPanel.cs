using System;
using UnityEngine;

public sealed class LanguageSettingsPanel : MonoBehaviour
{
    [SerializeField] private LanguageOptionButton[] _options;

    private void Awake()
    {
        if (_options == null || _options.Length == 0 || Array.Exists(_options, option => option == null))
            throw new InvalidOperationException(nameof(_options));
    }

    private void OnEnable()
    {
        foreach (LanguageOptionButton option in _options)
            option.Clicked += Select;

        RefreshSelection();
    }

    private void OnDisable()
    {
        foreach (LanguageOptionButton option in _options)
            option.Clicked -= Select;
    }

    private void Select(GameLanguage language)
    {
        LanguagePreference.Select(language);
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        GameLanguage selected = LanguagePreference.Selected;

        foreach (LanguageOptionButton option in _options)
            option.SetSelected(option.Language == selected);
    }
}
