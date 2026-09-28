using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CollectItemsCondition : ShelfUnlockCondition
{
    private readonly ConditionSlot[] _slots;
    private readonly Dictionary<ItemType, ConditionSlot> _slotsByType;

    public CollectItemsCondition(int closedShelfIndex, IReadOnlyList<ItemRequirement> requirements) : base(closedShelfIndex)
    {
        if (requirements == null || requirements.Count == 0 || requirements.Count > 3)
            throw new ArgumentException("CollectItems requires between 1 and 3 item requirements.", nameof(requirements));

        _slots = new ConditionSlot[requirements.Count];
        _slotsByType = new Dictionary<ItemType, ConditionSlot>(requirements.Count);

        for (int index = 0; index < requirements.Count; index++)
        {
            ItemRequirement requirement = requirements[index];

            if (_slotsByType.ContainsKey(requirement.Type))
                throw new ArgumentException("CollectItems requirements must have distinct item types.", nameof(requirements));

            ConditionSlot slot = new ConditionSlot(ConditionSlotKind.Item, requirement.Type, null, ConditionUnit.Items, requirement.Count);
            _slots[index] = slot;
            _slotsByType.Add(requirement.Type, slot);
        }
    }

    public override IReadOnlyList<ConditionSlot> Slots => _slots;

    public override IReadOnlyDictionary<ItemType, int> RemainingByType =>
        _slots.Where(slot => !slot.IsCompleted).ToDictionary(slot => slot.ItemType.Value, slot => slot.Required - slot.Current);

    public override IReadOnlyList<ConditionCredit> RegisterMatch(MatchInfo match)
    {
        if (!_slotsByType.TryGetValue(match.Type, out ConditionSlot slot) || slot.IsCompleted)
            return Array.Empty<ConditionCredit>();

        int applied = slot.Add(match.ItemCount);

        if (applied <= 0)
            return Array.Empty<ConditionCredit>();

        return new[] { new ConditionCredit(Array.IndexOf(_slots, slot), applied, applied) };
    }
}
