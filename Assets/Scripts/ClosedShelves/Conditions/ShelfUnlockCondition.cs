using System;
using System.Collections.Generic;
using System.Linq;

public abstract class ShelfUnlockCondition
{
    private static readonly IReadOnlyDictionary<ItemType, int> EmptyRemaining = new Dictionary<ItemType, int>();

    public int ClosedShelfIndex { get; }
    public abstract IReadOnlyList<ConditionSlot> Slots { get; }
    public bool IsCompleted => Slots.All(slot => slot.IsCompleted);
    public virtual IReadOnlyDictionary<ItemType, int> RemainingByType => EmptyRemaining;

    public abstract IReadOnlyList<ConditionCredit> RegisterMatch(MatchInfo match);

    protected ShelfUnlockCondition(int closedShelfIndex)
    {
        ClosedShelfIndex = closedShelfIndex;
    }

    public static ShelfUnlockCondition Create(ClosedShelfDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        return definition.Condition switch
        {
            ShelfUnlockConditionKind.CollectItems => new CollectItemsCondition(definition.ShelfIndex, definition.RequiredItems),
            ShelfUnlockConditionKind.MatchesOnShelf => new ShelfMatchesCondition(definition.ShelfIndex, definition.TargetShelfIndex, definition.RequiredCount),
            _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.Condition, "Unknown unlock condition kind.")
        };
    }
}
