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
    [SerializeField] private LanguageYG _victoryLocalization;
    [SerializeField] private LanguageYG _defeatLocalization;
    [SerializeField] private StarRatingView _earnedStars;
    [SerializeField] private TMP_Text _timeResult;
    [SerializeField] private TMP_Text _bestTimeResult;
    [SerializeField] private TMP_Text _bestScoreResult;
    [SerializeField] private GameObject _newRecordRoot;
    [SerializeField] private GameObject _remainingItemsRoot;
    [SerializeField] private TMP_Text _remainingItemsResult;
    [SerializeField] private RectTransform _lightRays;
    [SerializeField] private GameObject _sparkles;
    [SerializeField] private Image _defeatClock;

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
    }

    private void OnDisable()
    {
        _restartButton?.onClick.RemoveListener(RestartButtonClicked);
        _menuButton?.onClick.RemoveListener(MenuButtonClicked);
        _reviewButton?.onClick.RemoveListener(ReviewButtonClicked);
        _nextButton?.onClick.RemoveListener(NextButtonClicked);
        _raysTween?.Kill();
        _raysTween = null;
    }

    public void Show(LevelRunResult result, long bestTimeMilliseconds, long bestScore, bool isReviewAvailable, bool opensEndlessMode, bool isNewRecord)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        _finalScoreResult.SetText(result.Score.ToString());
        _bestScoreResult.SetText(bestScore.ToString());
        _timeResult.SetText(TimeTextFormatter.FormatMilliseconds(result.ActiveTimeMilliseconds));
        _bestTimeResult.SetText(TimeTextFormatter.FormatMilliseconds(bestTimeMilliseconds));
        _remainingItemsResult.SetText(result.RemainingItemCount.ToString());

        SetResultMode(result.Won);
        _timeResult.transform.parent.gameObject.SetActive(result.Won);
        _bestTimeResult.transform.parent.gameObject.SetActive(result.Won);
        _bestScoreResult.transform.parent.gameObject.SetActive(result.Won);
        _reviewButton.gameObject.SetActive(result.Won && isReviewAvailable);
        _nextButton.gameObject.SetActive(result.Won);
        _remainingItemsRoot.SetActive(!result.Won);

        if (_newRecordRoot != null)
            _newRecordRoot.SetActive(result.Won && isNewRecord);

        SetNextButtonText(opensEndlessMode);
        gameObject.SetActive(true);

        if (result.Won)
        {
            _earnedStars.RevealRating(result.Stars);
            StartRaysAnimation(result.Stars);
        }
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

    private void SetResultMode(bool won)
    {
        _victoryLocalization.enabled = won;
        _defeatLocalization.enabled = !won;
        _earnedStars.gameObject.SetActive(won);
        _lightRays.gameObject.SetActive(won);
        _sparkles.SetActive(won);
        _defeatClock.gameObject.SetActive(!won);

        LanguageYG title = won ? _victoryLocalization : _defeatLocalization;

        if (title.gameObject.activeInHierarchy)
            title.SwitchLanguage();
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

        if (_nextLevelLocalization == null || _endlessModeLocalization == null || _victoryLocalization == null || _defeatLocalization == null)
            throw new InvalidOperationException(nameof(LanguageYG));

        if (_earnedStars == null || _remainingItemsRoot == null || _remainingItemsResult == null || _lightRays == null || _sparkles == null || _defeatClock == null)
            throw new InvalidOperationException(nameof(LevelResultView));
    }
}
