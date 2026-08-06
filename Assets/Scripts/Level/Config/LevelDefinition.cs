using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelDefinition", menuName = "Game/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [SerializeField] private List<ShelfDefinition> _shelves;

    public IReadOnlyList<ShelfDefinition> Shelves => _shelves;
}
