using System;
using System.Collections.Generic;
using System.Linq;

public sealed class EndlessLayoutGenerator
{
    private readonly System.Random _random;
    private readonly EndlessGenerationConfig _config;
    private readonly ShelfItemCatalog _catalog;

    public EndlessLayoutGenerator(System.Random random, EndlessGenerationConfig config, ShelfItemCatalog catalog)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _config.Validate();
        _catalog.Validate();

        if (_catalog.Entries.Count < 2)
            throw new ArgumentException("At least two item types are required.", nameof(catalog));
    }

    public GenerationBatch GenerateInitial(BoardSnapshot snapshot)
    {
        Validate(snapshot);

        if (snapshot.Shelves.Any(shelf => shelf.Columns.Any(column => column.Items.Count > 0)))
            throw new ArgumentException("Initial generation requires an empty board.", nameof(snapshot));

        return Generate(snapshot, 1f);
    }

    public GenerationBatch GenerateRefill(BoardSnapshot snapshot, int matchCount)
    {
        Validate(snapshot);

        if (matchCount < 0)
            throw new ArgumentOutOfRangeException(nameof(matchCount));

        return Generate(snapshot, _config.GetColumnFillChance(matchCount));
    }

    private GenerationBatch Generate(BoardSnapshot snapshot, float fillChance)
    {
        List<ColumnPosition> positions = new List<ColumnPosition>();
        List<ColumnPosition> empty = new List<ColumnPosition>();
        Dictionary<ItemType, int> counts = _catalog.Entries.ToDictionary(entry => entry.Type, _ => 0);
        List<ItemType>[][] columns = new List<ItemType>[snapshot.Shelves.Count][];

        for (int shelfIndex = 0; shelfIndex < columns.Length; shelfIndex++)
        {
            columns[shelfIndex] = new List<ItemType>[snapshot.Shelves[shelfIndex].Capacity];

            for (int columnIndex = 0; columnIndex < columns[shelfIndex].Length; columnIndex++)
            {
                List<ItemType> items = new List<ItemType>(snapshot.Shelves[shelfIndex].Columns[columnIndex].Items);
                columns[shelfIndex][columnIndex] = items;
                ColumnPosition position = new ColumnPosition(shelfIndex, columnIndex);
                positions.Add(position);

                if (items.Count == 0)
                    empty.Add(position);

                foreach (ItemType type in items)
                {
                    if (!counts.ContainsKey(type))
                        throw new ArgumentException("The board contains an item absent from the catalog.", nameof(snapshot));

                    counts[type]++;
                }
            }
        }

        if (empty.Count == 0)
            throw new InvalidOperationException("Refill requires at least one empty column.");

        int reserveCount = Math.Max(_config.MinimumEmptyColumns, (int)Math.Ceiling(positions.Count * _config.EmptyColumnRatio));

        if (reserveCount >= positions.Count)
            throw new InvalidOperationException("The empty column reserve leaves no space for items.");

        Shuffle(empty);
        HashSet<ColumnPosition> reserved = new HashSet<ColumnPosition>(empty.Take(reserveCount));
        Shuffle(positions);
        int minimumLength = positions.Where(position => !reserved.Contains(position)).Min(position => columns[position.ShelfIndex][position.ColumnIndex].Count);
        GenerationBatch batch = new GenerationBatch(snapshot);

        foreach (ColumnPosition position in positions)
        {
            List<ItemType>[] shelf = columns[position.ShelfIndex];
            List<ItemType> items = shelf[position.ColumnIndex];

            if (reserved.Contains(position) || items.Count > minimumLength + 1 || _random.NextDouble() >= fillChance)
                continue;

            ItemType type = ChooseType(shelf, position.ColumnIndex, counts);
            items.Add(type);
            counts[type]++;
            batch.Append(position.ShelfIndex, position.ColumnIndex, type);
        }

        return batch;
    }

    private ItemType ChooseType(IReadOnlyList<List<ItemType>> shelf, int columnIndex, IReadOnlyDictionary<ItemType, int> counts)
    {
        List<ItemType> column = shelf[columnIndex];
        double[] weights = new double[_catalog.Entries.Count];
        double total = 0;

        for (int index = 0; index < weights.Length; index++)
        {
            ShelfItemCatalog.Entry entry = _catalog.Entries[index];

            if (column.Count == 0 && shelf.Count >= Shelf.MinimumMatchCapacity
                && shelf.Where((_, otherIndex) => otherIndex != columnIndex).All(items => items.Count > 0 && items[0] == entry.Type))
                continue;

            int repeats = shelf.Count(items => items.Count > 0 && items[items.Count - 1] == entry.Type);
            double weight = 1d / (1d + counts[entry.Type]);
            weight *= Math.Pow(_config.RepeatedTypeWeight, repeats);
            weights[index] = weight;
            total += weight;
        }

        if (total <= 0)
            throw new InvalidOperationException("No item type can be generated safely.");

        double roll = _random.NextDouble() * total;
        ItemType selected = default;

        for (int index = 0; index < weights.Length; index++)
        {
            if (weights[index] <= 0)
                continue;

            selected = _catalog.Entries[index].Type;
            roll -= weights[index];

            if (roll < 0)
                return selected;
        }

        return selected;
    }

    private static void Validate(BoardSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        if (snapshot.Shelves.Count < 2 || !snapshot.Shelves.Any(shelf => shelf.Capacity >= Shelf.MinimumMatchCapacity))
            throw new ArgumentException("The board needs a matching shelf and a buffer.", nameof(snapshot));
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int other = _random.Next(index + 1);
            T value = values[index];
            values[index] = values[other];
            values[other] = value;
        }
    }
}
