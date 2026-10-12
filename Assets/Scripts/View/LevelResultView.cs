using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG.LanguageLegacy;

public sealed class LevelResultView : MonoBehaviour
{
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private TMP_Text _finalScoreResult;
    [SerializeField] private Button _reviewButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private LanguageYG _nextLevelLocalization;
    [SerializeField] private LanguageYG _endlessModeLocalization;
    [SerializeField] private StarRatingView _earnedStars;
    [SerializeField] private TMP_Text _timeResult;
    [SerializeField] private TMP_Text _bestTimeResult;
    [SerializeField] private TMP_Text _bestScoreResult;
    [SerializeField] private GameObject _newRecordRoot;
    [SerializeField] private RectTransform _lightRays;
    [SerializeField] private GameObject _sparkles;
    [SerializeField] private GameObject _victoryPanel;
    [SerializeField] private DefeatResultView _defeatPanel;

    private Tween _raysTween;

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
        _defeatPanel.RestartRequested += RestartButtonClicked;
        _defeatPanel.MenuRequested += MenuButtonClicked;
    }

    private void OnDisable()
    {
        _restartButton?.onClick.RemoveListener(RestartButtonClicked);
        _menuButton?.onClick.RemoveListener(MenuButtonClicked);
        _reviewButton?.onClick.RemoveListener(ReviewButtonClicked);
        _nextButton?.onClick.RemoveListener(NextButtonClicked);

        if (_defeatPanel != null)
        {
            _defeatPanel.RestartRequested -= RestartButtonClicked;
            _defeatPanel.MenuRequested -= MenuButtonClicked;
        }

        _raysTween?.Kill();
        _raysTween = null;
    }

    public void Show(LevelRunResult result, long bestTimeMilliseconds, long bestScore, bool isReviewAvailable, bool opensEndlessMode, bool isNewRecord)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        gameObject.SetActive(true);
        SetResultMode(result.Won);

        if (result.Won)
            ShowVictory(result, bestTimeMilliseconds, bestScore, isReviewAvailable, opensEndlessMode, isNewRecord);
        else
            _defeatPanel.Show(result, bestScore);
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
        _defeatPanel.SetInteractable(interactable);
    }

    private void ShowVictory(LevelRunResult result, long bestTimeMilliseconds, long bestScore, bool isReviewAvailable, bool opensEndlessMode, bool isNewRecord)
    {
        _finalScoreResult.SetText(result.Score.ToString());
        _bestScoreResult.SetText(bestScore.ToString());
        _timeResult.SetText(TimeTextFormatter.FormatMilliseconds(result.ActiveTimeMilliseconds));
        _bestTimeResult.SetText(TimeTextFormatter.FormatMilliseconds(bestTimeMilliseconds));
        _reviewButton.gameObject.SetActive(isReviewAvailable);

        if (_newRecordRoot != null)
            _newRecordRoot.SetActive(isNewRecord);

        SetNextButtonText(opensEndlessMode);
        _earnedStars.RevealRating(result.Stars);
        StartRaysAnimation(result.Stars);
    }

    private void SetResultMode(bool won)
    {
        _victoryPanel.SetActive(won);
        _lightRays.gameObject.SetActive(won);
        _sparkles.SetActive(won);

        if (won)
            _defeatPanel.Hide();
    }

    private void StartRaysAnimation(int stars)
    {
        _raysTween?.Kill();
        _lightRays.localRotation = Quaternion.identity;
        float duration = stars == 3 ? 14f : 20f;
        _raysTween = _lightRays
            .DORotate(new Vector3(0f, 0f, 360f), duration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
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
        LanguageYG selectedLocalization = opensEndlessMode ? _endlessModeLocalization : _nextLevelLocalization;
        _nextLevelLocalization.enabled = selectedLocalization == _nextLevelLocalization;
        _endlessModeLocalization.enabled = selectedLocalization == _endlessModeLocalization;

        if (selectedLocalization.gameObject.activeInHierarchy)
            selectedLocalization.SwitchLanguage();
    }

    private void ValidateDependencies()
    {
        if (_restartButton == null || _menuButton == null || _reviewButton == null || _nextButton == null)
            throw new InvalidOperationException(nameof(Button));

        if (_finalScoreResult == null || _timeResult == null || _bestTimeResult == null || _bestScoreResult == null)
            throw new InvalidOperationException(nameof(TMP_Text));

        if (_nextLevelLocalization == null || _endlessModeLocalization == null)
            throw new InvalidOperationException(nameof(LanguageYG));

        if (_earnedStars == null || _lightRays == null || _sparkles == null || _victoryPanel == null || _defeatPanel == null)
            throw new InvalidOperationException(nameof(LevelResultView));
    }
}
