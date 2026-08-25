using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "BonusCatalog", menuName = "Game/Bonuses/Bonus Catalog")]
public class BonusCatalog : ScriptableObject
{
    [SerializeField] private List<BonusDefinition> _definitions = new List<BonusDefinition>();

    public IReadOnlyList<BonusDefinition> Definitions => _definitions;

    public bool TryGetDefinition(string id, out BonusDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null;
            return false;
        }

        foreach (var current in _definitions)
        {
            if (current == null)
                continue;

            if (string.Equals(current.Id, id, StringComparison.Ordinal))
            {
                definition = current;
                return true;
            }
        }

        definition = null;
        return false;
    }
}


