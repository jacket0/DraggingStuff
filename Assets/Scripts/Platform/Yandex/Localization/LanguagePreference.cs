using System;
using UnityEngine;
using YG;

public static class LanguagePreference
{
    private const string LanguageKey = "settings.language";

    public static GameLanguage Selected
    {
        get
        {
            int value = PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Auto);
            return Enum.IsDefined(typeof(GameLanguage), value) ? (GameLanguage)value : GameLanguage.Auto;
        }
    }

    public static void Select(GameLanguage language)
    {
        if (!Enum.IsDefined(typeof(GameLanguage), language))
            throw new ArgumentOutOfRangeException(nameof(language));

        PlayerPrefs.SetInt(LanguageKey, (int)language);
        PlayerPrefs.Save();
        Apply(language);
    }

    public static void ApplySaved()
    {
        GameLanguage language = Selected;

        if (language != GameLanguage.Auto)
            Apply(language);
    }

    private static void Apply(GameLanguage language)
    {
        if (language == GameLanguage.Auto)
        {
            YG2.GetLanguage();
            return;
        }

        YG2.SwitchLanguage(GetLanguageCode(language));
    }

    private static string GetLanguageCode(GameLanguage language) => language switch
    {
        GameLanguage.Russian => "ru",
        GameLanguage.English => "en",
        GameLanguage.Turkish => "tr",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };
}
