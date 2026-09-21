using System;
using System.Collections.Generic;

public sealed class TimedLevelGenerationInput
{
    public string Name { get; }
    public int EmptyColumnCount { get; }
    public IReadOnlyList<TimedLevelGroupCount> Groups { get; }

    public TimedLevelGenerationInput(string name, int emptyColumnCount, IReadOnlyList<TimedLevelGroupCount> groups)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException(nameof(name));
        EmptyColumnCount = emptyColumnCount > 0 ? emptyColumnCount : throw new ArgumentOutOfRangeException(nameof(emptyColumnCount));
        Groups = groups ?? throw new ArgumentNullException(nameof(groups));
    }
}

public readonly struct TimedLevelGroupCount
{
    public ItemType Type { get; }
    public int Count { get; }

    public TimedLevelGroupCount(ItemType type, int count)
    {
        Type = type;
        Count = count > 0 ? count : throw new ArgumentOutOfRangeException(nameof(count));
    }
}
