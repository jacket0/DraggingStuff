using System;
using System.Collections.Generic;

public sealed class BoardShelfSnapshot
{
    private readonly BoardLayerSnapshot[] _layers;

    public int Capacity { get; }
    public IReadOnlyList<BoardLayerSnapshot> Layers => _layers;

    public BoardShelfSnapshot(int capacity, IReadOnlyList<BoardLayerSnapshot> layers)
    {
        if (!Shelf.IsValidCapacity(capacity))
            throw new ArgumentOutOfRangeException(nameof(capacity));

        if (layers == null)
            throw new ArgumentNullException(nameof(layers));

        Capacity = capacity;
        _layers = new BoardLayerSnapshot[layers.Count];

        for (int index = 0; index < layers.Count; index++)
        {
            BoardLayerSnapshot layer = layers[index] ?? throw new ArgumentException(nameof(layers));

            if (layer.Items.Count != Capacity)
                throw new ArgumentException(nameof(layers));

            _layers[index] = layer;
        }
    }
}
