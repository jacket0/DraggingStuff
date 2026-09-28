using System.Collections.Generic;

public readonly struct ShelfProgressCredit
{
    public int ClosedShelfIndex { get; }
    public IReadOnlyList<ConditionCredit> Credits { get; }

    public ShelfProgressCredit(int closedShelfIndex, IReadOnlyList<ConditionCredit> credits)
    {
        ClosedShelfIndex = closedShelfIndex;
        Credits = credits;
    }
}
