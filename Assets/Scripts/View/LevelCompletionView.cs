using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG.LanguageLegacy;

public class LevelCompletionView : MonoBehaviour
{
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private TMP_Text _finalScoreResult;
    [SerializeField] private Button _reviewButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private LanguageYG _nextLevelLocalization;
    [SerializeField] private LanguageYG _endlessModeLocalization;

    public event Action MenuRequested;
    public event Action RestartRequested;
    public event Action ReviewRequested;
    public event Action NextRequested;

    private void Awake()
    {
        ValidateDependencies();
    }

    private void OnEnable()
    {
        _restartButton.onClick.AddListener(RestartButtonClicked);
        _menuButton.onClick.AddListener(MenuButtonClicked);
        _reviewButton.onClick.AddListener(ReviewButtonClicked);
        _nextButton.onClick.AddListener(NextButtonClicked);
    }

    private void OnDisable()
    {
        _restartButton?.onClick.RemoveListener(RestartButtonClicked);
        _menuButton?.onClick.RemoveListener(MenuButtonClicked);
        _reviewButton?.onClick.RemoveListener(ReviewButtonClicked);
        _nextButton?.onClick.RemoveListener(NextButtonClicked);
    }

    public void Show(long finalScore, bool isReviewAvailable, bool opensEndlessMode)
    {
        _finalScoreResult.SetText(finalScore.ToString());
        _reviewButton.gameObject.SetActive(isReviewAvailable);
        SetNextButtonText(opensEndlessMode);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void HideReviewButton()
    {
        _reviewButton.gameObject.SetActive(false);
    }

    public void SetInteractable(bool interactable)
    {
        _restartButton.interactable = interactable;
        _menuButton.interactable = interactable;
        _reviewButton.interactable = interactable;
        _nextButton.interactable = interactable;
    }

    private void RestartButtonClicked()
    {
        RestartRequested?.Invoke();   
    }

    private void MenuButtonClicked()
    {
        MenuRequested?.Invoke();
    }

    private void ReviewButtonClicked()
    {
        ReviewRequested?.Invoke();
    }

    private void NextButtonClicked()
    {
        NextRequested?.Invoke();
    }

    private void SetNextButtonText(bool opensEndlessMode)
    {
        LanguageYG selectedLocalization = opensEndlessMode
            ? _endlessModeLocalization
            : _nextLevelLocalization;

        _nextLevelLocalization.enabled = selectedLocalization == _nextLevelLocalization;
        _endlessModeLocalization.enabled = selectedLocalization == _endlessModeLocalization;

        if (selectedLocalization.gameObject.activeInHierarchy)
            selectedLocalization.SwitchLanguage();
    }

    private void ValidateDependencies()
    {
        if (_restartButton == null)
            throw new InvalidOperationException(nameof(_restartButton));

        if (_menuButton == null)
            throw new InvalidOperationException(nameof(_menuButton));

        if (_finalScoreResult == null)
            throw new InvalidOperationException(nameof(_finalScoreResult));

        if (_reviewButton == null)
            throw new InvalidOperationException(nameof(_reviewButton));

        if (_nextButton == null)
            throw new InvalidOperationException(nameof(_nextButton));

        if (_nextLevelLocalization == null)
            throw new InvalidOperationException(nameof(_nextLevelLocalization));

        if (_endlessModeLocalization == null)
            throw new InvalidOperationException(nameof(_endlessModeLocalization));
    }
}
