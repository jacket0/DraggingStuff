using UnityEngine;

public class LevelUiController : MonoBehaviour
{
    [SerializeField] private PauseWindowView _pauseWindowView;
    [SerializeField] private LevelHudView _levelHudView;
    [SerializeField] private LevelCompletionView _levelCompletionView;
    [SerializeField] private LevelSession _levelSession;

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
        _levelCompletionView.RestartLevel += HandleRestartRequested;
        _levelSession.LevelCompleted += HandleLevelCompleted;
    }

    private void OnDisable()
    {
        _levelHudView.PauseRequested -= HandlePauseRequested;
        _pauseWindowView.ResumeRequested -= HandleResumeRequested;
        _pauseWindowView.RestartRequested -= HandleRestartRequested;
        _levelCompletionView.RestartLevel -= HandleRestartRequested;
        _levelSession.LevelCompleted -= HandleLevelCompleted;

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
        _levelCompletionView.Show();
    }
}
