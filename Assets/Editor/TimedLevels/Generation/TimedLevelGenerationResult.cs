using System;
using System.Collections.Generic;

public sealed class TimedLevelGenerationResult
{
    public BoardStateSnapshot State { get; }
    public IReadOnlyList<TimedLevelMove> SolutionMoves { get; }

    public TimedLevelGenerationResult(BoardStateSnapshot state, IReadOnlyList<TimedLevelMove> solutionMoves)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));

        if (solutionMoves == null)
            throw new ArgumentNullException(nameof(solutionMoves));

        TimedLevelMove[] copy = new TimedLevelMove[solutionMoves.Count];

        for (int index = 0; index < copy.Length; index++)
            copy[index] = solutionMoves[index];

        SolutionMoves = Array.AsReadOnly(copy);
    }
}
