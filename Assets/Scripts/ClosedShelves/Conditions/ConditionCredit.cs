public readonly struct ConditionCredit
{
    public int SlotIndex { get; }
    public int Amount { get; }
    public int StarCount { get; }

    public ConditionCredit(int slotIndex, int amount, int starCount)
    {
        SlotIndex = slotIndex;
        Amount = amount;
        StarCount = starCount;
    }
}
