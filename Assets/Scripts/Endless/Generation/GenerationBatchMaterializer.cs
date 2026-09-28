using System;
using System.Collections.Generic;

public static class GenerationBatchMaterializer
{
    public static IReadOnlyList<Shelf> Append(ShelfBoard board, GenerationBatch batch, ShelfItemPool pool)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (batch == null)
            throw new ArgumentNullException(nameof(batch));

        if (pool == null)
            throw new ArgumentNullException(nameof(pool));

        List<Shelf> touched = new List<Shelf>();

        for (int shelfIndex = 0; shelfIndex < batch.ShelfCount; shelfIndex++)
        {
            Shelf shelf = board.Shelves[shelfIndex];
            bool shelfTouched = false;

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                foreach (ItemType type in batch.GetItems(shelfIndex, columnIndex))
                {
                    shelf.Columns[columnIndex].Append(pool.Get(type));
                    shelfTouched = true;
                }
            }

            if (shelfTouched)
                touched.Add(shelf);
        }

        return touched.AsReadOnly();
    }
}
