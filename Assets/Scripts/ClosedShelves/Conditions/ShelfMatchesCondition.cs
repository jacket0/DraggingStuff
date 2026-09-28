using System;
using System.Collections.Generic;

public sealed class ShelfMatchesCondition : ShelfUnlockCondition
{
    private const int StarsPerMatch = 3;

    private readonly ConditionSlot[] _slots;

    public int TargetShelfIndex { get; }

    public ShelfMatchesCondition(int closedShelfIndex, int targetShelfIndex, int requiredCount) : base(closedShelfIndex)
    {
        TargetShelfIndex = targetShelfIndex;
        _slots = new[] { new ConditionSlot(ConditionSlotKind.Shelf, null, targetShelfIndex, ConditionUnit.Matches, requiredCount) };
    }

    public override IReadOnlyList<ConditionSlot> Slots => _slots;

    public override IReadOnlyList<ConditionCredit> RegisterMatch(MatchInfo match)
    {
        if (match.ShelfIndex != TargetShelfIndex || _slots[0].IsCompleted)
            return Array.Empty<ConditionCredit>();

        int applied = _slots[0].Add(1);

        if (applied <= 0)
            return Array.Empty<ConditionCredit>();

        return new[] { new ConditionCredit(0, applied, StarsPerMatch) };
    }
}
