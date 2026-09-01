using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;

public sealed class MainMenuAccountController : MonoBehaviour
{
    [SerializeField] private Button _accountButton;
    [SerializeField] private TMP_Text _accountButtonText;
    [SerializeField] private GameObject _dialogRoot;
    [SerializeField] private TMP_Text _dialogTitleText;
    [SerializeField] private TMP_Text _dialogMessageText;
    [SerializeField] private Button _authorizeButton;
    [SerializeField] private TMP_Text _authorizeButtonText;
    [SerializeField] private Button _closeButton;

    private readonly YandexPlayerAccountService _accountService = new YandexPlayerAccountService();

    private void Awake()
    {
        ValidateDependencies();
        _dialogRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _accountButton.onClick.AddListener(OpenDialog);
        _authorizeButton.onClick.AddListener(RequestAuthorization);
        _closeButton.onClick.AddListener(CloseDialog);
        _accountService.StateChanged += Refresh;
        YG2.onSwitchLang += ApplyLanguage;
        _accountService.Enable();
        Refresh();
    }

    private void OnDisable()
    {
        _accountButton.onClick.RemoveListener(OpenDialog);
        _authorizeButton.onClick.RemoveListener(RequestAuthorization);
        _closeButton.onClick.RemoveListener(CloseDialog);
        _accountService.StateChanged -= Refresh;
        YG2.onSwitchLang -= ApplyLanguage;
        _accountService.Disable();
    }

    private void OpenDialog()
    {
        Refresh();
        _dialogRoot.SetActive(true);
    }

    private void CloseDialog()
    {
        _dialogRoot.SetActive(false);
    }

    private void RequestAuthorization()
    {
        if (_accountService.TryRequestAuthorization())
            CloseDialog();
    }

    private void Refresh()
    {
        PlayerAccountState state = _accountService.State;
        string language = YG2.lang;

        _accountButtonText.text = state == PlayerAccountState.AuthorizedNamed
            ? _accountService.PlayerName
            : GetAccountName(state, language);

        _dialogTitleText.text = GetDialogTitle(state, language);
        _dialogMessageText.text = GetDialogMessage(state, language);
        bool authorizationAvailable = state == PlayerAccountState.Guest;
        _authorizeButton.gameObject.SetActive(authorizationAvailable);
        _authorizeButtonText.text = GetSignInText(language);
    }

    private void ApplyLanguage(string language)
    {
        Refresh();
    }

    private static string GetAccountName(PlayerAccountState state, string language)
    {
        if (state == PlayerAccountState.AuthorizedAnonymous)
        {
            return language switch
            {
                EnableLanguages.RussianLanguageCode => "Аноним",
                EnableLanguages.TurkishLanguageCode => "Anonim",
                _ => "Anonymous"
            };
        }

        return GetSignInText(language);
    }

    private static string GetDialogTitle(PlayerAccountState state, string language)
    {
        if (state == PlayerAccountState.Guest)
        {
            return language switch
            {
                EnableLanguages.RussianLanguageCode => "СОХРАНИТЬ ПРОГРЕСС",
                EnableLanguages.TurkishLanguageCode => "İLERLEMEYİ KAYDET",
                _ => "SAVE PROGRESS"
            };
        }

        if (state == PlayerAccountState.AuthorizedAnonymous)
        {
            return language switch
            {
                EnableLanguages.RussianLanguageCode => "АНОНИМНЫЙ АККАУНТ",
                EnableLanguages.TurkishLanguageCode => "ANONİM HESAP",
                _ => "ANONYMOUS ACCOUNT"
            };
        }

        return language switch
        {
            EnableLanguages.RussianLanguageCode => "АККАУНТ",
            EnableLanguages.TurkishLanguageCode => "HESAP",
            _ => "ACCOUNT"
        };
    }

    private static string GetDialogMessage(PlayerAccountState state, string language)
    {
        if (state == PlayerAccountState.Guest)
        {
            return language switch
            {
                EnableLanguages.RussianLanguageCode => "Войдите в аккаунт Яндекса, чтобы сохранять прогресс в облаке и продолжать игру на других устройствах.",
                EnableLanguages.TurkishLanguageCode => "İlerlemenizi buluta kaydetmek ve diğer cihazlarda devam etmek için Yandex hesabınıza giriş yapın.",
                _ => "Sign in to your Yandex account to save progress in the cloud and continue on other devices."
            };
        }

        if (state == PlayerAccountState.AuthorizedAnonymous)
        {
            return language switch
            {
                EnableLanguages.RussianLanguageCode => "Вы вошли анонимно. Прогресс синхронизируется с вашим аккаунтом Яндекса.",
                EnableLanguages.TurkishLanguageCode => "Anonim olarak giriş yaptınız. İlerlemeniz Yandex hesabınızla eşitleniyor.",
                _ => "You are signed in anonymously. Progress is synchronized with your Yandex account."
            };
        }

        return language switch
        {
            EnableLanguages.RussianLanguageCode => "Прогресс синхронизируется с вашим аккаунтом Яндекса.",
            EnableLanguages.TurkishLanguageCode => "İlerlemeniz Yandex hesabınızla eşitleniyor.",
            _ => "Progress is synchronized with your Yandex account."
        };
    }

    private static string GetSignInText(string language)
    {
        return language switch
        {
            EnableLanguages.RussianLanguageCode => "ВОЙТИ",
            EnableLanguages.TurkishLanguageCode => "GİRİŞ YAP",
            _ => "SIGN IN"
        };
    }

    private void ValidateDependencies()
    {
        if (_accountButton == null || _accountButtonText == null || _dialogRoot == null ||
            _dialogTitleText == null || _dialogMessageText == null || _authorizeButton == null ||
            _authorizeButtonText == null || _closeButton == null)
        {
            throw new InvalidOperationException(nameof(MainMenuAccountController));
        }
    }
}
