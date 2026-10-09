using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BoardMoveSimulation
{
    public BoardStateSnapshot State { get; }
    public IReadOnlyList<MatchInfo> Matches { get; }
    public int MatchCount => Matches.Count;
    public IReadOnlyList<int> AffectedShelfIndexes { get; }
    public bool IsSwap { get; }
    public bool ConveyorsShifted { get; }
    public int MatchCountAfterShift { get; }
    public IReadOnlyList<int> OpenedShelfIndexes { get; }
    public int OpenedShelfCount => OpenedShelfIndexes.Count;
    public int EmptyColumnCount => State.EmptyColumnCount;
    public bool IsAllowed => IsSwap || MatchCount > 0 || EmptyColumnCount > 0;

    internal BoardMoveSimulation(
        BoardStateSnapshot state,
        IReadOnlyList<MatchInfo> matches,
        IReadOnlyList<int> affectedShelfIndexes,
        IReadOnlyList<int> openedShelfIndexes,
        bool isSwap = false,
        bool conveyorsShifted = false,
        int matchCountAfterShift = 0)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Matches = new List<MatchInfo>(matches).AsReadOnly();
        IsSwap = isSwap;
        ConveyorsShifted = conveyorsShifted;
        MatchCountAfterShift = matchCountAfterShift;
        AffectedShelfIndexes = Array.AsReadOnly(affectedShelfIndexes.ToArray());
        OpenedShelfIndexes = Array.AsReadOnly(openedShelfIndexes.ToArray());
    }
}
