using System;

public sealed class ConditionSlot
{
    public ConditionSlotKind Kind { get; }
    public ItemType? ItemType { get; }
    public int? TargetShelfIndex { get; }
    public ConditionUnit Unit { get; }
    public int Required { get; }
    public int Current { get; private set; }
    public bool IsCompleted => Current >= Required;

    public ConditionSlot(ConditionSlotKind kind, ItemType? itemType, int? targetShelfIndex, ConditionUnit unit, int required)
    {
        if (required <= 0)
            throw new ArgumentOutOfRangeException(nameof(required));

        if (kind == ConditionSlotKind.Item && !itemType.HasValue)
            throw new ArgumentException("An item slot requires an item type.", nameof(itemType));

        Kind = kind;
        ItemType = itemType;
        TargetShelfIndex = targetShelfIndex;
        Unit = unit;
        Required = required;
    }

    internal int Add(int amount)
    {
        int applied = Math.Max(0, Math.Min(amount, Required - Current));
        Current += applied;
        return applied;
    }
}
