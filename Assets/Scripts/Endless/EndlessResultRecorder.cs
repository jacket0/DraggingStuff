using System;
using UnityEngine;

public sealed class EndlessResultRecorder : MonoBehaviour
{
    [SerializeField] private EndlessSession _session;
    [SerializeField] private ScoreSystem _scoreSystem;
    [SerializeField] private EndlessProgressService _progress;

    public event Action<long, long> ResultRecorded;

    private void OnEnable()
    {
        _session.RunEnded += RecordResult;
    }

    private void OnDisable()
    {
        if (_session != null)
            _session.RunEnded -= RecordResult;
    }

    private void RecordResult()
    {
        long score = _scoreSystem.CurrentScore;
        _progress.RegisterResult(score);
        ResultRecorded?.Invoke(score, _progress.BestScore);
    }
}
