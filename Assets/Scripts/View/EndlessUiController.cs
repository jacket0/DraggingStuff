using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EndlessUiController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    [SerializeField] private LevelHudView _hudView;
    [SerializeField] private PauseWindowView _pauseWindowView;
    [SerializeField] private EndlessResultView _resultView;
    [SerializeField] private EndlessSession _session;
    [SerializeField] private EndlessResultRecorder _resultRecorder;
    [SerializeField] private YandexInterstitialAdService _interstitialAdService;

    private bool _isMenuTransitionPending;

    private IInterstitialAdService InterstitialAdService => _interstitialAdService;

    private void Start()
    {
        _hudView.Show();
        _pauseWindowView.Hide();
        _resultView.Hide();
    }

    private void OnEnable()
    {
        _hudView.PauseRequested += HandlePauseRequested;
        _pauseWindowView.ResumeRequested += HandleResumeRequested;
        _pauseWindowView.RestartRequested += HandleRestartRequested;
        _pauseWindowView.MenuRequested += HandleMenuRequested;
        _resultView.RestartRequested += HandleRestartRequested;
        _resultView.MenuRequested += HandleMenuRequested;
        _resultRecorder.ResultRecorded += HandleResultRecorded;
    }

    private void OnDisable()
    {
        _hudView.PauseRequested -= HandlePauseRequested;
        _pauseWindowView.ResumeRequested -= HandleResumeRequested;
        _pauseWindowView.RestartRequested -= HandleRestartRequested;
        _pauseWindowView.MenuRequested -= HandleMenuRequested;
        _resultView.RestartRequested -= HandleRestartRequested;
        _resultView.MenuRequested -= HandleMenuRequested;
        _resultRecorder.ResultRecorded -= HandleResultRecorded;
    }

    private void HandlePauseRequested()
    {
        if (_session.TryPause())
            _pauseWindowView.Show();
    }

    private void HandleResumeRequested()
    {
        _session.Resume();
        _pauseWindowView.Hide();
    }

    private void HandleRestartRequested()
    {
        _session.Restart();
    }

    private void HandleMenuRequested()
    {
        if (_isMenuTransitionPending)
            return;

        _isMenuTransitionPending = true;
        InterstitialAdService.Show(OpenMainMenu);
    }

    private static void OpenMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void HandleResultRecorded(long score, long bestScore)
    {
        _hudView.Hide();
        _pauseWindowView.Hide();
        _resultView.Show(score, bestScore);
    }
}
