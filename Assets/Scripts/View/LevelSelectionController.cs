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

    private readonly List<LevelCardView> _cards = new List<LevelCardView>();

    private void Start()
    {
        CreateCards();
    }

    private void OnDestroy()
    {
        foreach (var card in _cards)
            card.Clicked -= OpenLevel;
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
