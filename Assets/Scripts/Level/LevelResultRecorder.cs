using UnityEngine;

public class LevelResultRecorder : MonoBehaviour
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private LevelProgressService _progress;

    private void OnEnable()
    {
        _levelSession.RunEnded += RecordResult;
    }

    private void OnDisable()
    {
        _levelSession.RunEnded -= RecordResult;
    }

    private void RecordResult(LevelRunResult result)
    {
        _progress.RegisterLevelResult(result);
    }
}
