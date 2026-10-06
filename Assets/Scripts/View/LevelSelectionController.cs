using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelectionController : MonoBehaviour
{
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField] private LevelSelectionState _selectionState;
    [SerializeField] private LevelProgressService _progress;
    [SerializeField] private EndlessProgressService _endlessProgress;
    [SerializeField] private string _endlessSceneName = "EndlessLevel";
    [SerializeField] private List<LevelChapterSectionView> _sections = new List<LevelChapterSectionView>();
    [SerializeField] private EndlessModeCardView _endlessCard;
    [SerializeField] private ScrollRect _levelsScroll;

    private void Start()
    {
        ValidateDependencies();

        foreach (LevelChapterSectionView section in _sections)
        {
            foreach (LevelCardView card in section.Cards)
                card.Clicked += OpenLevel;
        }

        _endlessCard.Clicked += OpenEndlessLevel;
        RefreshCards();
        ScrollToCurrentChapter();
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
        foreach (LevelChapterSectionView section in _sections)
        {
            if (section == null)
                continue;

            foreach (LevelCardView card in section.Cards)
            {
                if (card != null)
                    card.Clicked -= OpenLevel;
            }
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
        LevelEntry currentLevel = FindCurrentLevel();
        int previousChapterLastLevelNumber = 0;

        for (int chapterIndex = 0; chapterIndex < _catalog.Chapters.Count; chapterIndex++)
        {
            LevelChapter chapter = _catalog.Chapters[chapterIndex];
            LevelChapterSectionView section = _sections[chapterIndex];
            int completedLevelCount = 0;
            int stars = 0;

            for (int levelIndex = 0; levelIndex < chapter.Levels.Count; levelIndex++)
            {
                LevelEntry level = chapter.Levels[levelIndex];
                int levelStars = _progress.GetStars(level);

                if (_progress.IsCompleted(level))
                    completedLevelCount++;

                stars += levelStars;
                section.Cards[levelIndex].Bind(new LevelCardState(
                    level,
                    _progress.IsUnlocked(level),
                    level == currentLevel,
                    levelIndex == 0 && chapter.Mechanic != LevelMechanic.Basics,
                    _progress.GetBestScore(level),
                    _progress.GetBestTime(level),
                    levelStars,
                    chapter.Mechanic != LevelMechanic.Basics ? chapter.MechanicIcon : null,
                    level.Definition.GetMechanicElementCount(chapter.Mechanic)));
            }

            section.Bind(new LevelChapterHeaderState(
                chapter,
                chapterIndex + 1,
                _progress.IsUnlocked(chapter.Levels[0]),
                previousChapterLastLevelNumber,
                completedLevelCount,
                stars));
            previousChapterLastLevelNumber = chapter.Levels[chapter.Levels.Count - 1].Number;
        }

        RefreshEndlessCard();
    }

    private LevelEntry FindCurrentLevel()
    {
        foreach (LevelEntry level in _catalog.Levels)
        {
            if (_progress.IsUnlocked(level) && !_progress.IsCompleted(level))
                return level;
        }

        return null;
    }

    private void ScrollToCurrentChapter()
    {
        LevelEntry currentLevel = FindCurrentLevel();

        if (currentLevel == null)
            return;

        int chapterIndex = 0;

        while (!ContainsLevel(_catalog.Chapters[chapterIndex], currentLevel))
            chapterIndex++;

        Canvas.ForceUpdateCanvases();
        RectTransform content = _levelsScroll.content;
        float scrollableHeight = content.rect.height - _levelsScroll.viewport.rect.height;

        if (scrollableHeight <= 0f)
            return;

        Bounds sectionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, _sections[chapterIndex].RectTransform);
        float sectionOffset = content.rect.yMax - sectionBounds.max.y;
        _levelsScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(sectionOffset / scrollableHeight);
    }

    private static bool ContainsLevel(LevelChapter chapter, LevelEntry level)
    {
        foreach (LevelEntry entry in chapter.Levels)
        {
            if (entry == level)
                return true;
        }

        return false;
    }

    private void HandleEndlessBestScoreChanged(long bestScore)
    {
        RefreshEndlessCard();
    }

    private void RefreshEndlessCard()
    {
        _endlessCard.Bind(_progress.IsEndlessUnlocked(), _endlessProgress.BestScore);
    }

    private void OpenEndlessLevel()
    {
        if (!_progress.IsEndlessUnlocked())
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

        if (_sections == null || _sections.Count != _catalog.Chapters.Count || _sections.Exists(section => section == null))
            throw new InvalidOperationException(nameof(_sections));

        for (int i = 0; i < _sections.Count; i++)
        {
            LevelChapterSectionView section = _sections[i];

            if (section.Header == null || section.Cards.Count != _catalog.Chapters[i].Levels.Count)
                throw new InvalidOperationException($"{section.name}: the section does not match chapter {i + 1}.");
        }

        if (_endlessCard == null)
            throw new InvalidOperationException(nameof(_endlessCard));

        if (_levelsScroll == null)
            throw new InvalidOperationException(nameof(_levelsScroll));
    }
}
