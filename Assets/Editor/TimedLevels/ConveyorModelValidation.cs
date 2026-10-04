using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ConveyorModelValidation
{
    private const int BoardShelfCount = 11;

    public static void Run(LevelCatalog catalog)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));

        ValidateRuntimeColumnShift();
        ValidateSnapshotColumnShift();
        ValidateShiftAfterSingleMatch();
        ValidateNoShiftWithoutMatch();
        ValidateSingleShiftForDoubleMatch();
        ValidateCascadeBeforeShift();
        ValidateMatchFromShiftWithoutSecondShift();
        ValidateShortColumnsKeepOrder();
        ValidateFingerprintMarker();
        ValidateLevelsWithoutConveyors(catalog);
        ValidateConveyorLevelSolutions(catalog);
        ValidateShelfBoardConveyorMarking();
        ValidateDefinitionErrors(catalog);
    }

    private static void ValidateRuntimeColumnShift()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            ItemType[] types = { ItemType.Ball, ItemType.Bear, ItemType.Plant };

            for (int count = 0; count <= types.Length; count++)
            {
                ShelfColumn column = new ShelfColumn();

                for (int index = 0; index < count; index++)
                    column.Append(TemporaryShelfFactory.CreateItem(types[index], temporaryObjects));

                ShelfItem[] before = column.Items.ToArray();
                bool isShifted = column.MoveFrontToBack();
                ItemType[] expected = ShiftTypes(types.Take(count).ToArray());

                if (isShifted != count >= 2
                    || !column.Items.Select(item => item.Type).SequenceEqual(expected)
                    || before.Any(item => item.Column != column)
                    || column.Count != count)
                {
                    throw new InvalidOperationException($"Conveyor M1: runtime shift of {count} items is wrong.");
                }
            }
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateSnapshotColumnShift()
    {
        ItemType[] types = { ItemType.Ball, ItemType.Bear, ItemType.Plant };

        for (int count = 0; count <= types.Length; count++)
        {
            ColumnStateSnapshot column = Column(types.Take(count).ToArray());
            ColumnStateSnapshot shifted = column.MoveFrontToBack();

            if (!shifted.Items.SequenceEqual(ShiftTypes(types.Take(count).ToArray())) || count < 2 && shifted != column)
                throw new InvalidOperationException($"Conveyor M2: snapshot shift of {count} items is wrong.");
        }
    }

    private static void ValidateShiftAfterSingleMatch()
    {
        BoardStateSnapshot board = Board(
            Shelf(false, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(false, Column(ItemType.Ball, ItemType.Lamp), Column(ItemType.Toy), Column(ItemType.Bear)),
            Shelf(true, Column(ItemType.Plant, ItemType.Lamp), Column(ItemType.Bear, ItemType.Toy), Column(ItemType.MapBall)));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 2), "M3");

        if (!simulation.ConveyorsShifted
            || simulation.MatchCount != 1
            || simulation.MatchCountAfterShift != 0
            || !simulation.AffectedShelfIndexes.Contains(2)
            || !HasColumns(simulation.State.Shelves[2], new[] { ItemType.Lamp, ItemType.Plant }, new[] { ItemType.Toy, ItemType.Bear }, new[] { ItemType.MapBall }))
        {
            throw new InvalidOperationException("Conveyor M3: a single match must shift the conveyor once.");
        }
    }

    private static void ValidateNoShiftWithoutMatch()
    {
        BoardStateSnapshot board = Board(
            Shelf(false, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(false, Column(ItemType.Toy, ItemType.Lamp), Column(ItemType.Bear), Column()),
            Shelf(true, Column(ItemType.Plant, ItemType.Lamp), Column(ItemType.Bear, ItemType.Toy), Column(ItemType.MapBall)));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 2), "M4");

        if (simulation.ConveyorsShifted
            || simulation.MatchCount != 0
            || simulation.AffectedShelfIndexes.Contains(2)
            || simulation.State.Shelves[2] != board.Shelves[2])
        {
            throw new InvalidOperationException("Conveyor M4: a move without a match must not shift the conveyor.");
        }
    }

    private static void ValidateSingleShiftForDoubleMatch()
    {
        BoardStateSnapshot board = Board(
            Shelf(false, Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Lamp)),
            Shelf(false, Column(ItemType.Lamp), Column(ItemType.Lamp), Column(ItemType.Ball)),
            Shelf(true, Column(ItemType.Plant, ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear, ItemType.MapBall), Column(ItemType.Beauty)));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(0, 2), new ColumnPosition(1, 2), "M5");

        if (!simulation.IsSwap
            || simulation.MatchCount != 2
            || simulation.MatchCountAfterShift != 0
            || !HasColumns(simulation.State.Shelves[2], new[] { ItemType.Lamp, ItemType.Toy, ItemType.Plant }, new[] { ItemType.MapBall, ItemType.Bear }, new[] { ItemType.Beauty }))
        {
            throw new InvalidOperationException("Conveyor M5: a swap with two matches must shift the conveyor exactly once.");
        }
    }

    private static void ValidateCascadeBeforeShift()
    {
        BoardStateSnapshot board = Board(
            Shelf(true, Column(ItemType.Ball, ItemType.Toy, ItemType.Plant), Column(ItemType.Ball, ItemType.Toy), Column(ItemType.Lamp, ItemType.Toy)),
            Shelf(false, Column(ItemType.Ball), Column(ItemType.Bear), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(0, 2), new ColumnPosition(1, 0), "M6");

        if (simulation.MatchCount != 2
            || simulation.MatchCountAfterShift != 0
            || simulation.Matches[0].Type != ItemType.Ball
            || simulation.Matches[1].Type != ItemType.Toy
            || !HasColumns(simulation.State.Shelves[0], new[] { ItemType.Plant }, Array.Empty<ItemType>(), Array.Empty<ItemType>()))
        {
            throw new InvalidOperationException("Conveyor M6: cascades must resolve before the conveyor shift.");
        }
    }

    private static void ValidateMatchFromShiftWithoutSecondShift()
    {
        BoardStateSnapshot board = Board(
            Shelf(false, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(false, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Toy)),
            Shelf(true,
                Column(ItemType.Lamp, ItemType.Toy, ItemType.Plant, ItemType.Beauty),
                Column(ItemType.Plant, ItemType.Toy, ItemType.MapBall, ItemType.Beauty),
                Column(ItemType.MapBall, ItemType.Toy, ItemType.Lamp, ItemType.Beauty)));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 2), "M7");
        BoardStateSnapshot secondShift = new BoardMoveSimulator().ShiftConveyors(simulation.State);

        if (!simulation.ConveyorsShifted
            || simulation.MatchCount != 2
            || simulation.MatchCountAfterShift != 1
            || simulation.Matches[1].Type != ItemType.Toy
            || simulation.State.Shelves[2].HasMatch()
            || !secondShift.Shelves[2].HasMatch())
        {
            throw new InvalidOperationException("Conveyor M7: a match from the shift must be scored without a second shift.");
        }
    }

    private static void ValidateShortColumnsKeepOrder()
    {
        BoardStateSnapshot board = Board(
            Shelf(false, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Shelf(false, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Toy)),
            Shelf(true, Column(), Column(ItemType.Lamp), Column(ItemType.Plant, ItemType.Toy)));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 2), "M8");

        if (!simulation.ConveyorsShifted
            || !HasColumns(simulation.State.Shelves[2], Array.Empty<ItemType>(), new[] { ItemType.Lamp }, new[] { ItemType.Toy, ItemType.Plant }))
        {
            throw new InvalidOperationException("Conveyor M8: empty and single-item columns must not change.");
        }
    }

    private static void ValidateFingerprintMarker()
    {
        ColumnStateSnapshot[] columns = { Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant) };
        ColumnStateSnapshot[] otherColumns = { Column(ItemType.Lamp), Column(ItemType.Toy), Column(ItemType.MapBall) };
        BoardStateSnapshot regular = Board(Shelf(false, columns), Shelf(false, otherColumns));
        BoardStateSnapshot conveyor = Board(Shelf(true, columns), Shelf(false, otherColumns));

        if (BoardStateFingerprint.CreateKey(regular) == BoardStateFingerprint.CreateKey(conveyor)
            || BoardStateFingerprint.CreateHash(regular) == BoardStateFingerprint.CreateHash(conveyor))
        {
            throw new InvalidOperationException("Conveyor M10: a conveyor shelf must differ from a regular shelf with the same items.");
        }

        BoardStateSnapshot conveyors = Board(Shelf(true, columns), Shelf(true, otherColumns));
        BoardStateSnapshot swappedConveyors = Board(Shelf(true, otherColumns), Shelf(true, columns));

        if (BoardStateFingerprint.CreateKey(conveyors) != BoardStateFingerprint.CreateKey(swappedConveyors)
            || BoardStateFingerprint.CreateHash(conveyors) == BoardStateFingerprint.CreateHash(swappedConveyors))
        {
            throw new InvalidOperationException("Conveyor M11: swapped conveyor shelves must share a key and differ in hash.");
        }
    }

    private static void ValidateLevelsWithoutConveyors(LevelCatalog catalog)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        foreach (LevelEntry level in catalog.Levels.Where(level => !level.Definition.HasConveyors))
        {
            foreach (TimedLevelVariant variant in level.Definition.Variants)
            {
                BoardStateSnapshot state = variant.CreateLayout();

                if (state.HasConveyors)
                    throw new InvalidOperationException($"Conveyor M12: level {level.Number}, seed {variant.Seed} has a conveyor shelf.");

                foreach (TimedLevelMove move in variant.CreateSolution())
                {
                    if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                        || simulation.ConveyorsShifted
                        || simulation.MatchCount != (simulation.IsSwap ? 0 : 1))
                    {
                        throw new InvalidOperationException($"Conveyor M12: level {level.Number}, seed {variant.Seed} no longer replays with one match per placement.");
                    }

                    state = simulation.State;
                }
            }
        }
    }

    private static void ValidateConveyorLevelSolutions(LevelCatalog catalog)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        foreach (LevelEntry level in catalog.Levels.Where(level => level.Definition.HasConveyors))
        {
            foreach (TimedLevelVariant variant in level.Definition.Variants)
            {
                BoardStateSnapshot state = variant.CreateLayout();

                if (!state.HasConveyors)
                    throw new InvalidOperationException($"Conveyor solutions: level {level.Number}, seed {variant.Seed} has no conveyor shelf.");

                foreach (TimedLevelMove move in variant.CreateSolution())
                {
                    if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                        || !simulation.IsAllowed
                        || simulation.MatchCount != 1
                        || !simulation.ConveyorsShifted
                        || simulation.MatchCountAfterShift != 0)
                    {
                        throw new InvalidOperationException($"Conveyor solutions: level {level.Number}, seed {variant.Seed} does not replay as one match and one shift per move.");
                    }

                    Dictionary<ItemType, int> expectedCounts = CountItems(state);
                    expectedCounts[simulation.Matches[0].Type] -= simulation.Matches[0].ItemCount;

                    if (!CountItems(simulation.State).OrderBy(pair => pair.Key).SequenceEqual(expectedCounts.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key)))
                        throw new InvalidOperationException($"Conveyor R9: level {level.Number}, seed {variant.Seed} changes item counts beyond the match.");

                    state = simulation.State;
                }

                if (!state.IsCleared)
                    throw new InvalidOperationException($"Conveyor solutions: level {level.Number}, seed {variant.Seed} does not clear the board.");
            }
        }
    }

    private static Dictionary<ItemType, int> CountItems(BoardStateSnapshot board)
    {
        return board.Shelves
            .SelectMany(shelf => shelf.Columns)
            .SelectMany(column => column.Items)
            .GroupBy(type => type)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static void ValidateShelfBoardConveyorMarking()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf closedShelf = TemporaryShelfFactory.CreateShelf("ClosedShelf", temporaryObjects);
            Shelf conveyorShelf = TemporaryShelfFactory.CreateShelf("ConveyorShelf", temporaryObjects);
            Shelf foreignShelf = TemporaryShelfFactory.CreateShelf("ForeignShelf", temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, closedShelf, conveyorShelf);
            board.Initialize();
            board.CloseShelf(closedShelf);

            ExpectFailure(() => board.MarkConveyor(foreignShelf), "Conveyor M13: a shelf from another board was marked.");
            ExpectFailure(() => board.MarkConveyor(closedShelf), "Conveyor M13: a closed shelf was marked.");
            board.MarkConveyor(conveyorShelf);
            ExpectFailure(() => board.MarkConveyor(conveyorShelf), "Conveyor M13: a shelf was marked twice.");

            board.OpenShelf(closedShelf);
            ExpectFailure(() => board.CloseShelf(conveyorShelf), "Conveyor M13: a conveyor shelf was closed.");

            if (!board.HasConveyors
                || board.ConveyorShelves.Count != 1
                || board.ConveyorShelves[0] != conveyorShelf
                || !board.CreateSnapshot().Shelves[1].IsConveyor
                || board.CreateSnapshot().Shelves[0].IsConveyor)
            {
                throw new InvalidOperationException("Conveyor M13: the board does not report its conveyor shelf.");
            }
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateDefinitionErrors(LevelCatalog catalog)
    {
        TimedLevelDefinition template = catalog.Levels.First(level => level.SceneName == "FifthLevel").Definition;
        BoardStateSnapshot board = Board(Enumerable.Range(0, BoardShelfCount)
            .Select(_ => Shelf(false, Column(), Column(), Column()))
            .ToArray());

        List<TimedLevelDefinition> definitions = new List<TimedLevelDefinition>();

        try
        {
            TimedLevelValidator.ValidateForGeneration(CreateDefinition(template, new[] { 9 }, false, definitions), board);
            ExpectFailure(() => TimedLevelValidator.ValidateForGeneration(CreateDefinition(template, new[] { 9, 9 }, false, definitions), board), "Conveyor M14: a duplicated conveyor index passed validation.");
            ExpectFailure(() => TimedLevelValidator.ValidateForGeneration(CreateDefinition(template, new[] { -1 }, false, definitions), board), "Conveyor M14: a negative conveyor index passed validation.");
            ExpectFailure(() => TimedLevelValidator.ValidateForGeneration(CreateDefinition(template, new[] { BoardShelfCount }, false, definitions), board), "Conveyor M14: an out-of-range conveyor index passed validation.");
            ExpectFailure(() => TimedLevelValidator.ValidateForGeneration(CreateDefinition(template, new[] { 9 }, true, definitions), board), "Conveyor M14: conveyors combined with closed shelves passed validation.");
        }
        finally
        {
            foreach (TimedLevelDefinition definition in definitions)
                UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static TimedLevelDefinition CreateDefinition(
        TimedLevelDefinition template,
        IReadOnlyList<int> conveyorShelfIndices,
        bool hasClosedShelf,
        List<TimedLevelDefinition> definitions)
    {
        TimedLevelDefinition definition = UnityEngine.Object.Instantiate(template);
        definition.hideFlags = HideFlags.HideAndDontSave;
        definitions.Add(definition);
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_variants").arraySize = 0;
        SerializedProperty indices = serializedDefinition.FindProperty("_conveyorShelfIndices");
        indices.arraySize = conveyorShelfIndices.Count;

        for (int index = 0; index < conveyorShelfIndices.Count; index++)
            indices.GetArrayElementAtIndex(index).intValue = conveyorShelfIndices[index];

        serializedDefinition.FindProperty("_closedShelves").arraySize = hasClosedShelf ? 1 : 0;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static BoardMoveSimulation Simulate(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target, string caseName)
    {
        if (!new BoardMoveSimulator().TrySimulate(board, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            throw new InvalidOperationException($"Conveyor {caseName}: the move was rejected.");

        if (board.ItemCount - simulation.MatchCount * TimedLevelLayoutRules.ConveyorShelfCapacity != simulation.State.ItemCount)
            throw new InvalidOperationException($"Conveyor M9 ({caseName}): items were created or destroyed.");

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

    private static bool HasColumns(ShelfStateSnapshot shelf, params ItemType[][] expectedColumns)
    {
        return shelf.Capacity == expectedColumns.Length
            && shelf.Columns.Select((column, index) => column.Items.SequenceEqual(expectedColumns[index])).All(isEqual => isEqual);
    }

    private static ItemType[] ShiftTypes(ItemType[] types)
    {
        return types.Length < 2 ? types : types.Skip(1).Append(types[0]).ToArray();
    }

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    private static ShelfStateSnapshot Shelf(bool isConveyor, params ColumnStateSnapshot[] columns) => new ShelfStateSnapshot(columns, true, isConveyor);

    private static BoardStateSnapshot Board(params ShelfStateSnapshot[] shelves) => new BoardStateSnapshot(shelves);
}
