using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelUiController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string EndlessLevelSceneName = "EndlessLevel";
    private const int ReviewLevelNumber = 3;

    [SerializeField] private PauseWindowView _pauseWindowView;
    [SerializeField] private LevelHudView _levelHudView;
    [SerializeField] private LevelCompletionView _levelCompletionView;
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private ScoreSystem _scoreSystem;
    [SerializeField] private YandexGameReviewService _gameReviewService;
    [SerializeField] private YandexInterstitialAdService _interstitialAdService;
    [SerializeField] private LevelCatalog _levelCatalog;
    [SerializeField] private LevelSelectionState _levelSelectionState;

    private bool _isTransitionPending;

    private IGameReviewService GameReviewService => _gameReviewService;
    private IInterstitialAdService InterstitialAdService => _interstitialAdService;

    private void Start()
    {
        _levelHudView.Show();
        _pauseWindowView.Hide();
        _levelCompletionView.Hide();
    }

    private void OnEnable()
    {
        _levelHudView.PauseRequested += HandlePauseRequested;
        _pauseWindowView.ResumeRequested += HandleResumeRequested;
        _pauseWindowView.RestartRequested += HandleRestartRequested;
        _levelCompletionView.RestartRequested += HandleRestartRequested;
        _levelSession.LevelCompleted += HandleLevelCompleted;
        _levelCompletionView.MenuRequested += HandleMenuRequest;
        _levelCompletionView.NextRequested += HandleNextLevelRequest;
        _pauseWindowView.MenuRequested += HandleMenuRequest;
        _levelCompletionView.ReviewRequested += HandleReviewRequested;
    }

    private void OnDisable()
    {
        _levelHudView.PauseRequested -= HandlePauseRequested;
        _pauseWindowView.ResumeRequested -= HandleResumeRequested;
        _pauseWindowView.RestartRequested -= HandleRestartRequested;
        _levelCompletionView.RestartRequested -= HandleRestartRequested;
        _levelSession.LevelCompleted -= HandleLevelCompleted;
        _levelCompletionView.MenuRequested -= HandleMenuRequest;
        _levelCompletionView.NextRequested -= HandleNextLevelRequest;
        _pauseWindowView.MenuRequested -= HandleMenuRequest;
        _levelCompletionView.ReviewRequested -= HandleReviewRequested;
    }

    private void HandlePauseRequested()
    {
        if (_levelSession.TryPause())
            _pauseWindowView.Show();
    }

    private void HandleResumeRequested()
    {
        _levelSession.Resume();
        _pauseWindowView.Hide();
    }

    private void HandleRestartRequested()
    {
        if (_isTransitionPending)
            return;

        _isTransitionPending = true;
        _levelSession.Restart();
    }

    private void HandleLevelCompleted()
    {
        bool isReviewAvailable =
            _levelSession.CurrentLevel.Number == ReviewLevelNumber &&
            GameReviewService.CanRequest;
        bool opensEndlessMode = !_levelCatalog.TryGetNext(_levelSession.CurrentLevel, out _);

        _levelHudView.Hide();
        _pauseWindowView.Hide();
        _levelCompletionView.Show(_scoreSystem.CurrentScore, isReviewAvailable, opensEndlessMode);
    }

    private void HandleMenuRequest()
    {
        if (_isTransitionPending)
            return;

        _isTransitionPending = true;
        _levelCompletionView.SetInteractable(false);
        InterstitialAdService.Show(OpenMainMenu);
    }

    private static void OpenMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void HandleReviewRequested()
    {
        if (_isTransitionPending)
            return;

        if (GameReviewService.TryRequest())
            _levelCompletionView.HideReviewButton();
    }

    private void HandleNextLevelRequest()
    {
        if (_isTransitionPending || _levelSession.State != LevelState.Won)
            return;

        _isTransitionPending = true;
        _levelCompletionView.SetInteractable(false);
        InterstitialAdService.Show(OpenNextLevel);
    }

    private void OpenNextLevel()
    {
        Time.timeScale = 1f;

        if (_levelCatalog.TryGetNext(_levelSession.CurrentLevel, out LevelEntry nextLevel))
        {
            _levelSelectionState.Select(nextLevel);
            SceneManager.LoadScene(nextLevel.SceneName);
            return;
        }

        SceneManager.LoadScene(EndlessLevelSceneName);
    }
}
