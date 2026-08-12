using System;
using UnityEngine;

public class MatchRewardController : MonoBehaviour
{
    [SerializeField] private LevelSession _levelSession;
    [SerializeField] private ScoreSystem _scoreSystem;
    [SerializeField] private ComboSystem _comboSystem;

    private void Awake()
    {
        if (_levelSession == null)
            throw new InvalidOperationException(nameof(_levelSession));

        if (_scoreSystem == null)
            throw new InvalidOperationException(nameof(_scoreSystem));

        if (_comboSystem == null)
            throw new InvalidOperationException(nameof(_comboSystem));
    }

    private void OnEnable()
    {
        _levelSession.MatchSucceeded += HandleMatchSucceeded;
    }

    private void OnDisable()
    {
        _levelSession.MatchSucceeded -= HandleMatchSucceeded;
    }

    private void HandleMatchSucceeded(MatchResolution match)
    {
        int comboMultiplier = _comboSystem.RegisterMatch();
        _scoreSystem.RegisterMatch(match, comboMultiplier);
    }
}
