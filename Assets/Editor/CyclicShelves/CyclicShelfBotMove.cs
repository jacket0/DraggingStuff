public sealed class CyclicShelfBotMove
{
    public ColumnPosition Source { get; }
    public ColumnPosition Target { get; }
    public bool IsMatch { get; }
    public bool IsSetup { get; }
    public bool IsEvacuation { get; }

    public CyclicShelfBotMove(ColumnPosition source, ColumnPosition target, bool isMatch, bool isSetup, bool isEvacuation)
    {
        Source = source;
        Target = target;
        IsMatch = isMatch;
        IsSetup = isSetup;
        IsEvacuation = isEvacuation;
    }
}
