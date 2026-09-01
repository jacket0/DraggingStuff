using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectionController : MonoBehaviour
{
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private LevelSelectionState _selectionState;
    [SerializeField] private LevelCardView _cardView;
    [SerializeField] private Transform _cardsRoot;
    [SerializeField] private LevelProgressService _progress;
    [SerializeField] private EndlessModeCardView _endlessCardView;
    [SerializeField] private EndlessProgressService _endlessProgress;
    [SerializeField] private string _endlessSceneName = "EndlessLevel";

    private readonly List<LevelCardView> _cards = new List<LevelCardView>();

    private EndlessModeCardView _createdEndlessCard;

    private void Start()
    {
        CreateCards();
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
        foreach (var card in _cards)
            card.Clicked -= OpenLevel;

        if (_createdEndlessCard != null)
            _createdEndlessCard.Clicked -= OpenEndlessLevel;
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

    private void CreateCards()
    {
        ValidateDependencies();

        foreach (var level in _catalog.Levels)
        {
            if (level == null)
                throw new NullReferenceException(nameof(level));

            LevelCardView card = Instantiate(_cardView, _cardsRoot);

            bool unlocked = _progress.IsUnlocked(level);
            long bestScore = _progress.GetBestScore(level);

            card.Bind(level, unlocked, bestScore);
            card.Clicked += OpenLevel;

            _cards.Add(card);
        }

        CreateEndlessCard();
    }

    private void CreateEndlessCard()
    {
        if (_endlessCardView == null && _endlessProgress == null)
            return;

        if (_endlessCardView == null || _endlessProgress == null || string.IsNullOrWhiteSpace(_endlessSceneName))
            throw new InvalidOperationException(nameof(_endlessCardView));

        _createdEndlessCard = Instantiate(_endlessCardView, _cardsRoot);
        _createdEndlessCard.Bind(_progress.AreAllLevelsCompleted(), _endlessProgress.BestScore);
        _createdEndlessCard.Clicked += OpenEndlessLevel;
    }

    private void RefreshCards()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            LevelEntry level = _catalog.Levels[i];
            _cards[i].Bind(level, _progress.IsUnlocked(level), _progress.GetBestScore(level));
        }

        RefreshEndlessCard();
    }

    private void HandleEndlessBestScoreChanged(long bestScore)
    {
        RefreshEndlessCard();
    }

    private void RefreshEndlessCard()
    {
        if (_createdEndlessCard == null)
            return;

        _createdEndlessCard.Bind(_progress.AreAllLevelsCompleted(), _endlessProgress.BestScore);
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

        if (_cardView == null)
            throw new InvalidOperationException(nameof(_cardView));

        if (_cardsRoot == null)
            throw new InvalidOperationException(nameof(_cardsRoot));

        if (_progress == null)
            throw new InvalidOperationException(nameof(_progress));
    }
}
