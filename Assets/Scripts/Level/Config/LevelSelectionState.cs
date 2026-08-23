using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelSelectionState", menuName = "Game/Level/Level Selection State")]
public class LevelSelectionState : ScriptableObject
{
    private LevelEntry _selectedLevel;

    public void Select(LevelEntry level)
    {
        _selectedLevel = level ?? throw new ArgumentNullException(nameof(level));
    }

    public bool TryGetSelected(out LevelEntry level)
    {
        level = _selectedLevel;
        return level != null;
    }
}
