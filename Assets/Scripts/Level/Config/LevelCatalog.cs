using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Game/Level/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    [SerializeField] private List<LevelChapter> _chapters = new List<LevelChapter>();

    public IReadOnlyList<LevelChapter> Chapters => _chapters;
    public IReadOnlyList<LevelEntry> Levels => CollectLevels();

    public int IndexOf(LevelEntry level)
    {
        IReadOnlyList<LevelEntry> levels = Levels;

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] == level)
                return i;
        }

        return -1;
    }

    public bool TryGetNext(LevelEntry currentLevel, out LevelEntry nextLevel)
    {
        if (currentLevel == null)
            throw new ArgumentNullException(nameof(currentLevel));

        IReadOnlyList<LevelEntry> levels = Levels;
        int nextIndex = IndexOf(currentLevel) + 1;

        if (nextIndex <= 0 || nextIndex >= levels.Count)
        {
            nextLevel = null;
            return false;
        }

        nextLevel = levels[nextIndex];
        return nextLevel != null;
    }

    private List<LevelEntry> CollectLevels()
    {
        List<LevelEntry> levels = new List<LevelEntry>();

        foreach (LevelChapter chapter in _chapters)
        {
            if (chapter == null)
                throw new InvalidOperationException($"{name}: a chapter is missing.");

            levels.AddRange(chapter.Levels);
        }

        return levels;
    }
}
