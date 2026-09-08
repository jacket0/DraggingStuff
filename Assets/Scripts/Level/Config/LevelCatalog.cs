using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Game/Level/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    [SerializeField] private List<LevelEntry> _levels;

    public IReadOnlyList<LevelEntry> Levels => _levels;

    public bool TryGetNext(LevelEntry currentLevel, out LevelEntry nextLevel)
    {
        if (currentLevel == null)
            throw new ArgumentNullException(nameof(currentLevel));

        int currentIndex = _levels.IndexOf(currentLevel);
        int nextIndex = currentIndex + 1;

        if (currentIndex < 0 || nextIndex >= _levels.Count)
        {
            nextLevel = null;
            return false;
        }

        nextLevel = _levels[nextIndex];
        return nextLevel != null;
    }
}
