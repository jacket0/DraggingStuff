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
    public IReadOnlyList<int> SwitchedShelfIndexes { get; }
    public int MatchCountAfterSwitch { get; }
    public IReadOnlyList<int> OpenedShelfIndexes { get; }
    public int OpenedShelfCount => OpenedShelfIndexes.Count;
    public IReadOnlyList<ColumnPosition> CleanedColumns { get; }
    public IReadOnlyList<ColumnPosition> GrownColumns { get; }
    public int EmptyColumnCount => State.EmptyColumnCount;
    public bool IsAllowed => IsSwap || MatchCount > 0 || EmptyColumnCount > 0;

    internal BoardMoveSimulation(
        BoardStateSnapshot state,
        IReadOnlyList<MatchInfo> matches,
        IReadOnlyList<int> affectedShelfIndexes,
        IReadOnlyList<int> openedShelfIndexes,
        bool isSwap = false,
        bool conveyorsShifted = false,
        int matchCountAfterShift = 0,
        IReadOnlyList<int> switchedShelfIndexes = null,
        int matchCountAfterSwitch = 0,
        IReadOnlyList<ColumnPosition> cleanedColumns = null,
        IReadOnlyList<ColumnPosition> grownColumns = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Matches = new List<MatchInfo>(matches).AsReadOnly();
        IsSwap = isSwap;
        ConveyorsShifted = conveyorsShifted;
        MatchCountAfterShift = matchCountAfterShift;
        AffectedShelfIndexes = Array.AsReadOnly(affectedShelfIndexes.ToArray());
        OpenedShelfIndexes = Array.AsReadOnly(openedShelfIndexes.ToArray());
        SwitchedShelfIndexes = Array.AsReadOnly(switchedShelfIndexes?.ToArray() ?? Array.Empty<int>());
        MatchCountAfterSwitch = matchCountAfterSwitch;
        CleanedColumns = Array.AsReadOnly(cleanedColumns?.ToArray() ?? Array.Empty<ColumnPosition>());
        GrownColumns = Array.AsReadOnly(grownColumns?.ToArray() ?? Array.Empty<ColumnPosition>());
    }
}
