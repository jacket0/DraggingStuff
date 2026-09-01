using TMPro;
using UnityEngine;
using YG;

[RequireComponent(typeof(TMP_Text))]
public sealed class LeaderboardAnonymousNameLocalizer : MonoBehaviour
{
    private TMP_Text _nameText;
    private bool _isAnonymous;

    private void Awake()
    {
        _nameText = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        DetectAnonymousName();
        ApplyLanguage(YG2.lang);
    }

    private void OnEnable()
    {
        YG2.onSwitchLang += ApplyLanguage;
    }

    private void OnDisable()
    {
        YG2.onSwitchLang -= ApplyLanguage;
    }

    private void DetectAnonymousName()
    {
        string playerName = _nameText.text;
        _isAnonymous = playerName == InfoYG.ANONYMOUS ||
            playerName == "скрыт" ||
            playerName == "is hidden" ||
            playerName == "gizli";
    }

    private void ApplyLanguage(string language)
    {
        if (!_isAnonymous)
            return;

        _nameText.text = language switch
        {
            EnableLanguages.RussianLanguageCode => "Аноним",
            EnableLanguages.TurkishLanguageCode => "Anonim",
            _ => "Anonymous"
        };
    }
}
