using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelUiController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    [SerializeField] private PauseWindowView _pauseWindowView;
    [SerializeField] private LevelHudView _levelHudView;
    [SerializeField] private LevelCompletionView _levelCompletionView;
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private ScoreSystem _scoreSystem;

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
        _levelHudView.Hide();
        _pauseWindowView.Hide();
        _levelCompletionView.Show(_scoreSystem.CurrentScore);
    }

    private void HandleMenuRequest()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(MainMenuSceneName);
    }
}
