
using System;
using System.Collections.Generic;

public class MultiplierModifierCollection
{
    private const float DefaultMultiplier = 1.0f;

    private readonly Dictionary<object, float> _multipliers = new Dictionary<object, float>();

    public float CombinedMultiplier { get; private set; } = DefaultMultiplier;

    public IDisposable AddMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));

        object modifierKey = new object();

        _multipliers.Add(modifierKey, multiplier);
        RecalculateCombinedMultiplier();

        return new ModifierOwner(() => RemoveMultiplier(modifierKey));
    }

    private void RemoveMultiplier(object modifierKey)
    {
        if (!_multipliers.Remove(modifierKey))
            return;

        RecalculateCombinedMultiplier();
    }

    private void RecalculateCombinedMultiplier()
    {
        float combinedMultiplier = DefaultMultiplier;

        foreach (var multiplier in _multipliers.Values)
            combinedMultiplier *= multiplier;

        CombinedMultiplier = combinedMultiplier;
    }
}