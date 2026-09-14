using System.Collections.Generic;

public sealed class BoardColumnSnapshot
{
    private readonly ColumnStateSnapshot _state;
    public IReadOnlyList<ItemType> Items => _state.Items;

    public BoardColumnSnapshot(IReadOnlyList<ItemType> items)
    {
        _state = new ColumnStateSnapshot(items);
    }
}
