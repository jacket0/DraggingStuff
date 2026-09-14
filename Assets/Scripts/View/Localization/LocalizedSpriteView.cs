using UnityEngine;
using UnityEngine.UI;
using YG;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class LocalizedSpriteView : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Sprite _englishSprite;
    [SerializeField] private Sprite _russianSprite;
    [SerializeField] private Sprite _turkishSprite;

    private void Awake()
    {
        if (_image == null)
            _image = GetComponent<Image>();

        if (_englishSprite == null)
            _englishSprite = _image.sprite;
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
        Sprite sprite = language switch
        {
            "ru" => _russianSprite,
            "tr" => _turkishSprite,
            _ => _englishSprite
        };

        _image.sprite = sprite != null ? sprite : _englishSprite;
    }
}
