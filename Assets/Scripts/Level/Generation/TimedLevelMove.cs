public readonly struct TimedLevelMove
{
    public ColumnPosition Source { get; }
    public ColumnPosition Target { get; }

    public TimedLevelMove(ColumnPosition source, ColumnPosition target)
    {
        Source = source;
        Target = target;
    }
}
