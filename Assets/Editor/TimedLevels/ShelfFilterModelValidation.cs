using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ShelfFilterModelValidation
{
    private const string FilterSceneName = "ThirdLevel";
    private const int BoardShelfCount = 11;

    private static readonly ItemType[] BallOnly = { ItemType.Ball };

    public static void Run(LevelCatalog catalog)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));

        ValidateAcceptedPlacement();
        ValidateRejectedPlacement();
        ValidateRejectedSwap();
        ValidateSwapRemovingJunk();
        ValidateJunkToUniversalColumn();
        ValidateMoveInsideFilteredShelf();
        ValidateSwapBetweenFilters();
        ValidateMatchOnFilteredShelf();
        ValidateAcceptedTypesNormalization();
        ValidateFingerprintMarker();
        ValidateCatalogLevelFilters(catalog);
        ValidateShelfBoardFilters();
        ValidateDefinitionErrors(catalog);
        ValidateVariantErrors(catalog);
        ValidateLockedFilteredShelf();
        ValidateFilteredTypeMatchesOnlyOnItsFilter();
        ValidateJunkMatchesAnywhere();
        ValidateCascadeKeepsFilteredTypeOnRegularShelf();
        ValidateTwoTypeFilterMatches();
        ValidateShelfBoardMatchesLikeSimulator();
    }

    private static void ValidateAcceptedPlacement()
    {
        BoardStateSnapshot board = Board(
            Filter(BallOnly, Column(), Column(ItemType.Bear), Column(ItemType.Lamp)),
            Regular(Column(ItemType.Ball, ItemType.Toy), Column(ItemType.Plant), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 0), "M1");

        if (!HasColumns(simulation.State.Shelves[0], new[] { ItemType.Ball }, new[] { ItemType.Bear }, new[] { ItemType.Lamp })
            || !simulation.State.Shelves[0].AcceptedTypes.SequenceEqual(BallOnly))
        {
            throw new InvalidOperationException("Filter M1: an accepted item must land on the filtered shelf.");
        }
    }

    private static void ValidateRejectedPlacement()
    {
        BoardStateSnapshot board = Board(
            Filter(BallOnly, Column(), Column(ItemType.Bear), Column(ItemType.Lamp)),
            Regular(Column(ItemType.Ball, ItemType.Toy), Column(ItemType.Plant), Column()));

        ExpectRejected(board, new ColumnPosition(1, 1), new ColumnPosition(0, 0), "Filter M2: a rejected type was placed on the filtered shelf.");
    }

    private static void ValidateRejectedSwap()
    {
        BoardStateSnapshot board = Board(
            Filter(BallOnly, Column(ItemType.Bear), Column(ItemType.Lamp), Column()),
            Regular(Column(ItemType.Plant), Column(ItemType.Toy), Column()));

        ExpectRejected(board, new ColumnPosition(1, 0), new ColumnPosition(0, 0), "Filter M3: a swap brought a rejected type onto the filtered shelf.");
        ExpectRejected(board, new ColumnPosition(0, 0), new ColumnPosition(1, 0), "Filter M3: a swap from the filtered shelf brought a rejected type onto it.");
    }

    private static void ValidateSwapRemovingJunk()
    {
        BoardStateSnapshot board = Board(
            Filter(BallOnly, Column(ItemType.Bear), Column(ItemType.Lamp), Column()),
            Regular(Column(ItemType.Ball), Column(ItemType.Toy), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 0), "M4");

        if (!simulation.IsSwap
            || !HasColumns(simulation.State.Shelves[0], new[] { ItemType.Ball }, new[] { ItemType.Lamp }, Array.Empty<ItemType>())
            || !HasColumns(simulation.State.Shelves[1], new[] { ItemType.Bear }, new[] { ItemType.Toy }, Array.Empty<ItemType>()))
        {
            throw new InvalidOperationException("Filter M4: a swap must replace junk with an accepted item.");
        }
    }

    private static void ValidateJunkToUniversalColumn()
    {
        BoardStateSnapshot board = Board(
            Filter(BallOnly, Column(ItemType.Bear, ItemType.Ball), Column(ItemType.Lamp), Column()),
            Regular(Column(ItemType.Plant), Column(ItemType.Toy), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(0, 0), new ColumnPosition(1, 2), "M5");

        if (!HasColumns(simulation.State.Shelves[0], new[] { ItemType.Ball }, new[] { ItemType.Lamp }, Array.Empty<ItemType>())
            || !HasColumns(simulation.State.Shelves[1], new[] { ItemType.Plant }, new[] { ItemType.Toy }, new[] { ItemType.Bear }))
        {
            throw new InvalidOperationException("Filter M5: junk must move to a universal empty column.");
        }
    }

    private static void ValidateMoveInsideFilteredShelf()
    {
        BoardStateSnapshot board = Board(
            Filter(BallOnly, Column(ItemType.Bear), Column(ItemType.Ball, ItemType.Lamp), Column()),
            Regular(Column(ItemType.Plant), Column(ItemType.Toy), Column()));

        ExpectRejected(board, new ColumnPosition(0, 0), new ColumnPosition(0, 2), "Filter M6: junk moved into an empty column of its filtered shelf.");
        ExpectRejected(board, new ColumnPosition(0, 1), new ColumnPosition(0, 0), "Filter M6: a swap inside the filtered shelf kept junk on it.");
        Simulate(board, new ColumnPosition(0, 1), new ColumnPosition(0, 2), "M6");
    }

    private static void ValidateSwapBetweenFilters()
    {
        BoardStateSnapshot accepting = Board(
            Filter(new[] { ItemType.Ball, ItemType.Lamp }, Column(ItemType.Bear), Column(ItemType.Lamp), Column()),
            Filter(new[] { ItemType.Bear }, Column(ItemType.Ball), Column(ItemType.Bear), Column()),
            Regular(Column(ItemType.Plant), Column(), Column()));
        BoardMoveSimulation simulation = Simulate(accepting, new ColumnPosition(1, 0), new ColumnPosition(0, 0), "M7");

        if (!simulation.IsSwap || simulation.State.Shelves[0].Columns[0].Items[0] != ItemType.Ball || simulation.State.Shelves[1].Columns[0].Items[0] != ItemType.Bear)
            throw new InvalidOperationException("Filter M7: a swap accepted by both filters must happen.");

        BoardStateSnapshot rejecting = Board(
            Filter(new[] { ItemType.Ball, ItemType.Lamp }, Column(ItemType.Bear), Column(ItemType.Lamp), Column()),
            Filter(new[] { ItemType.Plant }, Column(ItemType.Ball), Column(ItemType.Plant), Column()),
            Regular(Column(ItemType.Toy), Column(), Column()));

        ExpectRejected(rejecting, new ColumnPosition(1, 0), new ColumnPosition(0, 0), "Filter M7: a swap rejected by one filter happened.");
    }

    private static void ValidateMatchOnFilteredShelf()
    {
        ColumnStateSnapshot[] filteredColumns =
        {
            Column(ItemType.Ball, ItemType.Toy),
            Column(ItemType.Ball, ItemType.Toy),
            Column(ItemType.Lamp, ItemType.Toy)
        };
        ColumnStateSnapshot[] regularColumns = { Column(ItemType.Ball), Column(ItemType.Plant), Column() };
        BoardStateSnapshot filtered = Board(Filter(new[] { ItemType.Ball, ItemType.Toy }, filteredColumns), Regular(regularColumns));
        BoardStateSnapshot unfiltered = Board(Regular(filteredColumns), Regular(regularColumns));
        BoardMoveSimulation filteredSimulation = Simulate(filtered, new ColumnPosition(1, 0), new ColumnPosition(0, 2), "M8");
        BoardMoveSimulation unfilteredSimulation = Simulate(unfiltered, new ColumnPosition(1, 0), new ColumnPosition(0, 2), "M8");

        if (filteredSimulation.MatchCount != 2
            || !filteredSimulation.Matches.SequenceEqual(unfilteredSimulation.Matches)
            || !filteredSimulation.AffectedShelfIndexes.SequenceEqual(unfilteredSimulation.AffectedShelfIndexes)
            || BoardStateFingerprint.CreateKey(WithoutFilters(filteredSimulation.State)) != BoardStateFingerprint.CreateKey(unfilteredSimulation.State)
            || !filteredSimulation.State.Shelves[0].IsFiltered)
        {
            throw new InvalidOperationException("Filter M8: a match and cascade on a filtered shelf must resolve as on a regular shelf.");
        }
    }

    private static void ValidateAcceptedTypesNormalization()
    {
        ItemType[] expected = { ItemType.Ball, ItemType.Lamp };
        ShelfStateSnapshot shelf = Filter(new[] { ItemType.Lamp, ItemType.Ball, ItemType.Ball }, Column(ItemType.Bear), Column(ItemType.Plant, ItemType.Toy), Column());

        if (!shelf.AcceptedTypes.SequenceEqual(expected)
            || !shelf.WithColumns(new[] { Column(), Column(), Column() }).AcceptedTypes.SequenceEqual(expected)
            || !shelf.ShiftConveyor().AcceptedTypes.SequenceEqual(expected))
        {
            throw new InvalidOperationException("Filter M9: accepted types must be normalized and preserved.");
        }

        BoardStateSnapshot board = Board(
            Regular(Column(ItemType.Bear), Column(ItemType.Bear), Column()),
            Conveyor(Column(ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear, ItemType.Toy), Column(ItemType.Plant)),
            shelf,
            Regular(Column(ItemType.Bear), Column(), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(3, 0), new ColumnPosition(0, 2), "M9");

        if (!simulation.ConveyorsShifted || !simulation.State.Shelves[2].AcceptedTypes.SequenceEqual(expected))
            throw new InvalidOperationException("Filter M9: a conveyor shift must keep the filter of another shelf.");

        ExpectArgumentFailure(() => Filter(new[] { ItemType.Ball, ItemType.Bear, ItemType.Plant, ItemType.Lamp }, Column(), Column(), Column()), "Filter M9: four accepted types passed.");
        ExpectArgumentFailure(() => new ShelfStateSnapshot(new[] { Column(), Column(), Column() }, false, false, BallOnly), "Filter M9: a closed shelf was filtered.");
        ExpectArgumentFailure(() => new ShelfStateSnapshot(new[] { Column(), Column(), Column() }, true, true, BallOnly), "Filter M9: a conveyor shelf was filtered.");
    }

    private static void ValidateFingerprintMarker()
    {
        ColumnStateSnapshot[] columns = { Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant) };
        ColumnStateSnapshot[] otherColumns = { Column(ItemType.Lamp), Column(ItemType.Toy), Column(ItemType.MapBall) };
        BoardStateSnapshot regular = Board(Regular(columns), Regular(otherColumns));
        BoardStateSnapshot filtered = Board(Filter(BallOnly, columns), Regular(otherColumns));
        BoardStateSnapshot otherFilter = Board(Filter(new[] { ItemType.Ball, ItemType.Bear }, columns), Regular(otherColumns));

        if (BoardStateFingerprint.CreateKey(regular) == BoardStateFingerprint.CreateKey(filtered)
            || BoardStateFingerprint.CreateHash(regular) == BoardStateFingerprint.CreateHash(filtered)
            || BoardStateFingerprint.CreateKey(filtered) == BoardStateFingerprint.CreateKey(otherFilter)
            || BoardStateFingerprint.CreateHash(filtered) == BoardStateFingerprint.CreateHash(otherFilter))
        {
            throw new InvalidOperationException("Filter M10: filters must change the key and the hash.");
        }

        if (BoardStateFingerprint.CreateKey(Board(Regular(Column(ItemType.Ball), Column(), Column(ItemType.Bear, ItemType.Lamp)))) != "[()(0,)(1,3,)]")
            throw new InvalidOperationException("Filter M11: the key of a shelf without a filter changed.");
    }

    private static void ValidateCatalogLevelFilters(LevelCatalog catalog)
    {
        foreach (LevelEntry level in catalog.Levels)
        {
            TimedLevelDefinition definition = level.Definition;

            foreach (TimedLevelVariant variant in definition.Variants)
            {
                BoardStateSnapshot layout = variant.CreateLayout();

                if (layout.Shelves.Any(shelf => shelf.IsFiltered) != definition.HasShelfFilters || BoardStateFingerprint.CreateHash(layout) != variant.LayoutHash)
                    throw new InvalidOperationException($"Filter M11: level {level.Number}, seed {variant.Seed} changed its filter state or hash.");
            }

            BoardStateSnapshot boardShape = Board(definition.Variants[0].CreateLayout().Shelves
                .Select(shelf => Regular(shelf.Columns.Select(_ => Column()).ToArray()))
                .ToArray());
            TimedLevelValidator.ValidateForRuntime(definition, boardShape, TimedLevelLayoutRules.GeneratorVersion);
        }
    }

    private static void ValidateShelfBoardFilters()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf closedShelf = TemporaryShelfFactory.CreateShelf("ClosedShelf", temporaryObjects);
            Shelf conveyorShelf = TemporaryShelfFactory.CreateShelf("ConveyorShelf", temporaryObjects);
            Shelf filteredShelf = TemporaryShelfFactory.CreateShelf("FilteredShelf", temporaryObjects);
            Shelf regularShelf = TemporaryShelfFactory.CreateShelf("RegularShelf", temporaryObjects);
            Shelf foreignShelf = TemporaryShelfFactory.CreateShelf("ForeignShelf", temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, closedShelf, conveyorShelf, filteredShelf, regularShelf);
            board.Initialize();
            board.CloseShelf(closedShelf);
            board.MarkConveyor(conveyorShelf);

            ExpectFailure(() => board.SetFilter(closedShelf, BallOnly), "filtered once", "Filter M12: a closed shelf was filtered.");
            ExpectFailure(() => board.SetFilter(conveyorShelf, BallOnly), "filtered once", "Filter M12: a conveyor shelf was filtered.");
            ExpectFailure(() => board.SetFilter(foreignShelf, BallOnly), "filtered once", "Filter M12: a shelf from another board was filtered.");
            ExpectFailure(() => board.SetFilter(filteredShelf, Array.Empty<ItemType>()), "one to", "Filter M12: an empty filter was set.");
            ExpectFailure(() => board.SetFilter(filteredShelf, new[] { ItemType.Ball, ItemType.Bear, ItemType.Plant, ItemType.Lamp }), "one to", "Filter M12: four accepted types were set.");
            board.SetFilter(filteredShelf, new[] { ItemType.Lamp, ItemType.Ball });
            ExpectFailure(() => board.SetFilter(filteredShelf, BallOnly), "filtered once", "Filter M12: a shelf was filtered twice.");
            ExpectFailure(() => board.CloseShelf(filteredShelf), "closed", "Filter M12: a filtered shelf was closed.");
            ExpectFailure(() => board.MarkConveyor(filteredShelf), "conveyor", "Filter M12: a filtered shelf was marked as a conveyor.");

            BoardStateSnapshot snapshot = board.CreateSnapshot();

            if (!board.HasFilters
                || !board.IsFiltered(filteredShelf)
                || board.IsFiltered(regularShelf)
                || !board.GetAcceptedTypes(filteredShelf).SequenceEqual(new[] { ItemType.Ball, ItemType.Lamp })
                || board.GetAcceptedTypes(regularShelf).Count != 0
                || !board.Accepts(filteredShelf, ItemType.Lamp)
                || board.Accepts(filteredShelf, ItemType.Bear)
                || !board.Accepts(regularShelf, ItemType.Bear)
                || !snapshot.Shelves[2].AcceptedTypes.SequenceEqual(new[] { ItemType.Ball, ItemType.Lamp })
                || snapshot.Shelves.Where((shelf, index) => index != 2).Any(shelf => shelf.IsFiltered))
            {
                throw new InvalidOperationException("Filter M12: the board does not report its filtered shelf.");
            }
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateDefinitionErrors(LevelCatalog catalog)
    {
        TimedLevelDefinition template = catalog.Levels.First(level => level.SceneName == FilterSceneName).Definition;
        ItemType levelType = template.ItemGroups[0].Type;
        ItemType missingType = Enum.GetValues(typeof(ItemType)).Cast<ItemType>().FirstOrDefault(type => template.ItemGroups.All(group => group.Type != type));
        BoardStateSnapshot board = Board(Enumerable.Range(0, BoardShelfCount)
            .Select(_ => Regular(Column(), Column(), Column()))
            .ToArray());
        List<TimedLevelDefinition> definitions = new List<TimedLevelDefinition>();

        try
        {
            TimedLevelValidator.ValidateForGeneration(CreateDefinition(template, definitions, definition => SetFilters(definition, 1, 1, (5, new[] { levelType }))), board);

            ExpectDefinitionFailure(template, definitions, board, definition => SetFilters(definition, 0, 0, (5, new[] { levelType }), (5, new[] { levelType })),
                "filtered twice", "Filter M13: a duplicated filter index passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition => SetFilters(definition, 0, 0, (-1, new[] { levelType })),
                "non-negative", "Filter M13: a negative filter index passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition => SetFilters(definition, 0, 0, (BoardShelfCount, new[] { levelType })),
                "out of range", "Filter M13: an out-of-range filter index passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition => SetFilters(definition, 0, 0, (5, Array.Empty<ItemType>())),
                "must accept one to", "Filter M13: a filter without types passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition => SetFilters(definition, 0, 0, (5, new[] { ItemType.Ball, ItemType.Bear, ItemType.Plant, ItemType.Lamp })),
                "must accept one to", "Filter M13: a filter with four types passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition => SetFilters(definition, 0, 0, (5, new[] { levelType, levelType })),
                "twice", "Filter M13: a filter with a repeated type passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition => SetFilters(definition, 1, 1),
                "require shelf filters", "Filter M13: filter usage counts without filters passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition =>
                {
                    SetFilters(definition, 0, 0, (5, new[] { ItemType.Ball, ItemType.Bear }));
                    SetItemGroups(definition, ItemType.Ball, ItemType.Bear);
                },
                "every item type", "Filter M13: a filter accepting every level type passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition =>
                {
                    SetFilters(definition, 0, 0, (5, new[] { levelType }));
                    definition.FindProperty("_closedShelves").arraySize = 1;
                },
                "closed shelves", "Filter M13: filters combined with closed shelves passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition =>
                {
                    SetFilters(definition, definition.FindProperty("_emptyColumnCount").intValue + 1, 0, (5, new[] { levelType }), (6, new[] { levelType }));
                },
                "exceed the empty column reserve", "Filter M13: more filtered empty columns than empty columns passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition =>
                {
                    definition.FindProperty("_emptyColumnCount").intValue = 4;
                    SetFilters(definition, 4, 0, (5, new[] { levelType }));
                },
                "exceed the 3 columns", "Filter M13: more filtered empty columns than filtered columns passed validation.");
            ExpectDefinitionFailure(template, definitions, board, definition =>
                {
                    SetFilters(definition, 0, 0, (9, new[] { levelType }));
                    SetIntegers(definition.FindProperty("_conveyorShelfIndices"), 9);
                    definition.FindProperty("_shuffleSwapCount").intValue = 0;
                },
                "cannot be filtered", "Filter M13: a filter on a conveyor shelf passed validation.");

            if (template.ItemGroups.Count < Enum.GetValues(typeof(ItemType)).Length)
            {
                ExpectDefinitionFailure(template, definitions, board, definition => SetFilters(definition, 0, 0, (5, new[] { missingType })),
                    "not one of the level's item types", "Filter M13: a filter with a foreign type passed validation.");
            }
            else
            {
                ExpectDefinitionFailure(template, definitions, board, definition =>
                    {
                        SetFilters(definition, 0, 0, (5, new[] { levelType }));
                        SetItemGroups(definition, template.ItemGroups.Skip(1).Select(group => group.Type).ToArray());
                    },
                    "not one of the level's item types", "Filter M13: a filter with a foreign type passed validation.");
            }
        }
        finally
        {
            foreach (TimedLevelDefinition definition in definitions)
                UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void ValidateVariantErrors(LevelCatalog catalog)
    {
        TimedLevelDefinition template = catalog.Levels.First(level => level.SceneName == FilterSceneName).Definition;
        BoardStateSnapshot board = Board(Enumerable.Range(0, 4).Select(_ => Regular(Column(), Column(), Column())).ToArray());
        TimedLevelMove[] solution =
        {
            new TimedLevelMove(new ColumnPosition(1, 0), new ColumnPosition(0, 0)),
            new TimedLevelMove(new ColumnPosition(3, 0), new ColumnPosition(1, 0)),
            new TimedLevelMove(new ColumnPosition(3, 1), new ColumnPosition(2, 2))
        };
        List<TimedLevelDefinition> definitions = new List<TimedLevelDefinition>();

        try
        {
            TimedLevelDefinition valid = CreateSmallFilterDefinition(template, definitions, 1, 1, CreateSmallFilterLayout(BallOnly), solution);
            TimedLevelValidator.ValidateForRuntime(valid, board, TimedLevelLayoutRules.GeneratorVersion);
            ReplaySolution(valid.Variants[0], "M14");

            ExpectFailure(
                () => TimedLevelValidator.ValidateForRuntime(CreateSmallFilterDefinition(template, definitions, 1, 1, CreateSmallFilterLayout(new[] { ItemType.Bear }), solution), board, TimedLevelLayoutRules.GeneratorVersion),
                "incorrect filter",
                "Filter M14: a variant with a wrong filter passed validation.");
            ExpectFailure(
                () => TimedLevelValidator.ValidateForRuntime(CreateSmallFilterDefinition(template, definitions, 1, 1, CreateSmallFilterLayout(Array.Empty<ItemType>()), solution), board, TimedLevelLayoutRules.GeneratorVersion),
                "incorrect filter",
                "Filter M14: a variant without its filter passed validation.");
            ExpectFailure(
                () => TimedLevelValidator.ValidateForRuntime(CreateSmallFilterDefinition(template, definitions, 2, 1, CreateSmallFilterLayout(BallOnly), solution), board, TimedLevelLayoutRules.GeneratorVersion),
                "filtered empty columns instead",
                "Filter M14: a variant with too few filtered empty columns passed validation.");
            ExpectFailure(
                () => TimedLevelValidator.ValidateForRuntime(CreateSmallFilterDefinition(template, definitions, 1, 2, CreateSmallFilterLayout(BallOnly), solution), board, TimedLevelLayoutRules.GeneratorVersion),
                "filtered moves instead",
                "Filter M14: a variant with too few filtered moves passed validation.");
        }
        finally
        {
            foreach (TimedLevelDefinition definition in definitions)
                UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void ValidateLockedFilteredShelf()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf lockedShelf = TemporaryShelfFactory.CreateShelf("LockedFilteredShelf", temporaryObjects);
            Shelf filteredShelf = TemporaryShelfFactory.CreateShelf("FilteredShelf", temporaryObjects);
            Shelf regularShelf = TemporaryShelfFactory.CreateShelf("RegularShelf", temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, lockedShelf, filteredShelf, regularShelf);
            board.Initialize();
            Fill(lockedShelf, temporaryObjects, new[] { ItemType.Ball }, new[] { ItemType.Ball }, new[] { ItemType.Ball });
            Fill(filteredShelf, temporaryObjects, Array.Empty<ItemType>(), new[] { ItemType.Bear }, new[] { ItemType.Lamp });
            Fill(regularShelf, temporaryObjects, new[] { ItemType.Plant }, new[] { ItemType.Ball }, Array.Empty<ItemType>());
            board.SetFilter(lockedShelf, BallOnly);
            board.SetFilter(filteredShelf, BallOnly);
            board.LockShelf(lockedShelf);

            if (board.CanMove(regularShelf.Columns[0], filteredShelf.Columns[0]))
                throw new InvalidOperationException("Filter M15: the board allowed a rejected type onto a filtered shelf.");

            if (!board.TrySimulateMove(regularShelf.Columns[1], filteredShelf.Columns[0], out BoardMoveSimulation simulation))
                throw new InvalidOperationException("Filter M15: the board rejected an accepted type.");

            ShelfStateSnapshot lockedState = simulation.State.Shelves[0];

            if (!lockedState.AcceptedTypes.SequenceEqual(BallOnly) || lockedState.Columns.Any(column => !column.IsEmpty))
                throw new InvalidOperationException("Filter M15: a locked filtered shelf lost its filter while resolving its match.");
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateFilteredTypeMatchesOnlyOnItsFilter()
    {
        BoardStateSnapshot regular = Board(
            Filter(BallOnly, Column(), Column(ItemType.Bear), Column(ItemType.Lamp)),
            Regular(Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Regular(Column(ItemType.Ball), Column(ItemType.Plant), Column()));
        BoardMoveSimulation regularSimulation = Simulate(regular, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "M16");

        if (regularSimulation.MatchCount != 0
            || !HasColumns(regularSimulation.State.Shelves[1], new[] { ItemType.Ball }, new[] { ItemType.Ball }, new[] { ItemType.Ball })
            || !regularSimulation.State.Shelves[1].HasMatch()
            || regularSimulation.State.CanMatch(1))
        {
            throw new InvalidOperationException("Filter M16: three filtered items matched on a regular shelf.");
        }

        BoardStateSnapshot filtered = Board(
            Filter(BallOnly, Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            Regular(Column(ItemType.Ball), Column(ItemType.Plant), Column()));
        BoardMoveSimulation filteredSimulation = Simulate(filtered, new ColumnPosition(1, 0), new ColumnPosition(0, 2), "M16");

        if (filteredSimulation.MatchCount != 1 || filteredSimulation.Matches[0].ShelfIndex != 0 || filteredSimulation.State.Shelves[0].Columns.Any(column => !column.IsEmpty))
            throw new InvalidOperationException("Filter M16: three filtered items did not match on their filter.");
    }

    private static void ValidateJunkMatchesAnywhere()
    {
        BoardStateSnapshot regular = Board(
            Filter(BallOnly, Column(), Column(ItemType.Ball), Column()),
            Regular(Column(ItemType.Bear), Column(ItemType.Bear), Column()),
            Regular(Column(ItemType.Bear), Column(ItemType.Plant), Column()));
        BoardMoveSimulation regularSimulation = Simulate(regular, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "M17");

        if (regularSimulation.MatchCount != 1 || regularSimulation.Matches[0].ShelfIndex != 1)
            throw new InvalidOperationException("Filter M17: an unfiltered type did not match on a regular shelf.");

        BoardStateSnapshot filtered = Board(
            Filter(BallOnly, Column(ItemType.Ball, ItemType.Bear), Column(ItemType.Ball, ItemType.Bear), Column(ItemType.Lamp, ItemType.Bear)),
            Regular(Column(ItemType.Ball), Column(ItemType.Plant), Column()));
        BoardMoveSimulation filteredSimulation = Simulate(filtered, new ColumnPosition(1, 0), new ColumnPosition(0, 2), "M17");

        if (filteredSimulation.MatchCount != 2
            || filteredSimulation.Matches.Any(match => match.ShelfIndex != 0)
            || filteredSimulation.Matches[1].Type != ItemType.Bear
            || filteredSimulation.State.Shelves[0].Columns.Any(column => !column.IsEmpty))
        {
            throw new InvalidOperationException("Filter M17: junk left on a filtered shelf did not match there.");
        }
    }

    private static void ValidateCascadeKeepsFilteredTypeOnRegularShelf()
    {
        BoardStateSnapshot board = Board(
            Filter(BallOnly, Column(), Column(ItemType.Bear), Column()),
            Regular(Column(ItemType.Plant, ItemType.Ball), Column(ItemType.Plant, ItemType.Ball), Column(ItemType.Lamp, ItemType.Ball)),
            Regular(Column(ItemType.Plant), Column(), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "M18");

        if (simulation.MatchCount != 1
            || simulation.Matches[0].Type != ItemType.Plant
            || !HasColumns(simulation.State.Shelves[1], new[] { ItemType.Ball }, new[] { ItemType.Ball }, new[] { ItemType.Ball }))
        {
            throw new InvalidOperationException("Filter M18: a cascade matched a filtered type on a regular shelf.");
        }
    }

    private static void ValidateTwoTypeFilterMatches()
    {
        BoardStateSnapshot board = Board(
            Filter(new[] { ItemType.Ball, ItemType.Toy }, Column(ItemType.Toy), Column(ItemType.Toy), Column()),
            Regular(Column(ItemType.Toy), Column(ItemType.Toy), Column()),
            Regular(Column(ItemType.Toy), Column(ItemType.Toy), Column(ItemType.Plant)));
        BoardMoveSimulation regularSimulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "M19");
        BoardMoveSimulation filteredSimulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(0, 2), "M19");

        if (regularSimulation.MatchCount != 0)
            throw new InvalidOperationException("Filter M19: the second type of a filter matched on a regular shelf.");

        if (filteredSimulation.MatchCount != 1 || filteredSimulation.Matches[0].ShelfIndex != 0)
            throw new InvalidOperationException("Filter M19: the second type of a filter did not match on its filter.");
    }

    private static void ValidateShelfBoardMatchesLikeSimulator()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf filteredShelf = TemporaryShelfFactory.CreateShelf("FilteredShelf", temporaryObjects);
            Shelf regularShelf = TemporaryShelfFactory.CreateShelf("RegularShelf", temporaryObjects);
            Shelf otherShelf = TemporaryShelfFactory.CreateShelf("OtherShelf", temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, filteredShelf, regularShelf, otherShelf);
            board.Initialize();
            Fill(filteredShelf, temporaryObjects, new[] { ItemType.Ball }, new[] { ItemType.Ball }, new[] { ItemType.Ball });
            Fill(regularShelf, temporaryObjects, new[] { ItemType.Ball }, new[] { ItemType.Ball }, new[] { ItemType.Ball });
            Fill(otherShelf, temporaryObjects, new[] { ItemType.Plant }, Array.Empty<ItemType>(), Array.Empty<ItemType>());
            board.SetFilter(filteredShelf, BallOnly);

            if (board.CanMatch(regularShelf) || !board.CanMatch(filteredShelf))
                throw new InvalidOperationException("Filter M20: the board does not match filtered types like the simulator.");

            if (board.TryResolveMatch(regularShelf, out _) || regularShelf.Columns.Any(column => column.IsEmpty))
                throw new InvalidOperationException("Filter M20: the board resolved filtered items on a regular shelf.");

            board.LockShelf(filteredShelf);
            board.LockShelf(regularShelf);
            ColumnPosition source = new ColumnPosition(2, 0);
            ColumnPosition target = new ColumnPosition(2, 1);

            if (!board.TrySimulateMove(otherShelf.Columns[0], otherShelf.Columns[1], out BoardMoveSimulation boardSimulation)
                || !new BoardMoveSimulator().TrySimulate(board.CreateSnapshot(), source, target, out BoardMoveSimulation simulation))
            {
                throw new InvalidOperationException("Filter M20: the move was rejected.");
            }

            if (BoardStateFingerprint.CreateKey(boardSimulation.State) != BoardStateFingerprint.CreateKey(simulation.State)
                || !HasColumns(boardSimulation.State.Shelves[1], new[] { ItemType.Ball }, new[] { ItemType.Ball }, new[] { ItemType.Ball })
                || boardSimulation.State.Shelves[0].Columns.Any(column => !column.IsEmpty))
            {
                throw new InvalidOperationException("Filter M20: the board and the simulator resolved locked shelves differently.");
            }
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static BoardStateSnapshot CreateSmallFilterLayout(IReadOnlyList<ItemType> firstShelfTypes)
    {
        return Board(
            new ShelfStateSnapshot(new[] { Column(), Column(ItemType.Ball), Column(ItemType.Ball) }, true, false, firstShelfTypes),
            Regular(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Bear)),
            Regular(Column(ItemType.Plant), Column(ItemType.Plant), Column()),
            Regular(Column(ItemType.Bear), Column(ItemType.Plant), Column()));
    }

    private static TimedLevelDefinition CreateSmallFilterDefinition(
        TimedLevelDefinition template,
        List<TimedLevelDefinition> definitions,
        int filteredEmptyColumnCount,
        int minimumFilteredMoveCount,
        BoardStateSnapshot layout,
        IReadOnlyList<TimedLevelMove> solution)
    {
        TimedLevelDefinition definition = CreateDefinition(template, definitions, serializedDefinition =>
        {
            serializedDefinition.FindProperty("_emptyColumnCount").intValue = 3;
            SetItemGroups(serializedDefinition, ItemType.Ball, ItemType.Bear, ItemType.Plant);
            SetFilters(serializedDefinition, filteredEmptyColumnCount, minimumFilteredMoveCount, (0, BallOnly));
        });
        TimedLevelVariant variant = new TimedLevelVariant(1, TimedLevelLayoutRules.GeneratorVersion, solution.Count, BoardStateFingerprint.CreateHash(layout), layout, solution);
        List<TimedLevelVariant> variants = (List<TimedLevelVariant>)typeof(TimedLevelDefinition)
            .GetField("_variants", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(definition);
        variants.Add(variant);
        return definition;
    }

    private static TimedLevelDefinition CreateDefinition(TimedLevelDefinition template, List<TimedLevelDefinition> definitions, Action<SerializedObject> configure)
    {
        TimedLevelDefinition definition = UnityEngine.Object.Instantiate(template);
        definition.hideFlags = HideFlags.HideAndDontSave;
        definitions.Add(definition);
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("_variants").arraySize = 0;
        serializedDefinition.FindProperty("_closedShelves").arraySize = 0;
        serializedDefinition.FindProperty("_conveyorShelfIndices").arraySize = 0;
        serializedDefinition.FindProperty("_refillSettings").objectReferenceValue = null;
        configure(serializedDefinition);
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void ExpectDefinitionFailure(
        TimedLevelDefinition template,
        List<TimedLevelDefinition> definitions,
        BoardStateSnapshot board,
        Action<SerializedObject> configure,
        string expectedMessagePart,
        string message)
    {
        ExpectFailure(() => TimedLevelValidator.ValidateForGeneration(CreateDefinition(template, definitions, configure), board), expectedMessagePart, message);
    }

    private static void SetFilters(SerializedObject definition, int filteredEmptyColumnCount, int minimumFilteredMoveCount, params (int ShelfIndex, ItemType[] AcceptedTypes)[] filters)
    {
        SerializedProperty filtersProperty = definition.FindProperty("_shelfFilters");
        filtersProperty.arraySize = filters.Length;

        for (int index = 0; index < filters.Length; index++)
        {
            SerializedProperty filterProperty = filtersProperty.GetArrayElementAtIndex(index);
            filterProperty.FindPropertyRelative("_shelfIndex").intValue = filters[index].ShelfIndex;
            SerializedProperty typesProperty = filterProperty.FindPropertyRelative("_acceptedTypes");
            typesProperty.arraySize = filters[index].AcceptedTypes.Length;

            for (int typeIndex = 0; typeIndex < filters[index].AcceptedTypes.Length; typeIndex++)
                typesProperty.GetArrayElementAtIndex(typeIndex).enumValueIndex = (int)filters[index].AcceptedTypes[typeIndex];
        }

        definition.FindProperty("_filteredEmptyColumnCount").intValue = filteredEmptyColumnCount;
        definition.FindProperty("_minimumFilteredMoveCount").intValue = minimumFilteredMoveCount;
    }

    private static void SetItemGroups(SerializedObject definition, params ItemType[] types)
    {
        SerializedProperty groupsProperty = definition.FindProperty("_itemGroups");
        groupsProperty.arraySize = types.Length;

        for (int index = 0; index < types.Length; index++)
        {
            SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(index);
            groupProperty.FindPropertyRelative("_type").enumValueIndex = (int)types[index];
            groupProperty.FindPropertyRelative("_groupCount").intValue = 1;
        }
    }

    private static void SetIntegers(SerializedProperty property, params int[] values)
    {
        property.arraySize = values.Length;

        for (int index = 0; index < values.Length; index++)
            property.GetArrayElementAtIndex(index).intValue = values[index];
    }

    private static void ReplaySolution(TimedLevelVariant variant, string caseName)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = variant.CreateLayout();

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation) || simulation.MatchCount != 1)
                throw new InvalidOperationException($"Filter {caseName}: the scenario solution does not replay.");

            state = simulation.State;
        }

        if (!state.IsCleared)
            throw new InvalidOperationException($"Filter {caseName}: the scenario solution does not clear the board.");
    }

    private static void Fill(Shelf shelf, List<GameObject> temporaryObjects, params ItemType[][] columns)
    {
        for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
        {
            foreach (ItemType type in columns[columnIndex])
                shelf.Columns[columnIndex].Append(TemporaryShelfFactory.CreateItem(type, temporaryObjects));
        }
    }

    private static BoardMoveSimulation Simulate(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target, string caseName)
    {
        if (!new BoardMoveSimulator().TrySimulate(board, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            throw new InvalidOperationException($"Filter {caseName}: the move was rejected.");

        return simulation;
    }

    private static void ExpectRejected(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target, string message)
    {
        if (new BoardMoveSimulator().TrySimulate(board, source, target, out _))
            throw new InvalidOperationException(message);
    }

    private static void ExpectFailure(Action action, string expectedMessagePart, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            if (exception.Message.Contains(expectedMessagePart))
                return;

            throw new InvalidOperationException($"{message} Unexpected failure: {exception.Message}");
        }

        throw new InvalidOperationException(message);
    }

    private static void ExpectArgumentFailure(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static BoardStateSnapshot WithoutFilters(BoardStateSnapshot board)
    {
        return Board(board.Shelves.Select(shelf => new ShelfStateSnapshot(shelf.Columns, shelf.IsOpen, shelf.IsConveyor)).ToArray());
    }

    private static bool HasColumns(ShelfStateSnapshot shelf, params ItemType[][] expectedColumns)
    {
        return shelf.Capacity == expectedColumns.Length
            && shelf.Columns.Select((column, index) => column.Items.SequenceEqual(expectedColumns[index])).All(isEqual => isEqual);
    }

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    private static ShelfStateSnapshot Regular(params ColumnStateSnapshot[] columns) => new ShelfStateSnapshot(columns, true, false);

    private static ShelfStateSnapshot Conveyor(params ColumnStateSnapshot[] columns) => new ShelfStateSnapshot(columns, true, true);

    private static ShelfStateSnapshot Filter(IReadOnlyList<ItemType> acceptedTypes, params ColumnStateSnapshot[] columns) => new ShelfStateSnapshot(columns, true, false, acceptedTypes);

    private static BoardStateSnapshot Board(params ShelfStateSnapshot[] shelves) => new BoardStateSnapshot(shelves);
}
