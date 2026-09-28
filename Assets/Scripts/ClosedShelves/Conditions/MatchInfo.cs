public readonly struct MatchInfo
{
    public int ShelfIndex { get; }
    public ItemType Type { get; }
    public int ItemCount { get; }

    public MatchInfo(int shelfIndex, ItemType type, int itemCount)
    {
        ShelfIndex = shelfIndex;
        Type = type;
        ItemCount = itemCount;
    }
}
