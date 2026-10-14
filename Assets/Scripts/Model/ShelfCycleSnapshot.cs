using System;

public sealed class ShelfCycleSnapshot
{
    public int OpenMoves { get; }
    public int ClosedMoves { get; }
    public int MovesToSwitch { get; }

    public ShelfCycleSnapshot(int openMoves, int closedMoves, int movesToSwitch)
    {
        if (openMoves < 1)
            throw new ArgumentOutOfRangeException(nameof(openMoves));

        if (closedMoves < 1)
            throw new ArgumentOutOfRangeException(nameof(closedMoves));

        if (movesToSwitch < 1 || movesToSwitch > Math.Max(openMoves, closedMoves))
            throw new ArgumentOutOfRangeException(nameof(movesToSwitch));

        OpenMoves = openMoves;
        ClosedMoves = closedMoves;
        MovesToSwitch = movesToSwitch;
    }

    public int GetDuration(bool isOpen) => isOpen ? OpenMoves : ClosedMoves;

    public ShelfCycleSnapshot Advance(bool isOpen, out bool switched) => Advance(isOpen, 1, out switched);

    public ShelfCycleSnapshot Advance(bool isOpen, int moves, out bool switched)
    {
        if (moves < 1 || moves > MovesToSwitch)
            throw new ArgumentOutOfRangeException(nameof(moves));

        switched = moves == MovesToSwitch;
        int movesToSwitch = switched ? GetDuration(!isOpen) : MovesToSwitch - moves;
        return new ShelfCycleSnapshot(OpenMoves, ClosedMoves, movesToSwitch);
    }
}
