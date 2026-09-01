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

    public BoardSnapshot Append(GenerationBatch batch)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));

        if (batch.ShelfCount != _shelves.Length)
            throw new ArgumentException(nameof(batch));

        BoardShelfSnapshot[] combinedShelves = new BoardShelfSnapshot[_shelves.Length];

        for (int shelfIndex = 0; shelfIndex < _shelves.Length; shelfIndex++)
        {
            List<BoardLayerSnapshot> layers = new List<BoardLayerSnapshot>(_shelves[shelfIndex].Layers);
            layers.AddRange(batch.GetLayers(shelfIndex));
            combinedShelves[shelfIndex] = new BoardShelfSnapshot(_shelves[shelfIndex].Capacity, layers);
        }

        return new BoardSnapshot(combinedShelves);
    }
}
