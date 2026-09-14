using System;
using System.Collections.Generic;

public sealed class BoardSnapshot
{
    private readonly BoardShelfSnapshot[] _shelves;

    public IReadOnlyList<BoardShelfSnapshot> Shelves => _shelves;

    public BoardSnapshot(IReadOnlyList<BoardShelfSnapshot> shelves)
    {
        if (shelves == null)
            throw new ArgumentNullException(nameof(shelves));

        _shelves = new BoardShelfSnapshot[shelves.Count];

        for (int index = 0; index < shelves.Count; index++)
            _shelves[index] = shelves[index] ?? throw new ArgumentException(nameof(shelves));
    }
}
