using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BonusCatalog", menuName = "Game/Bonuses/Bonus Catalog")]
public class BonusCatalog : ScriptableObject
{
    [SerializeField] private List<BonusDefinition> _definitions = new List<BonusDefinition>();

    public IReadOnlyList<BonusDefinition> Definitions => _definitions;

    public bool TryGetDefinition(BonusId bonusId, out BonusDefinition definition)
    {
        foreach (var current in _definitions)
        {
            if (current != null && current.Id == bonusId)
            {
                definition = current;
                return true;
            }
        }

        definition = null;
        return false;
    }
}


