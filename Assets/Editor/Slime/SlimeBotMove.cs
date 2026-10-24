public sealed class SlimeBotMove
{
    public ColumnPosition Source { get; }
    public ColumnPosition Target { get; }
    public bool IsMatch { get; }
    public bool IsCleaning { get; }
    public bool IsSetup { get; }
    public bool IsEvacuation { get; }
    public bool SticksItem { get; }

    public SlimeBotMove(ColumnPosition source, ColumnPosition target, bool isMatch, bool isCleaning, bool isSetup, bool isEvacuation, bool sticksItem)
    {
        Source = source;
        Target = target;
        IsMatch = isMatch;
        IsCleaning = isCleaning;
        IsSetup = isSetup;
        IsEvacuation = isEvacuation;
        SticksItem = sticksItem;
    }
}
