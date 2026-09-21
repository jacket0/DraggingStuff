using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectionController : MonoBehaviour
{
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private LevelSelectionState _selectionState;
    [SerializeField] private LevelProgressService _progress;
    [SerializeField] private EndlessProgressService _endlessProgress;
    [SerializeField] private string _endlessSceneName = "EndlessLevel";
    [SerializeField] private List<LevelCardView> _cards = new List<LevelCardView>();
    [SerializeField] private EndlessModeCardView _endlessCard;

    private void Start()
    {
        ValidateDependencies();

        foreach (LevelCardView card in _cards)
        {
            card.Clicked += OpenLevel;
        }

        _endlessCard.Clicked += OpenEndlessLevel;
        RefreshCards();
    }

    private void OnEnable()
    {
        if (_progress != null)
            _progress.ProgressChanged += RefreshCards;

        if (_endlessProgress != null)
            _endlessProgress.BestScoreChanged += HandleEndlessBestScoreChanged;
    }

    private void OnDisable()
    {
        if (_progress != null)
            _progress.ProgressChanged -= RefreshCards;

        if (_endlessProgress != null)
            _endlessProgress.BestScoreChanged -= HandleEndlessBestScoreChanged;
    }

    private void OnDestroy()
    {
        foreach (LevelCardView card in _cards)
        {
            if (card != null)
                card.Clicked -= OpenLevel;
        }

        if (_endlessCard != null)
            _endlessCard.Clicked -= OpenEndlessLevel;
    }

    private void OpenLevel(LevelEntry level)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        if (!level.CanBeStarted)
            return;

        _selectionState.Select(level);
        SceneManager.LoadScene(level.SceneName);
    }

    private void RefreshCards()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            LevelEntry level = _catalog.Levels[i];
            _cards[i].Bind(
                level,
                _progress.IsUnlocked(level),
                _progress.GetBestScore(level),
                _progress.GetBestTime(level),
                _progress.GetStars(level));
        }

        RefreshEndlessCard();
    }

    private void HandleEndlessBestScoreChanged(long bestScore)
    {
        RefreshEndlessCard();
    }

    private void RefreshEndlessCard()
    {
        _endlessCard.Bind(_progress.AreAllLevelsCompleted(), _endlessProgress.BestScore);
    }

    private void OpenEndlessLevel()
    {
        if (!_progress.AreAllLevelsCompleted())
            return;

        SceneManager.LoadScene(_endlessSceneName);
    }

    private void ValidateDependencies()
    {
        if (_catalog == null)
            throw new InvalidOperationException(nameof(_catalog));

        if (_selectionState == null)
            throw new InvalidOperationException(nameof(_selectionState));

        if (_progress == null)
            throw new InvalidOperationException(nameof(_progress));

        if (_endlessProgress == null)
            throw new InvalidOperationException(nameof(_endlessProgress));

        if (_cards == null || _cards.Count != _catalog.Levels.Count || _cards.Exists(card => card == null))
            throw new InvalidOperationException(nameof(_cards));

        if (_endlessCard == null)
            throw new InvalidOperationException(nameof(_endlessCard));
    }
}
