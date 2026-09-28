using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ClosedShelfProgress
{
    private sealed class Entry
    {
        public ShelfUnlockCondition Condition;
        public ClosedShelfState State;
    }

    private readonly Dictionary<int, Entry> _entries;

    public ClosedShelfProgress(IReadOnlyList<ClosedShelfDefinition> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        _entries = new Dictionary<int, Entry>(definitions.Count);

        foreach (ClosedShelfDefinition definition in definitions)
        {
            _entries.Add(definition.ShelfIndex, new Entry
            {
                Condition = ShelfUnlockCondition.Create(definition),
                State = ClosedShelfState.Closed
            });
        }
    }

    public bool IsRefillActive => _entries.Values.Any(entry => !entry.Condition.IsCompleted);

    public IReadOnlyDictionary<ItemType, int> PriorityTypes
    {
        get
        {
            Dictionary<ItemType, int> totals = new Dictionary<ItemType, int>();

            foreach (Entry entry in _entries.Values.Where(entry => !entry.Condition.IsCompleted))
            {
                foreach (KeyValuePair<ItemType, int> pair in entry.Condition.RemainingByType)
                    totals[pair.Key] = totals.TryGetValue(pair.Key, out int existing) ? existing + pair.Value : pair.Value;
            }

            return totals;
        }
    }

    public IReadOnlyCollection<int> ClosedShelfIndexes => _entries.Keys;
    public ClosedShelfState GetState(int closedShelfIndex) => _entries[closedShelfIndex].State;
    public ShelfUnlockCondition GetCondition(int closedShelfIndex) => _entries[closedShelfIndex].Condition;

    public IReadOnlyList<ShelfProgressCredit> RegisterMatch(MatchInfo match)
    {
        List<ShelfProgressCredit> credits = new List<ShelfProgressCredit>();

        foreach (KeyValuePair<int, Entry> pair in _entries)
        {
            Entry entry = pair.Value;

            if (entry.State != ClosedShelfState.Closed || entry.Condition.IsCompleted)
                continue;

            IReadOnlyList<ConditionCredit> conditionCredits = entry.Condition.RegisterMatch(match);

            if (conditionCredits.Count > 0)
                credits.Add(new ShelfProgressCredit(pair.Key, conditionCredits));

            if (entry.Condition.IsCompleted)
                entry.State = ClosedShelfState.Revealing;
        }

        return credits;
    }

    public void MarkOpen(int closedShelfIndex) => _entries[closedShelfIndex].State = ClosedShelfState.Open;
}
