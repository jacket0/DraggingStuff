using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EndlessUiController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    [SerializeField] private LevelHudView _hudView;
    [SerializeField] private PauseWindowView _pauseWindowView;
    [SerializeField] private EndlessResultView _resultView;
    [SerializeField] private EndlessContinueOfferView _continueOfferView;
    [SerializeField] private EndlessContinueAdService _continueAdService;
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
        _continueOfferView.Hide();
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
        _session.ContinueOffered += HandleContinueOffered;
        _continueOfferView.WatchAdRequested += HandleWatchAdRequested;
        _continueOfferView.DeclineRequested += HandleContinueDeclined;
        _continueOfferView.CountdownFinished += HandleContinueDeclined;
        _continueAdService.AdFinished += HandleContinueAdFinished;
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
        _session.ContinueOffered -= HandleContinueOffered;
        _continueOfferView.WatchAdRequested -= HandleWatchAdRequested;
        _continueOfferView.DeclineRequested -= HandleContinueDeclined;
        _continueOfferView.CountdownFinished -= HandleContinueDeclined;
        _continueAdService.AdFinished -= HandleContinueAdFinished;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void TogglePause()
    {
        if (_isMenuTransitionPending)
            return;

        if (_session.State == LevelState.Paused)
            HandleResumeRequested();
        else
            HandlePauseRequested();
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

        if (_continueAdService.HasShownAd)
            OpenMainMenu();
        else
            InterstitialAdService.Show(OpenMainMenu);
    }

    private void HandleContinueOffered()
    {
        _continueOfferView.Show();
    }

    private void HandleWatchAdRequested()
    {
        _continueOfferView.PauseCountdown();

        if (!_continueAdService.TryShow())
            _continueOfferView.RestartCountdown();
    }

    private void HandleContinueAdFinished(bool isRewarded)
    {
        if (isRewarded)
            _continueOfferView.Hide();
        else if (_session.IsContinueOffered)
            _continueOfferView.RestartCountdown();
    }

    private void HandleContinueDeclined()
    {
        _continueOfferView.Hide();
        _session.DeclineContinue();
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
        _continueOfferView.Hide();
        _resultView.Show(score, bestScore);
    }
}
