using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Game/Level/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    [SerializeField] private List<LevelChapter> _chapters = new List<LevelChapter>();
    [SerializeField] private List<LevelEntry> _levels;

    [NonSerialized] private List<LevelEntry> _chapterLevels;

    public IReadOnlyList<LevelChapter> Chapters => _chapters;
    public IReadOnlyList<LevelEntry> Levels => _chapters.Count > 0 ? GetChapterLevels() : _levels;

    private void OnEnable()
    {
        _chapterLevels = null;
    }

    private void OnValidate()
    {
        _chapterLevels = null;
    }

    public bool TryGetNext(LevelEntry currentLevel, out LevelEntry nextLevel)
    {
        if (currentLevel == null)
            throw new ArgumentNullException(nameof(currentLevel));

        IReadOnlyList<LevelEntry> levels = Levels;
        int nextIndex = FindIndex(levels, currentLevel) + 1;

        if (nextIndex <= 0 || nextIndex >= levels.Count)
        {
            nextLevel = null;
            return false;
        }

        nextLevel = levels[nextIndex];
        return nextLevel != null;
    }

    private List<LevelEntry> GetChapterLevels()
    {
        if (_chapterLevels != null)
            return _chapterLevels;

        _chapterLevels = new List<LevelEntry>();

        foreach (LevelChapter chapter in _chapters)
        {
            if (chapter == null)
                throw new InvalidOperationException($"{name}: a chapter is missing.");

            _chapterLevels.AddRange(chapter.Levels);
        }

        return _chapterLevels;
    }

    private static int FindIndex(IReadOnlyList<LevelEntry> levels, LevelEntry level)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] == level)
                return i;
        }

        return -1;
    }
}
