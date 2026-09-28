using System;
using System.Collections.Generic;

public sealed class BoardMoveSimulation
{
    public BoardStateSnapshot State { get; }
    public IReadOnlyList<MatchInfo> Matches { get; }
    public int MatchCount => Matches.Count;
    public IReadOnlyList<int> AffectedShelfIndexes { get; }
    public bool IsSwap { get; }
    public int EmptyColumnCount => State.EmptyColumnCount;
    public bool IsAllowed => IsSwap || MatchCount > 0 || EmptyColumnCount > 0;

    internal BoardMoveSimulation(BoardStateSnapshot state, IReadOnlyList<MatchInfo> matches, IReadOnlyList<int> affectedShelfIndexes, bool isSwap = false)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Matches = new List<MatchInfo>(matches).AsReadOnly();
        IsSwap = isSwap;
        int[] copy = new int[affectedShelfIndexes.Count];

        for (int index = 0; index < copy.Length; index++)
            copy[index] = affectedShelfIndexes[index];

        AffectedShelfIndexes = Array.AsReadOnly(copy);
    }
}
