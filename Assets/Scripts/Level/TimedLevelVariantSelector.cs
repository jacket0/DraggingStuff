using System;
using System.Collections.Generic;
using UnityEngine;

public static class TimedLevelVariantSelector
{
    private static readonly Dictionary<int, int> PreviousIndexes = new Dictionary<int, int>();

    public static int Select(int levelNumber, int variantCount)
    {
        if (levelNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelNumber));

        if (variantCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(variantCount));

        int index = UnityEngine.Random.Range(0, variantCount);

        if (variantCount > 1 && PreviousIndexes.TryGetValue(levelNumber, out int previousIndex) && index == previousIndex)
            index = (index + UnityEngine.Random.Range(1, variantCount)) % variantCount;

        PreviousIndexes[levelNumber] = index;
        return index;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        PreviousIndexes.Clear();
    }
}
