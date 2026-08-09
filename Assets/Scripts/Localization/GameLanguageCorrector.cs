using UnityEngine;
using YG;

public static class GameLanguageCorrector
{

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        YG2.onCorrectLang += Correct;
    }

    private static void Correct(string language)
    {
        if (language != EnableLanguages.RussianLanguageCode && language != EnableLanguages.EnglishLanguageCode && language != EnableLanguages.TurkishLanguageCode)
            YG2.lang = EnableLanguages.EnglishLanguageCode;
    }
}
