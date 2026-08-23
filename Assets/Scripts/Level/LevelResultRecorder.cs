using UnityEngine;

public class LevelResultRecorder : MonoBehaviour
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private ScoreSystem _scoreSystem;
    [SerializeField] private LevelProgressService _progress;

    private void OnEnable()
    {
        _levelSession.LevelCompleted += RecordResult;
    }

    private void OnDisable()
    {
        _levelSession.LevelCompleted -= RecordResult;
    }

    private void RecordResult()
    {
        _progress.RegisterCompletion(_levelSession.CurrentLevel, _scoreSystem.CurrentScore);
    }
}
