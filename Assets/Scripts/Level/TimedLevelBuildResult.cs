using System;

public sealed class TimedLevelBuildResult
{
    public int Seed { get; }
    public int VariantIndex { get; }
    public string LayoutHash { get; }

    public TimedLevelBuildResult(int seed, int variantIndex, string layoutHash)
    {
        if (variantIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(variantIndex));

        Seed = seed;
        VariantIndex = variantIndex;
        LayoutHash = string.IsNullOrWhiteSpace(layoutHash)
            ? throw new ArgumentException("A layout hash is required.", nameof(layoutHash))
            : layoutHash;
    }
}
