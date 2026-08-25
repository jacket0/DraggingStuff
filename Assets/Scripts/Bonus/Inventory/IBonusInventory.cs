using System;

public interface IBonusInventory
{
    public event Action<BonusDefinition, int> AmountChanged;

    public int GetAmount(BonusDefinition definition);
    public bool TryConsume(BonusDefinition definition);
    public bool TryGrant(BonusDefinition definition);
}