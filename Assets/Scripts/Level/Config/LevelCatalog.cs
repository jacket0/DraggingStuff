using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Game/Level/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    [SerializeField] private List<LevelEntry> _levels;

    public IReadOnlyList<LevelEntry> Levels => _levels;
}
