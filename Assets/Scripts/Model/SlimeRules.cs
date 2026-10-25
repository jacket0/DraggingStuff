using System;

public sealed class SlimeRules
{
    public static SlimeRules Default { get; } = new SlimeRules(1, true, 0);

    public int ColumnsPerGrowth { get; }
    public bool CleansNeighborShelves { get; }
    public int GraceMoves { get; }

    public SlimeRules(int columnsPerGrowth, bool cleansNeighborShelves, int graceMoves)
    {
        if (columnsPerGrowth < 1)
            throw new ArgumentOutOfRangeException(nameof(columnsPerGrowth));

        if (graceMoves < 0)
            throw new ArgumentOutOfRangeException(nameof(graceMoves));

        ColumnsPerGrowth = columnsPerGrowth;
        CleansNeighborShelves = cleansNeighborShelves;
        GraceMoves = graceMoves;
    }
}
