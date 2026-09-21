using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class LevelUiController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string EndlessLevelSceneName = "EndlessLevel";
    private const int ReviewLevelNumber = 3;

    [SerializeField] private PauseWindowView _pauseWindowView;
    [SerializeField] private LevelHudView _levelHudView;
    [FormerlySerializedAs("_levelCompletionView")]
    [SerializeField] private LevelResultView _levelResultView;
    [SerializeField] private LevelSession _levelSession;
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
        _levelResultView.Hide();
    }

    private void OnEnable()
    {
        _levelHudView.PauseRequested += HandlePauseRequested;
        _pauseWindowView.ResumeRequested += HandleResumeRequested;
        _pauseWindowView.RestartRequested += HandleRestartRequested;
        _levelResultView.RestartRequested += HandleRestartRequested;
        _levelSession.RunEnded += HandleRunEnded;
        _levelResultView.MenuRequested += HandleMenuRequest;
        _levelResultView.NextRequested += HandleNextLevelRequest;
        _pauseWindowView.MenuRequested += HandleMenuRequest;
        _levelResultView.ReviewRequested += HandleReviewRequested;
    }

    private void OnDisable()
    {
        _levelHudView.PauseRequested -= HandlePauseRequested;
        _pauseWindowView.ResumeRequested -= HandleResumeRequested;
        _pauseWindowView.RestartRequested -= HandleRestartRequested;
        _levelResultView.RestartRequested -= HandleRestartRequested;
        _levelSession.RunEnded -= HandleRunEnded;
        _levelResultView.MenuRequested -= HandleMenuRequest;
        _levelResultView.NextRequested -= HandleNextLevelRequest;
        _pauseWindowView.MenuRequested -= HandleMenuRequest;
        _levelResultView.ReviewRequested -= HandleReviewRequested;
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

    private void HandleRunEnded(LevelRunResult result)
    {
        bool isReviewAvailable =
            result.Won &&
            _levelSession.CurrentLevel.Number == ReviewLevelNumber &&
            GameReviewService.CanRequest;
        bool opensEndlessMode = !_levelCatalog.TryGetNext(_levelSession.CurrentLevel, out _);
        long previousBestScore = GameProgressRepository.GetLevelBestScore(result.LevelNumber);
        long bestTime = SelectBestTime(
            GameProgressRepository.GetLevelBestTime(result.LevelNumber),
            result.Won ? result.ActiveTimeMilliseconds : 0);
        long bestScore = Math.Max(previousBestScore, result.Won ? result.Score : 0);
        bool isNewRecord = result.Won && result.Score > previousBestScore;

        _levelHudView.Hide();
        _pauseWindowView.Hide();
        _levelResultView.Show(result, bestTime, bestScore, isReviewAvailable, opensEndlessMode, isNewRecord);
    }

    private void HandleMenuRequest()
    {
        if (_isTransitionPending)
            return;

        _isTransitionPending = true;
        _levelResultView.SetInteractable(false);
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
            _levelResultView.HideReviewButton();
    }

    private void HandleNextLevelRequest()
    {
        if (_isTransitionPending || _levelSession.State != LevelState.Won)
            return;

        _isTransitionPending = true;
        _levelResultView.SetInteractable(false);
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

    private static long SelectBestTime(long currentBestTime, long resultTime)
    {
        if (currentBestTime <= 0)
            return Math.Max(0, resultTime);

        if (resultTime <= 0)
            return currentBestTime;

        return Math.Min(currentBestTime, resultTime);
    }
}
