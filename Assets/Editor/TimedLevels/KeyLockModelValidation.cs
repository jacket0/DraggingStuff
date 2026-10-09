using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class KeyLockModelValidation
{
    private const int ShelfCapacity = 3;

    public static void Run(LevelCatalog catalog)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));

        ValidateClosedShelfDoesNotMatch();
        ValidateMovesWithLockedShelfRejected();
        ValidatePlacementCollectsKey();
        ValidateMatchCollectsKeyAndOpensRow();
        ValidateHiddenKeyStaysInColumn();
        ValidateTwoKeysInOneMove();
        ValidateCascadeCollectsKey();
        ValidateFrontKeyBlocksColumn();
        ValidateLevelsWithoutKeys(catalog);
        ValidateFingerprintKeys();
        ValidateShelfBoardKeyLocks();
        ValidateRuntimeColumnKeys();
    }

    private static void ValidateClosedShelfDoesNotMatch()
    {
        BoardStateSnapshot board = Board(
            LockedShelf(Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Ball)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            OpenShelf(Column(ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "M1");

        if (board.CanMatch(0)
            || simulation.MatchCount != 1
            || simulation.Matches[0].Type != ItemType.Lamp
            || simulation.State.Shelves[0] != board.Shelves[0])
        {
            throw new InvalidOperationException("Key locks M1: a ready row behind a closed shelf must not match.");
        }
    }

    private static void ValidateMovesWithLockedShelfRejected()
    {
        BoardStateSnapshot board = Board(
            LockedShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(), Column(ItemType.Toy)));
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        if (simulator.TrySimulate(board, new ColumnPosition(0, 0), new ColumnPosition(1, 1), out _)
            || simulator.TrySimulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 2), out _)
            || simulator.TrySimulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 0), out _)
            || simulator.TrySimulate(board, new ColumnPosition(0, 1), new ColumnPosition(1, 2), out _))
        {
            throw new InvalidOperationException("Key locks M2: a move from, into or a swap with a locked shelf must be rejected.");
        }
    }

    private static void ValidatePlacementCollectsKey()
    {
        BoardStateSnapshot board = Board(
            LockedShelf(Column(ItemType.Ball, ItemType.Toy), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear), Column()),
            OpenShelf(Column(ItemType.MapBall), Column(ItemType.Beauty), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(1, 2), "M3");

        if (simulation.MatchCount != 0
            || !simulation.OpenedShelfIndexes.SequenceEqual(new[] { 0 })
            || !simulation.State.Shelves[0].IsOpen
            || simulation.State.HasKeys
            || !simulation.AffectedShelfIndexes.Contains(0)
            || simulation.State.Shelves[1].Columns[0].FrontItem != ItemType.Toy)
        {
            throw new InvalidOperationException("Key locks M3: a placement that brings a key to the front must collect it and open its shelf.");
        }
    }

    private static void ValidateMatchCollectsKeyAndOpensRow()
    {
        BoardStateSnapshot board = Board(
            LockedShelf(Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Ball)),
            OpenShelf(KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.Lamp), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Bear), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "M4");

        if (simulation.MatchCount != 2
            || simulation.Matches[0].Type != ItemType.Lamp
            || simulation.Matches[1].Type != ItemType.Ball
            || simulation.Matches[1].ShelfIndex != 0
            || simulation.OpenedShelfCount != 1
            || !simulation.State.Shelves[0].IsOpen
            || simulation.State.Shelves[0].Columns.Any(column => !column.IsEmpty))
        {
            throw new InvalidOperationException("Key locks M4: a row revealed by opening a shelf must match in the same move.");
        }
    }

    private static void ValidateHiddenKeyStaysInColumn()
    {
        BoardStateSnapshot board = Board(
            LockedShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(KeyColumn(2, 0, ItemType.Lamp, ItemType.Toy, ItemType.Bear), Column(ItemType.MapBall), Column()),
            OpenShelf(Column(ItemType.Beauty), Column(), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(1, 2), "M5");
        ColumnStateSnapshot keyColumn = simulation.State.Shelves[1].Columns[0];

        if (simulation.OpenedShelfCount != 0
            || simulation.State.Shelves[0].IsOpen
            || simulation.State.KeyCount != 1
            || keyColumn.Keys[0].ItemsInFront != 1
            || keyColumn.FrontItem != ItemType.Toy)
        {
            throw new InvalidOperationException("Key locks M5: a key that becomes second in its column must stay there and keep its shelf closed.");
        }
    }

    private static void ValidateTwoKeysInOneMove()
    {
        BoardStateSnapshot placementBoard = Board(
            LockedShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            LockedShelf(Column(ItemType.Toy), Column(ItemType.MapBall), Column(ItemType.Beauty)),
            OpenShelf(KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear), Column(ItemType.MapBall)),
            OpenShelf(KeyColumn(1, 1, ItemType.Lamp, ItemType.TrainToy), Column(ItemType.Lamp), Column()));
        BoardMoveSimulation placement = Simulate(placementBoard, new ColumnPosition(2, 0), new ColumnPosition(3, 2), "M6");

        if (!placement.OpenedShelfIndexes.SequenceEqual(new[] { 0, 1 })
            || !placement.State.Shelves[0].IsOpen
            || !placement.State.Shelves[1].IsOpen
            || placement.State.HasKeys)
        {
            throw new InvalidOperationException("Key locks M6: two keys freed by one move must each open their own shelf once.");
        }

        BoardStateSnapshot matchBoard = Board(
            LockedShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            LockedShelf(Column(ItemType.Toy), Column(ItemType.MapBall), Column(ItemType.Beauty)),
            OpenShelf(KeyColumn(1, 1, ItemType.Lamp, ItemType.TrainToy), Column(ItemType.Ball, ItemType.Bear), KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Plant), Column()));
        BoardMoveSimulation match = Simulate(matchBoard, new ColumnPosition(3, 0), new ColumnPosition(2, 1), "M6");

        if (!match.IsSwap
            || match.MatchCount != 1
            || !match.OpenedShelfIndexes.SequenceEqual(new[] { 1, 0 })
            || match.State.HasKeys)
        {
            throw new InvalidOperationException("Key locks M6: keys freed by one match must be collected in column order.");
        }
    }

    private static void ValidateCascadeCollectsKey()
    {
        BoardStateSnapshot board = Board(
            LockedShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(KeyColumn(2, 0, ItemType.Lamp, ItemType.Toy, ItemType.Bear), Column(ItemType.Lamp, ItemType.Toy), Column(ItemType.Ball, ItemType.Toy)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.MapBall), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "M7");

        if (simulation.MatchCount != 2
            || simulation.Matches[1].Type != ItemType.Toy
            || !simulation.OpenedShelfIndexes.SequenceEqual(new[] { 0 })
            || !simulation.State.Shelves[0].IsOpen)
        {
            throw new InvalidOperationException("Key locks M7: a key freed by a cascade must open its shelf in the same move.");
        }
    }

    private static void ValidateFrontKeyBlocksColumn()
    {
        BoardStateSnapshot board = Board(
            LockedShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(KeyColumn(0, 0, ItemType.Lamp), Column(ItemType.Lamp), Column(ItemType.Lamp)),
            OpenShelf(KeyColumn(0, 0), Column(ItemType.Toy), Column()));
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        ColumnStateSnapshot frontKeyColumn = board.Shelves[1].Columns[0];

        if (frontKeyColumn.FrontItem != null
            || frontKeyColumn.IsEmpty
            || board.Shelves[2].Columns[0].IsEmpty
            || board.EmptyColumnCount != 1
            || board.Shelves[1].HasMatch()
            || board.CanMatch(1)
            || simulator.TrySimulate(board, new ColumnPosition(1, 0), new ColumnPosition(2, 2), out _)
            || simulator.TrySimulate(board, new ColumnPosition(2, 1), new ColumnPosition(1, 0), out _)
            || simulator.TrySimulate(board, new ColumnPosition(2, 1), new ColumnPosition(2, 0), out _))
        {
            throw new InvalidOperationException("Key locks M8: a column with a key in front must have no front item to take, receive or match.");
        }
    }

    private static void ValidateLevelsWithoutKeys(LevelCatalog catalog)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        foreach (LevelEntry level in catalog.Levels)
        {
            foreach (TimedLevelVariant variant in level.Definition.Variants)
            {
                BoardStateSnapshot state = variant.CreateLayout();

                if (state.HasKeys || BoardStateFingerprint.CreateHash(state) != variant.LayoutHash)
                    throw new InvalidOperationException($"Key locks M10: level {level.Number}, seed {variant.Seed} changed its layout hash.");

                foreach (TimedLevelMove move in variant.CreateSolution())
                {
                    if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                        || !simulation.IsAllowed
                        || simulation.OpenedShelfCount != 0)
                    {
                        throw new InvalidOperationException($"Key locks M10: level {level.Number}, seed {variant.Seed} no longer replays without keys.");
                    }

                    state = simulation.State;
                }
            }
        }
    }

    private static void ValidateFingerprintKeys()
    {
        ShelfStateSnapshot lockedShelf = LockedShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant));
        ShelfStateSnapshot otherLockedShelf = LockedShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant));
        ShelfStateSnapshot otherShelf = OpenShelf(Column(ItemType.Toy), Column(), Column());
        BoardStateSnapshot[] boards =
        {
            Board(lockedShelf, otherLockedShelf, OpenShelf(Column(ItemType.Lamp, ItemType.Toy), Column(ItemType.MapBall), Column()), otherShelf),
            Board(lockedShelf, otherLockedShelf, OpenShelf(KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.MapBall), Column()), otherShelf),
            Board(lockedShelf, otherLockedShelf, OpenShelf(KeyColumn(2, 0, ItemType.Lamp, ItemType.Toy), Column(ItemType.MapBall), Column()), otherShelf),
            Board(lockedShelf, otherLockedShelf, OpenShelf(KeyColumn(1, 1, ItemType.Lamp, ItemType.Toy), Column(ItemType.MapBall), Column()), otherShelf),
            Board(lockedShelf, otherLockedShelf, OpenShelf(Column(ItemType.Lamp, ItemType.Toy), KeyColumn(1, 0, ItemType.MapBall), Column()), otherShelf)
        };

        if (boards.Select(BoardStateFingerprint.CreateKey).Distinct().Count() != boards.Length
            || boards.Select(BoardStateFingerprint.CreateHash).Distinct().Count() != boards.Length)
        {
            throw new InvalidOperationException("Key locks M11: the same items with a key in another place or for another shelf must differ in key and hash.");
        }

        BoardStateSnapshot keyBoard = Board(lockedShelf, OpenShelf(KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(), Column()), otherShelf);
        BoardStateSnapshot reorderedKeyBoard = Board(lockedShelf, otherShelf, OpenShelf(KeyColumn(1, 0, ItemType.Lamp, ItemType.Toy), Column(), Column()));

        if (BoardStateFingerprint.CreateKey(keyBoard) == BoardStateFingerprint.CreateKey(reorderedKeyBoard))
            throw new InvalidOperationException("Key locks M11: shelves of a board with keys must keep their physical order in the search key.");
    }

    private static void ValidateShelfBoardKeyLocks()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf lockedShelf = CreateFilledShelf("LockedShelf", temporaryObjects);
            Shelf emptyShelf = TemporaryShelfFactory.CreateShelf("EmptyShelf", temporaryObjects);
            Shelf conveyorShelf = CreateFilledShelf("ConveyorShelf", temporaryObjects);
            Shelf filteredShelf = CreateFilledShelf("FilteredShelf", temporaryObjects);
            Shelf foreignShelf = CreateFilledShelf("ForeignShelf", temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, lockedShelf, emptyShelf, conveyorShelf, filteredShelf);
            board.Initialize();
            board.MarkConveyor(conveyorShelf);
            board.SetFilter(filteredShelf, new[] { ItemType.Ball });

            ExpectFailure(() => board.CloseKeyLockedShelf(emptyShelf), "Key locks M12: an empty shelf was locked by a key.");
            ExpectFailure(() => board.CloseKeyLockedShelf(conveyorShelf), "Key locks M12: a conveyor shelf was locked by a key.");
            ExpectFailure(() => board.CloseKeyLockedShelf(filteredShelf), "Key locks M12: a filtered shelf was locked by a key.");
            ExpectFailure(() => board.CloseKeyLockedShelf(foreignShelf), "Key locks M12: a shelf from another board was locked by a key.");
            ExpectFailure(() => board.OpenKeyLockedShelf(lockedShelf), "Key locks M12: an open shelf was opened by a key.");

            board.CloseKeyLockedShelf(lockedShelf);
            ExpectFailure(() => board.CloseKeyLockedShelf(lockedShelf), "Key locks M12: a shelf was locked by a key twice.");

            if (!board.HasKeyLocks
                || !board.IsKeyLocked(lockedShelf)
                || !board.IsShelfClosed(lockedShelf)
                || board.IsShelfAvailable(lockedShelf)
                || board.IsCleared
                || board.CreateSnapshot().Shelves[0].IsOpen)
            {
                throw new InvalidOperationException("Key locks M12: the board does not report its key-locked shelf.");
            }

            board.OpenKeyLockedShelf(lockedShelf);
            ExpectFailure(() => board.OpenKeyLockedShelf(lockedShelf), "Key locks M12: a shelf was opened by a key twice.");

            if (board.HasKeyLocks || board.IsKeyLocked(lockedShelf) || board.IsShelfClosed(lockedShelf) || !board.CreateSnapshot().Shelves[0].IsOpen)
                throw new InvalidOperationException("Key locks M12: an opened shelf must become a regular shelf.");
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateRuntimeColumnKeys()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf shelf = TemporaryShelfFactory.CreateShelf("KeyShelf", temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, shelf);
            board.Initialize();
            ShelfColumn column = shelf.Columns[0];
            ShelfColumn otherColumn = shelf.Columns[1];
            ShelfItem lamp = TemporaryShelfFactory.CreateItem(ItemType.Lamp, temporaryObjects);
            ShelfItem toy = TemporaryShelfFactory.CreateItem(ItemType.Toy, temporaryObjects);
            ShelfKey key = TemporaryShelfFactory.CreateKey(temporaryObjects);
            column.Append(lamp);
            column.Append(toy);
            otherColumn.Append(TemporaryShelfFactory.CreateItem(ItemType.Bear, temporaryObjects));
            column.AddKey(key, 1);

            ExpectFailure(() => column.AddKey(TemporaryShelfFactory.CreateKey(temporaryObjects), 1), "Key locks M13: two keys shared a place in a column.");
            ExpectFailure(() => shelf.CreateSnapshot(), "Key locks M13: a column with keys created a snapshot without its board.");

            if (column.GetQueueSlot(lamp) != 0 || column.GetQueueSlot(key) != 1 || column.GetQueueSlot(toy) != 2 || column.FrontItem != lamp || column.FrontKey != null)
                throw new InvalidOperationException("Key locks M13: queue slots before the key are wrong.");

            if (column.TakeFront() != lamp || column.FrontKey != key || column.FrontItem != null || column.IsEmpty || column.GetQueueSlot(key) != 0 || column.GetQueueSlot(toy) != 1)
                throw new InvalidOperationException("Key locks M13: taking the item in front of the key must bring the key to the front.");

            ExpectFailure(() => column.TakeFront(), "Key locks M13: an item was taken from behind a front key.");
            ExpectFailure(() => column.SwapFrontWith(otherColumn), "Key locks M13: a column with a front key swapped its front.");
            ExpectFailure(() => otherColumn.SwapFrontWith(column), "Key locks M13: a front was swapped into a column with a front key.");
            ExpectFailure(() => column.PlaceFront(lamp), "Key locks M13: an item was placed in front of a front key.");

            if (board.CanPickUp(column))
                throw new InvalidOperationException("Key locks M13: a column with a front key can be picked up.");

            if (column.TakeFrontKey() != key || key.Column != null || column.HasKeys || column.FrontItem != toy || column.GetQueueSlot(toy) != 0)
                throw new InvalidOperationException("Key locks M13: taking the front key must leave the next item in front.");

            ExpectFailure(() => column.TakeFrontKey(), "Key locks M13: a key was taken from a column without a front key.");
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static Shelf CreateFilledShelf(string name, List<GameObject> temporaryObjects)
    {
        Shelf shelf = TemporaryShelfFactory.CreateShelf(name, temporaryObjects);

        foreach (ShelfColumn column in shelf.Columns)
            column.Append(TemporaryShelfFactory.CreateItem(ItemType.Ball, temporaryObjects));

        return shelf;
    }

    private static BoardMoveSimulation Simulate(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target, string caseName)
    {
        if (!new BoardMoveSimulator().TrySimulate(board, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            throw new InvalidOperationException($"Key locks {caseName}: the move was rejected.");

        if (board.ItemCount - simulation.MatchCount * ShelfCapacity != simulation.State.ItemCount)
            throw new InvalidOperationException($"Key locks M9 ({caseName}): items were created or destroyed.");

        return simulation;
    }

    private static void ExpectFailure(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    private static ColumnStateSnapshot KeyColumn(int itemsInFront, int lockedShelfIndex, params ItemType[] items)
    {
        return new ColumnStateSnapshot(items, new[] { new ColumnKeySnapshot(itemsInFront, lockedShelfIndex) });
    }

    private static ShelfStateSnapshot OpenShelf(params ColumnStateSnapshot[] columns) => new ShelfStateSnapshot(columns, true);

    private static ShelfStateSnapshot LockedShelf(params ColumnStateSnapshot[] columns) => new ShelfStateSnapshot(columns, false);

    private static BoardStateSnapshot Board(params ShelfStateSnapshot[] shelves) => new BoardStateSnapshot(shelves);
}
