using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelUiController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const int ReviewLevelNumber = 3;

    [SerializeField] private PauseWindowView _pauseWindowView;
    [SerializeField] private LevelHudView _levelHudView;
    [SerializeField] private LevelCompletionView _levelCompletionView;
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private ScoreSystem _scoreSystem;
    [SerializeField] private YandexGameReviewService _gameReviewService;

    private IGameReviewService GameReviewService => _gameReviewService;

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
        _pauseWindowView.MenuRequested -= HandleMenuRequest;
        _levelCompletionView.ReviewRequested -= HandleReviewRequested;
    }

    private void HandlePauseRequested()
    {
        if (_levelSession.TryPauseLevel())
            _pauseWindowView.Show();
    }

    private void HandleResumeRequested()
    {
        _levelSession.ResumeLevel();
        _pauseWindowView.Hide();
    }

    private void HandleRestartRequested()
    {
        _levelSession.RestartLevel();
    }

    private void HandleLevelCompleted()
    {
        bool isReviewAvailable =
            _levelSession.CurrentLevel.Number == ReviewLevelNumber &&
            GameReviewService.CanRequest;

        _levelHudView.Hide();
        _pauseWindowView.Hide();
        _levelCompletionView.Show(_scoreSystem.CurrentScore, isReviewAvailable);
    }

    private void HandleMenuRequest()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void HandleReviewRequested()
    {
        if (GameReviewService.TryRequest())
            _levelCompletionView.HideReviewButton();
    }
}
