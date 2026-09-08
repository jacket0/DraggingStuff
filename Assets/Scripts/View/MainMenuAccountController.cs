using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG.LanguageLegacy;

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
    [SerializeField] private LanguageYG _signInLocalization;
    [SerializeField] private LanguageYG _anonymousNameLocalization;
    [SerializeField] private LanguageYG _guestTitleLocalization;
    [SerializeField] private LanguageYG _anonymousTitleLocalization;
    [SerializeField] private LanguageYG _accountTitleLocalization;
    [SerializeField] private LanguageYG _guestMessageLocalization;
    [SerializeField] private LanguageYG _anonymousMessageLocalization;
    [SerializeField] private LanguageYG _accountMessageLocalization;
    [SerializeField] private LanguageYG _authorizeButtonLocalization;

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
        _accountService.Enable();
        Refresh();
    }

    private void OnDisable()
    {
        _accountButton.onClick.RemoveListener(OpenDialog);
        _authorizeButton.onClick.RemoveListener(RequestAuthorization);
        _closeButton.onClick.RemoveListener(CloseDialog);
        _accountService.StateChanged -= Refresh;
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
        RefreshAccountButton(state);
        RefreshDialog(state);

        bool authorizationAvailable = state == PlayerAccountState.Guest;
        _authorizeButton.gameObject.SetActive(authorizationAvailable);
        _authorizeButtonLocalization.enabled = authorizationAvailable;

        if (authorizationAvailable && _authorizeButtonLocalization.gameObject.activeInHierarchy)
            _authorizeButtonLocalization.SwitchLanguage();
    }

    private void RefreshAccountButton(PlayerAccountState state)
    {
        bool showsPlayerName = state == PlayerAccountState.AuthorizedNamed;
        _signInLocalization.enabled = state == PlayerAccountState.Guest;
        _anonymousNameLocalization.enabled = state == PlayerAccountState.AuthorizedAnonymous;

        if (showsPlayerName)
        {
            _accountButtonText.text = _accountService.PlayerName;
            return;
        }

        LanguageYG selectedLocalization = state == PlayerAccountState.Guest
            ? _signInLocalization
            : _anonymousNameLocalization;

        if (selectedLocalization.gameObject.activeInHierarchy)
            selectedLocalization.SwitchLanguage();
    }

    private void RefreshDialog(PlayerAccountState state)
    {
        LanguageYG titleLocalization = state switch
        {
            PlayerAccountState.Guest => _guestTitleLocalization,
            PlayerAccountState.AuthorizedAnonymous => _anonymousTitleLocalization,
            _ => _accountTitleLocalization
        };

        LanguageYG messageLocalization = state switch
        {
            PlayerAccountState.Guest => _guestMessageLocalization,
            PlayerAccountState.AuthorizedAnonymous => _anonymousMessageLocalization,
            _ => _accountMessageLocalization
        };

        SelectLocalization(titleLocalization,
            _guestTitleLocalization,
            _anonymousTitleLocalization,
            _accountTitleLocalization);

        SelectLocalization(messageLocalization,
            _guestMessageLocalization,
            _anonymousMessageLocalization,
            _accountMessageLocalization);
    }

    private static void SelectLocalization(
        LanguageYG selected,
        LanguageYG first,
        LanguageYG second,
        LanguageYG third)
    {
        first.enabled = first == selected;
        second.enabled = second == selected;
        third.enabled = third == selected;
        if (selected.gameObject.activeInHierarchy)
            selected.SwitchLanguage();
    }

    private void ValidateDependencies()
    {
        if (_accountButton == null || _accountButtonText == null || _dialogRoot == null ||
            _dialogTitleText == null || _dialogMessageText == null || _authorizeButton == null ||
            _authorizeButtonText == null || _closeButton == null)
        {
            throw new InvalidOperationException(nameof(MainMenuAccountController));
        }

        if (_signInLocalization == null || _anonymousNameLocalization == null ||
            _guestTitleLocalization == null || _anonymousTitleLocalization == null ||
            _accountTitleLocalization == null || _guestMessageLocalization == null ||
            _anonymousMessageLocalization == null || _accountMessageLocalization == null ||
            _authorizeButtonLocalization == null)
        {
            throw new InvalidOperationException(nameof(LanguageYG));
        }
    }
}
