using TMPro;
using UnityEngine;
using YG;

[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedTextView : MonoBehaviour
{
    [SerializeField, TextArea] private string _russianText;
    [SerializeField, TextArea] private string _englishText;
    [SerializeField, TextArea] private string _turkishText;

    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        YG2.onSwitchLang += ApplyLanguage;
        ApplyLanguage(YG2.lang);
    }

    private void OnDisable()
    {
        YG2.onSwitchLang -= ApplyLanguage;
    }

    private void ApplyLanguage(string language)
    {
        switch (language)
        {
            case EnableLanguages.RussianLanguageCode:
                _text.text = _russianText;
                break;

            case EnableLanguages.TurkishLanguageCode:
                _text.text = _turkishText;
                break;

            default:
                _text.text = _englishText;
                break;
        }
    }
}