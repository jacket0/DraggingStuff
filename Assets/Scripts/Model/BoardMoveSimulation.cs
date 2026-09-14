using System;
using System.Collections.Generic;

public sealed class BoardMoveSimulation
{
    public BoardStateSnapshot State { get; }
    public int MatchCount { get; }
    public IReadOnlyList<int> AffectedShelfIndexes { get; }
    public int EmptyColumnCount => State.EmptyColumnCount;
    public bool IsAllowed => MatchCount > 0 || EmptyColumnCount > 0;

    internal BoardMoveSimulation(BoardStateSnapshot state, int matchCount, IReadOnlyList<int> affectedShelfIndexes)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        MatchCount = matchCount;
        int[] copy = new int[affectedShelfIndexes.Count];

        for (int index = 0; index < copy.Length; index++)
            copy[index] = affectedShelfIndexes[index];

        AffectedShelfIndexes = Array.AsReadOnly(copy);
    }
}
