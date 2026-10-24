using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SlimeModelValidation
{
    private const int ShelfCapacity = 3;
    private const int GrowthPeriod = 2;
    private const int DefinitionShelfCount = 4;
    private const int DefinitionEmptyColumnCount = 3;
    private static readonly ItemType[] DefinitionTypes = { ItemType.Ball, ItemType.Bear, ItemType.Plant, ItemType.Lamp };

    public static void Run(LevelCatalog catalog)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));

        ValidateSnapshotErrors();
        ValidateStickyColumnCannotGiveItems();
        ValidateStickyColumnAcceptsItem();
        ValidateMatchCleansOwnShelf();
        ValidateMatchCleansNeighborShelves();
        ValidateTransferKeepsCounter();
        ValidateDoubleMatchGrowsOnce();
        ValidateGrowthAcrossLink();
        ValidateGrowthWithoutTarget();
        ValidateCleanSlimeStopsGrowing();
        ValidateDeadlockRescue();
        ValidateClearedBoardWithSlime();
        ValidateFingerprintSlime();
        ValidateShelfBoardSlime();
        ValidateLevelsWithoutSlime(catalog);
        ValidateDefinitionErrors(catalog);
        ValidateVariantRoundTrip(catalog);
    }

    private static void ValidateSnapshotErrors()
    {
        ExpectFailure<ArgumentOutOfRangeException>(() => Slime(0, 1), "Slime S1: a growth period of zero passed.");
        ExpectFailure<ArgumentOutOfRangeException>(() => Slime(2, 3), "Slime S1: moves to growth longer than the period passed.");
        ExpectFailure<ArgumentException>(() => Slime(2, 2, new[] { Position(0, 0), Position(0, 0) }), "Slime S1: a column became sticky twice.");
        ExpectFailure<ArgumentException>(() => new SlimeColumnLink(Position(0, 0), Position(0, 1)), "Slime S1: a link connected columns of one shelf.");
        ExpectFailure<ArgumentException>(
            () => Slime(2, 2, Array.Empty<ColumnPosition>(), new SlimeColumnLink(Position(0, 0), Position(1, 0)), new SlimeColumnLink(Position(1, 0), Position(0, 0))),
            "Slime S1: two links connected the same columns.");
        ExpectFailure<ArgumentException>(
            () => SlimeBoard(Slime(2, 2, new[] { Position(1, 0) }), OpenShelf(Column(), Column(), Column())),
            "Slime S1: a sticky column outside the board passed.");
        ExpectFailure<ArgumentException>(
            () => SlimeBoard(Slime(2, 2, Array.Empty<ColumnPosition>(), new SlimeColumnLink(Position(0, 0), Position(2, 0))), OpenShelf(Column(), Column(), Column()), OpenShelf(Column(), Column(), Column())),
            "Slime S1: a link outside the board passed.");
        ExpectFailure<ArgumentException>(
            () => SlimeBoard(Slime(2, 2), new ShelfStateSnapshot(new[] { Column(), Column(), Column() }, true, true)),
            "Slime S1: slime shared a board with a conveyor.");
        ExpectFailure<ArgumentException>(
            () => SlimeBoard(Slime(2, 2), new ShelfStateSnapshot(new[] { Column(), Column(), Column() }, false)),
            "Slime S1: slime shared a board with a closed shelf.");
        ExpectFailure<ArgumentException>(
            () => SlimeBoard(Slime(2, 2), new ShelfStateSnapshot(new[] { Column(), Column(), Column() }, true, false, new[] { ItemType.Ball })),
            "Slime S1: slime shared a board with a filter.");
    }

    private static void ValidateStickyColumnCannotGiveItems()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, GrowthPeriod, new[] { Position(0, 0) }),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(), Column(ItemType.Lamp), Column(ItemType.Toy)));

        ExpectRejected(board, Position(0, 0), Position(1, 0), "Slime S2: an item was taken from a sticky column.");
        ExpectRejected(board, Position(0, 0), Position(1, 1), "Slime S2: a sticky column was the source of a swap.");
        ExpectRejected(board, Position(1, 1), Position(0, 0), "Slime S2: a sticky column was the target of a swap.");
        ExpectRejected(board, Position(0, 1), Position(0, 0), "Slime S2: a sticky column swapped with its own shelf.");
        Simulate(board, Position(0, 1), Position(1, 1), "S2");
    }

    private static void ValidateStickyColumnAcceptsItem()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, GrowthPeriod, new[] { Position(0, 0) }),
            OpenShelf(Column(), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Lamp), Column()));
        BoardMoveSimulation simulation = Simulate(board, Position(1, 0), Position(0, 0), "S3");

        if (simulation.MatchCount != 0
            || !simulation.State.IsSticky(Position(0, 0))
            || simulation.State.Shelves[0].Columns[0].FrontItem != ItemType.Ball
            || simulation.State.Slime.MovesToGrowth != GrowthPeriod
            || simulation.CleanedColumns.Count != 0
            || simulation.GrownColumn.HasValue)
        {
            throw new InvalidOperationException("Slime S3: an item placed into a sticky column must stick without moving the growth counter.");
        }

        ExpectRejected(simulation.State, Position(0, 0), Position(1, 0), "Slime S3: a stuck item was taken back.");
    }

    private static void ValidateMatchCleansOwnShelf()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(0, 0), Position(0, 1) }),
            OpenShelf(Column(), Column(ItemType.Ball, ItemType.Plant), Column(ItemType.Ball)),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Lamp), Column()));
        BoardMoveSimulation simulation = Simulate(board, Position(1, 0), Position(0, 0), "S4");

        if (simulation.MatchCount != 1
            || simulation.State.Slime.HasStickyColumns
            || !simulation.CleanedColumns.SequenceEqual(new[] { Position(0, 0), Position(0, 1) })
            || simulation.GrownColumn.HasValue
            || simulation.State.Shelves[0].Columns[1].FrontItem != ItemType.Plant)
        {
            throw new InvalidOperationException("Slime S4: a match on a sticky shelf must clean its own columns without growth.");
        }
    }

    private static void ValidateMatchCleansNeighborShelves()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(1, 0), Position(2, 0) }, new SlimeColumnLink(Position(0, 0), Position(1, 0))),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            OpenShelf(Column(ItemType.Plant), Column(ItemType.Bear), Column(ItemType.Toy)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Bear), Column(ItemType.Toy)),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Lamp), Column()));
        BoardMoveSimulation simulation = Simulate(board, Position(3, 0), Position(0, 2), "S5");

        if (simulation.MatchCount != 1
            || !simulation.CleanedColumns.SequenceEqual(new[] { Position(1, 0) })
            || simulation.State.IsSticky(Position(1, 0))
            || !simulation.State.IsSticky(Position(2, 0))
            || simulation.State.Slime.MovesToGrowth != GrowthPeriod
            || simulation.GrownColumn.HasValue)
        {
            throw new InvalidOperationException("Slime S5: a match must clean only linked neighbor shelves and reset the growth counter.");
        }
    }

    private static void ValidateTransferKeepsCounter()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(0, 0) }),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(), Column(ItemType.Lamp), Column(ItemType.Toy)));
        BoardMoveSimulation simulation = Simulate(board, Position(1, 1), Position(1, 0), "S6");

        if (simulation.State.Slime.MovesToGrowth != 1 || simulation.GrownColumn.HasValue || simulation.State.Slime.StickyColumns.Count != 1)
            throw new InvalidOperationException("Slime S6: a move without a match must not touch the growth counter.");
    }

    private static void ValidateDoubleMatchGrowsOnce()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(2, 0) }),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Bear)),
            OpenShelf(Column(ItemType.Bear), Column(ItemType.Bear), Column(ItemType.Ball)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Toy, ItemType.Plant), Column(ItemType.Plant)));
        BoardMoveSimulation simulation = Simulate(board, Position(0, 2), Position(1, 2), "S7");

        if (simulation.MatchCount != 2
            || !simulation.GrownColumn.Equals(Position(2, 1))
            || !simulation.State.Slime.StickyColumns.SequenceEqual(new[] { Position(2, 0), Position(2, 1) })
            || simulation.State.Slime.MovesToGrowth != GrowthPeriod
            || !simulation.State.Shelves[2].Columns[1].Items.SequenceEqual(new[] { ItemType.Toy, ItemType.Plant }))
        {
            throw new InvalidOperationException("Slime S7: a double match must grow slime once onto the neighbor column and keep its items.");
        }

        BoardMoveSimulation nextSimulation = Simulate(Board(board, Slime(GrowthPeriod, GrowthPeriod, new[] { Position(2, 0) })), Position(0, 2), Position(1, 2), "S7");

        if (nextSimulation.GrownColumn.HasValue || nextSimulation.State.Slime.MovesToGrowth != 1)
            throw new InvalidOperationException("Slime S7: the first matching move of a period must only count down.");
    }

    private static void ValidateGrowthAcrossLink()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(2, 0), Position(2, 1), Position(2, 2) }, new SlimeColumnLink(Position(2, 2), Position(3, 0))),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Lamp), Column(ItemType.Toy)),
            OpenShelf(Column(ItemType.Bear), Column(ItemType.Plant), Column(ItemType.Lamp)),
            OpenShelf(Column(ItemType.Toy), Column(ItemType.Plant), Column(ItemType.Bear)));

        if (!SlimeRule.FindGrowthTarget(board).Equals(Position(3, 0)))
            throw new InvalidOperationException("Slime S8: the growth forecast must cross the link to the neighbor shelf.");

        BoardMoveSimulation simulation = Simulate(board, Position(1, 0), Position(0, 2), "S8");

        if (simulation.CleanedColumns.Count != 0 || !simulation.GrownColumn.Equals(Position(3, 0)) || !simulation.State.IsSticky(Position(3, 0)))
            throw new InvalidOperationException("Slime S8: slime must grow across the link to the neighbor shelf.");
    }

    private static void ValidateGrowthWithoutTarget()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(2, 0), Position(2, 1), Position(2, 2) }),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Lamp), Column(ItemType.Toy)),
            OpenShelf(Column(ItemType.Bear), Column(ItemType.Plant), Column(ItemType.Lamp)));
        BoardMoveSimulation simulation = Simulate(board, Position(1, 0), Position(0, 2), "S9");

        if (simulation.GrownColumn.HasValue || simulation.State.Slime.StickyColumns.Count != 3 || simulation.State.Slime.MovesToGrowth != GrowthPeriod)
            throw new InvalidOperationException("Slime S9: growth without a target must be skipped and restart the counter.");
    }

    private static void ValidateCleanSlimeStopsGrowing()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Ball), Column()),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Lamp), Column(ItemType.Toy)));
        BoardMoveSimulation simulation = Simulate(board, Position(1, 0), Position(0, 2), "S10");

        if (simulation.GrownColumn.HasValue || simulation.State.Slime.HasStickyColumns || SlimeRule.FindGrowthTarget(simulation.State).HasValue)
            throw new InvalidOperationException("Slime S10: fully cleaned slime must stop growing.");
    }

    private static void ValidateDeadlockRescue()
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(0, 0), Position(0, 1), Position(0, 2), Position(1, 0), Position(1, 1) }),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Toy), Column(ItemType.Bear)));

        if (!SlimeRule.IsStuck(board, simulator))
            throw new InvalidOperationException("Slime S11: a board without legal moves must be stuck.");

        BoardStateSnapshot rescuedBoard = SlimeRule.ApplyRescue(board, out ColumnPosition cleanedColumn);

        if (!cleanedColumn.Equals(Position(0, 0))
            || rescuedBoard.IsSticky(Position(0, 0))
            || rescuedBoard.Slime.StickyColumns.Count != 4
            || rescuedBoard.Slime.MovesToGrowth != GrowthPeriod
            || SlimeRule.IsStuck(rescuedBoard, simulator))
        {
            throw new InvalidOperationException("Slime S11: a rescue must clean the first filled sticky column and free a move.");
        }

        BoardStateSnapshot movableBoard = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(0, 0) }),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Toy), Column(ItemType.Bear)));

        if (SlimeRule.IsStuck(movableBoard, simulator))
            throw new InvalidOperationException("Slime S11: a board with a legal swap was reported as stuck.");
    }

    private static void ValidateClearedBoardWithSlime()
    {
        BoardStateSnapshot board = SlimeBoard(
            Slime(GrowthPeriod, 1, new[] { Position(0, 0), Position(1, 2) }),
            OpenShelf(Column(), Column(), Column()),
            OpenShelf(Column(), Column(), Column()));

        if (!board.IsCleared || SlimeRule.IsStuck(board, new BoardMoveSimulator()))
            throw new InvalidOperationException("Slime S12: a board without items must count as cleared even with slime left.");
    }

    private static void ValidateFingerprintSlime()
    {
        ShelfStateSnapshot[] shelves =
        {
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column()),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column())
        };
        BoardStateSnapshot firstColumnSticky = SlimeBoard(Slime(GrowthPeriod, GrowthPeriod, new[] { Position(0, 0) }), shelves);
        BoardStateSnapshot secondColumnSticky = SlimeBoard(Slime(GrowthPeriod, GrowthPeriod, new[] { Position(0, 1) }), shelves);
        BoardStateSnapshot otherShelfSticky = SlimeBoard(Slime(GrowthPeriod, GrowthPeriod, new[] { Position(1, 0) }), shelves);
        BoardStateSnapshot otherCounter = SlimeBoard(Slime(GrowthPeriod, 1, new[] { Position(0, 0) }), shelves);
        BoardStateSnapshot swappedColumns = SlimeBoard(
            Slime(GrowthPeriod, GrowthPeriod, new[] { Position(0, 0) }),
            OpenShelf(Column(ItemType.Bear), Column(ItemType.Ball), Column()),
            shelves[1]);
        string[] keys = new[] { firstColumnSticky, secondColumnSticky, otherShelfSticky, otherCounter, swappedColumns }.Select(BoardStateFingerprint.CreateKey).ToArray();

        if (keys.Distinct().Count() != keys.Length)
            throw new InvalidOperationException("Slime S13: the search key must keep sticky columns, their order and the growth counter.");

        if (BoardStateFingerprint.CreateHash(firstColumnSticky) == BoardStateFingerprint.CreateHash(secondColumnSticky)
            || BoardStateFingerprint.CreateHash(new BoardStateSnapshot(shelves)) != BoardStateFingerprint.CreateHash(SlimeBoard(Slime(GrowthPeriod, GrowthPeriod), shelves)))
        {
            throw new InvalidOperationException("Slime S13: the layout hash must mark only sticky columns.");
        }
    }

    private static void ValidateShelfBoardSlime()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf firstShelf = CreateShelf("FirstShelf", temporaryObjects, ItemType.Ball, ItemType.Bear, ItemType.Plant);
            Shelf secondShelf = CreateShelf("SecondShelf", temporaryObjects, ItemType.Lamp, ItemType.Toy);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, firstShelf, secondShelf);
            board.Initialize();
            SlimeSnapshot slime = Slime(GrowthPeriod, GrowthPeriod, new[] { Position(0, 0) }, new SlimeColumnLink(Position(0, 2), Position(1, 0)));

            ExpectFailure<ArgumentException>(() => board.MarkSlime(Slime(GrowthPeriod, GrowthPeriod, new[] { Position(2, 0) })), "Slime SB1: a sticky column outside the board was accepted.");
            board.MarkSlime(slime);
            ExpectFailure<InvalidOperationException>(() => board.MarkSlime(slime), "Slime SB1: slime was added twice.");
            ExpectFailure<InvalidOperationException>(() => board.MarkConveyor(secondShelf), "Slime SB1: a conveyor shared a board with slime.");
            ExpectFailure<InvalidOperationException>(() => board.SetFilter(secondShelf, new[] { ItemType.Lamp }), "Slime SB1: a filter shared a board with slime.");
            ExpectFailure<InvalidOperationException>(() => board.MarkCyclic(secondShelf, new ShelfCycleSnapshot(3, 3, 1), true), "Slime SB1: a cycle shared a board with slime.");
            ExpectFailure<InvalidOperationException>(() => board.SetSlime(Slime(GrowthPeriod, 1, new[] { Position(0, 1) })), "Slime SB1: slime changed its links.");

            BoardStateSnapshot snapshot = board.CreateSnapshot();

            if (!board.HasSlime
                || !board.IsSticky(firstShelf.Columns[0])
                || board.IsSticky(firstShelf.Columns[1])
                || board.CanPickUp(firstShelf.Columns[0])
                || !board.CanPickUp(firstShelf.Columns[1])
                || board.CanMove(firstShelf.Columns[0], secondShelf.Columns[2])
                || board.CanMove(secondShelf.Columns[0], firstShelf.Columns[0])
                || !board.CanMove(firstShelf.Columns[1], secondShelf.Columns[2])
                || !snapshot.IsSticky(Position(0, 0))
                || board.GetColumn(Position(1, 2)) != secondShelf.Columns[2])
            {
                throw new InvalidOperationException("Slime SB2: the board must report sticky columns and forbid taking from them.");
            }

            new BoardMoveSimulator().TrySimulate(snapshot, Position(0, 1), Position(1, 2), out BoardMoveSimulation simulation);
            board.TryMove(firstShelf.Columns[1], secondShelf.Columns[2]);
            board.SetSlime(simulation.State.Slime.WithStickyColumns(new[] { Position(0, 0), Position(0, 1) }, 1));
            BoardStateSnapshot expectedBoard = simulation.State.WithSlime(simulation.State.Slime.WithStickyColumns(new[] { Position(0, 0), Position(0, 1) }, 1));

            if (BoardStateFingerprint.CreateKey(board.CreateSnapshot()) != BoardStateFingerprint.CreateKey(expectedBoard) || board.CanPickUp(firstShelf.Columns[1]))
                throw new InvalidOperationException("Slime SB3: the board and the simulator must agree after a move and a slime change.");
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateLevelsWithoutSlime(LevelCatalog catalog)
    {
        foreach (LevelEntry level in catalog.Levels)
        {
            if (level.Definition != null && level.Definition.Variants.Any(variant => variant.CreateLayout().HasSlime))
                throw new InvalidOperationException($"Slime S14: level {level.Number} stores slime in its layout.");
        }
    }

    private static void ValidateDefinitionErrors(LevelCatalog catalog)
    {
        TimedLevelDefinition template = catalog.Levels[0].Definition;
        BoardStateSnapshot boardShape = new BoardStateSnapshot(Enumerable.Range(0, DefinitionShelfCount).Select(_ => OpenShelf(Column(), Column(), Column())).ToArray());
        ColumnPosition[] oneSticky = { Position(0, 0) };
        ColumnPosition[][] validLinks = { new[] { Position(0, 2), Position(1, 0) } };
        List<UnityEngine.Object> temporaryAssets = new List<UnityEngine.Object>();

        try
        {
            Validate(CreateDefinition(template, oneSticky, validLinks, GrowthPeriod, Array.Empty<int>(), true, temporaryAssets));

            ExpectDefinitionFailure(new[] { Position(0, 0), Position(0, 0) }, validLinks, Array.Empty<int>(), true, "a duplicated sticky column passed validation.");
            ExpectDefinitionFailure(
                new[] { Position(0, 0), Position(0, 1), Position(0, 2), Position(1, 0), Position(1, 1) },
                validLinks,
                Array.Empty<int>(),
                true,
                "five sticky columns passed validation.");
            ExpectDefinitionFailure(new[] { Position(7, 0) }, validLinks, Array.Empty<int>(), true, "a sticky column outside the board passed validation.");
            ExpectDefinitionFailure(oneSticky, new[] { new[] { Position(0, 2), Position(9, 0) } }, Array.Empty<int>(), true, "a slime link outside the board passed validation.");
            ExpectDefinitionFailure(oneSticky, validLinks, Array.Empty<int>(), false, "slime without a neighborhood passed validation.");
            ExpectDefinitionFailure(oneSticky, validLinks, new[] { 2 }, true, "slime combined with a conveyor passed validation.");
        }
        finally
        {
            foreach (UnityEngine.Object temporaryAsset in temporaryAssets)
                UnityEngine.Object.DestroyImmediate(temporaryAsset);
        }

        void Validate(TimedLevelDefinition definition) => TimedLevelValidator.ValidateForRuntime(definition, boardShape, TimedLevelLayoutRules.GeneratorVersion);

        void ExpectDefinitionFailure(
            IReadOnlyList<ColumnPosition> stickyColumns,
            IReadOnlyList<ColumnPosition[]> links,
            IReadOnlyList<int> conveyorShelfIndices,
            bool hasNeighborhood,
            string message)
        {
            ExpectFailure<InvalidOperationException>(
                () => Validate(CreateDefinition(template, stickyColumns, links, GrowthPeriod, conveyorShelfIndices, hasNeighborhood, temporaryAssets)),
                $"Slime D1: {message}");
        }
    }

    private static void ValidateVariantRoundTrip(LevelCatalog catalog)
    {
        List<UnityEngine.Object> temporaryAssets = new List<UnityEngine.Object>();

        try
        {
            TimedLevelDefinition definition = CreateDefinition(
                catalog.Levels[0].Definition,
                new[] { Position(0, 0), Position(2, 1) },
                new[] { new[] { Position(0, 2), Position(1, 0) } },
                3,
                Array.Empty<int>(),
                true,
                temporaryAssets);
            BoardStateSnapshot restored = definition.Variants[0].CreateLayout();
            BoardStateSnapshot playable = definition.AddSlime(restored);

            if (BoardStateFingerprint.CreateHash(restored) != definition.Variants[0].LayoutHash
                || restored.HasSlime
                || !definition.HasSlime
                || !playable.Slime.StickyColumns.SequenceEqual(new[] { Position(0, 0), Position(2, 1) })
                || !playable.Slime.Links.SequenceEqual(new[] { new SlimeColumnLink(Position(0, 2), Position(1, 0)) })
                || playable.Slime.GrowthPeriod != 3
                || playable.Slime.MovesToGrowth != 3
                || BoardStateFingerprint.CreateKey(playable.WithSlime(null)) != BoardStateFingerprint.CreateKey(DefinitionLayout())
                || definition.GetMechanicElementCount(LevelMechanic.Slime) != 2
                || catalog.Levels[0].Definition.HasSlime)
            {
                throw new InvalidOperationException("Slime D2: slime from the definition does not survive the variant round trip.");
            }
        }
        finally
        {
            foreach (UnityEngine.Object temporaryAsset in temporaryAssets)
                UnityEngine.Object.DestroyImmediate(temporaryAsset);
        }
    }

    private static TimedLevelDefinition CreateDefinition(
        TimedLevelDefinition template,
        IReadOnlyList<ColumnPosition> stickyColumns,
        IReadOnlyList<ColumnPosition[]> links,
        int growthPeriod,
        IReadOnlyList<int> conveyorShelfIndices,
        bool hasNeighborhood,
        List<UnityEngine.Object> temporaryAssets)
    {
        TimedLevelDefinition definition = UnityEngine.Object.Instantiate(template);
        definition.hideFlags = HideFlags.HideAndDontSave;
        temporaryAssets.Add(definition);
        SerializedObject serializedDefinition = new SerializedObject(definition);
        WriteIntegers(serializedDefinition.FindProperty("_conveyorShelfIndices"), conveyorShelfIndices);
        WriteIntegers(serializedDefinition.FindProperty("_keyLockedShelfIndices"), Array.Empty<int>());
        serializedDefinition.FindProperty("_closedShelves").arraySize = 0;
        serializedDefinition.FindProperty("_refillSettings").objectReferenceValue = null;
        serializedDefinition.FindProperty("_shelfFilters").arraySize = 0;
        serializedDefinition.FindProperty("_filteredEmptyColumnCount").intValue = 0;
        serializedDefinition.FindProperty("_cyclicShelves").arraySize = 0;
        serializedDefinition.FindProperty("_minimumItemsBehindShutter").intValue = 0;
        serializedDefinition.FindProperty("_minimumPreparationMoves").intValue = 0;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = DefinitionEmptyColumnCount;

        SerializedProperty itemGroups = serializedDefinition.FindProperty("_itemGroups");
        itemGroups.arraySize = DefinitionTypes.Length;

        for (int index = 0; index < DefinitionTypes.Length; index++)
        {
            itemGroups.GetArrayElementAtIndex(index).FindPropertyRelative("_type").enumValueIndex = (int)DefinitionTypes[index];
            itemGroups.GetArrayElementAtIndex(index).FindPropertyRelative("_groupCount").intValue = 1;
        }

        SerializedProperty slime = serializedDefinition.FindProperty("_slime");
        slime.FindPropertyRelative("_neighborhood").objectReferenceValue = hasNeighborhood ? CreateNeighborhood(links, temporaryAssets) : null;
        slime.FindPropertyRelative("_growthPeriod").intValue = growthPeriod;
        SerializedProperty stickyProperty = slime.FindPropertyRelative("_stickyColumns");
        stickyProperty.arraySize = stickyColumns.Count;

        for (int index = 0; index < stickyColumns.Count; index++)
            WriteColumn(stickyProperty.GetArrayElementAtIndex(index), stickyColumns[index]);

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        TimedLevelVariantGenerator.WriteVariants(definition, new[] { CreateVariant(DefinitionLayout()) });
        return definition;
    }

    private static SlimeNeighborhood CreateNeighborhood(IReadOnlyList<ColumnPosition[]> links, List<UnityEngine.Object> temporaryAssets)
    {
        SlimeNeighborhood neighborhood = ScriptableObject.CreateInstance<SlimeNeighborhood>();
        neighborhood.hideFlags = HideFlags.HideAndDontSave;
        temporaryAssets.Add(neighborhood);
        SerializedObject serializedNeighborhood = new SerializedObject(neighborhood);
        SerializedProperty linksProperty = serializedNeighborhood.FindProperty("_links");
        linksProperty.arraySize = links.Count;

        for (int index = 0; index < links.Count; index++)
        {
            WriteColumn(linksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_first"), links[index][0]);
            WriteColumn(linksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_second"), links[index][1]);
        }

        serializedNeighborhood.ApplyModifiedPropertiesWithoutUndo();
        return neighborhood;
    }

    private static void WriteColumn(SerializedProperty property, ColumnPosition position)
    {
        property.FindPropertyRelative("_shelfIndex").intValue = position.ShelfIndex;
        property.FindPropertyRelative("_columnIndex").intValue = position.ColumnIndex;
    }

    private static void WriteIntegers(SerializedProperty property, IReadOnlyList<int> values)
    {
        property.arraySize = values.Count;

        for (int index = 0; index < values.Count; index++)
            property.GetArrayElementAtIndex(index).intValue = values[index];
    }

    private static TimedLevelVariant CreateVariant(BoardStateSnapshot layout)
    {
        TimedLevelMove[] solution = { new TimedLevelMove(Position(2, 2), Position(3, 0)) };
        return new TimedLevelVariant(1, TimedLevelLayoutRules.GeneratorVersion, solution.Length, BoardStateFingerprint.CreateHash(layout), layout, solution);
    }

    private static BoardStateSnapshot DefinitionLayout()
    {
        return new BoardStateSnapshot(new[]
        {
            OpenShelf(Column(ItemType.Lamp, ItemType.Ball), Column(ItemType.Bear, ItemType.Lamp), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Ball, ItemType.Bear), Column(ItemType.Lamp), Column(ItemType.Plant)),
            OpenShelf(Column(), Column(), Column())
        });
    }

    private static Shelf CreateShelf(string name, List<GameObject> temporaryObjects, params ItemType[] frontItems)
    {
        Shelf shelf = TemporaryShelfFactory.CreateShelf(name, temporaryObjects);

        for (int index = 0; index < frontItems.Length; index++)
            shelf.Columns[index].Append(TemporaryShelfFactory.CreateItem(frontItems[index], temporaryObjects));

        return shelf;
    }

    private static BoardMoveSimulation Simulate(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target, string caseName)
    {
        if (!new BoardMoveSimulator().TrySimulate(board, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            throw new InvalidOperationException($"Slime {caseName}: the move was rejected.");

        if (board.ItemCount - simulation.MatchCount * ShelfCapacity != simulation.State.ItemCount)
            throw new InvalidOperationException($"Slime {caseName}: items were created or destroyed.");

        if (!simulation.State.HasSlime || !simulation.State.Slime.Links.SequenceEqual(board.Slime.Links))
            throw new InvalidOperationException($"Slime {caseName}: the move lost the slime links.");

        return simulation;
    }

    private static void ExpectRejected(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target, string message)
    {
        if (new BoardMoveSimulator().TrySimulate(board, source, target, out _))
            throw new InvalidOperationException(message);
    }

    private static void ExpectFailure<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static ColumnPosition Position(int shelfIndex, int columnIndex) => new ColumnPosition(shelfIndex, columnIndex);

    private static SlimeSnapshot Slime(int growthPeriod, int movesToGrowth, IEnumerable<ColumnPosition> stickyColumns = null, params SlimeColumnLink[] links)
    {
        return new SlimeSnapshot(stickyColumns ?? Array.Empty<ColumnPosition>(), links, growthPeriod, movesToGrowth);
    }

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    private static ShelfStateSnapshot OpenShelf(params ColumnStateSnapshot[] columns) => new ShelfStateSnapshot(columns, true);

    private static BoardStateSnapshot SlimeBoard(SlimeSnapshot slime, params ShelfStateSnapshot[] shelves) => new BoardStateSnapshot(shelves, Array.Empty<ItemType>(), slime);

    private static BoardStateSnapshot Board(BoardStateSnapshot board, SlimeSnapshot slime) => board.WithSlime(slime);
}
