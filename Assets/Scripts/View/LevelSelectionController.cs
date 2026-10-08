using System;
using System.Collections.Generic;
using System.Linq;
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

    private int _scrolledChapterIndex = -1;

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
        int currentChapterIndex = -1;
        int previousChapterLastLevelNumber = 0;

        for (int chapterIndex = 0; chapterIndex < _catalog.Chapters.Count; chapterIndex++)
        {
            LevelChapter chapter = _catalog.Chapters[chapterIndex];
            LevelChapterSectionView section = _sections[chapterIndex];
            bool hasMechanic = chapter.Mechanic != LevelMechanic.Basics;
            int completedLevelCount = 0;
            int stars = 0;

            for (int levelIndex = 0; levelIndex < chapter.Levels.Count; levelIndex++)
            {
                LevelEntry level = chapter.Levels[levelIndex];
                bool isUnlocked = _progress.IsUnlocked(level);
                bool isCompleted = _progress.IsCompleted(level);
                bool isCurrent = isUnlocked && !isCompleted && currentChapterIndex < 0;
                int levelStars = _progress.GetStars(level);
                LevelChapter secondaryMechanicChapter = FindSecondaryMechanicChapter(level, chapter);

                if (isCurrent)
                    currentChapterIndex = chapterIndex;

                if (isUnlocked && isCompleted)
                {
                    completedLevelCount++;
                    stars += levelStars;
                }

                section.Cards[levelIndex].Bind(new LevelCardState(
                    level,
                    isUnlocked,
                    isCurrent,
                    hasMechanic && levelIndex == 0 && !isCompleted,
                    _progress.GetBestScore(level),
                    _progress.GetBestTime(level),
                    levelStars,
                    hasMechanic ? chapter.MechanicIcon : null,
                    level.Definition.GetMechanicElementCount(chapter.Mechanic),
                    secondaryMechanicChapter != null ? secondaryMechanicChapter.MechanicIcon : null,
                    secondaryMechanicChapter != null ? level.Definition.GetMechanicElementCount(secondaryMechanicChapter.Mechanic) : 0));
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

        if (currentChapterIndex >= 0 && currentChapterIndex != _scrolledChapterIndex)
            ScrollToChapter(currentChapterIndex);
    }

    private LevelChapter FindSecondaryMechanicChapter(LevelEntry level, LevelChapter chapter)
    {
        return _catalog.Chapters.FirstOrDefault(other =>
            other.Mechanic != chapter.Mechanic
            && other.Mechanic != LevelMechanic.Basics
            && level.Definition.GetMechanicElementCount(other.Mechanic) > 0);
    }

    private void ScrollToChapter(int chapterIndex)
    {
        _scrolledChapterIndex = chapterIndex;
        Canvas.ForceUpdateCanvases();
        RectTransform content = _levelsScroll.content;
        float scrollableHeight = content.rect.height - _levelsScroll.viewport.rect.height;

        if (scrollableHeight <= 0f)
            return;

        Bounds sectionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, _sections[chapterIndex].RectTransform);
        float sectionOffset = content.rect.yMax - sectionBounds.max.y;
        _levelsScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(sectionOffset / scrollableHeight);
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
