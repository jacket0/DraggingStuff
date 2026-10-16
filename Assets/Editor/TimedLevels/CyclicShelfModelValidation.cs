using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CyclicShelfModelValidation
{
    private const int ShelfCapacity = 3;
    private const int DefinitionShelfCount = 4;
    private const int DefinitionEmptyColumnCount = 3;
    private static readonly ItemType[] DefinitionTypes = { ItemType.Ball, ItemType.Bear, ItemType.Plant, ItemType.Lamp };

    public static void Run(LevelCatalog catalog)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));

        ValidateCycleSnapshot();
        ValidateTransferKeepsCounter();
        ValidateSingleMatchAdvancesCounter();
        ValidateDoubleSwapMatchAdvancesOnce();
        ValidateCascadeAdvancesOnce();
        ValidateClosingKeepsQueues();
        ValidateClosedReadyRowDoesNotMatch();
        ValidateMatchAfterOpeningDoesNotAdvance();
        ValidateSimultaneousSwitch();
        ValidateCarouselRevolution();
        ValidateMixedWindows();
        ValidateDeadlockWithoutOpenItems();
        ValidateDeadlockWithoutOpenTriple();
        ValidateRepeatedDeadlockStopsCycles();
        ValidateFingerprintCycles();
        ValidateLevelsWithoutCycles(catalog);
        ValidateIntroHintLevels(catalog);
        ValidateMovesWithClosedCellRejected();
        ValidateShelfBoardCycles();
        ValidateShelfBoardRescue();
        ValidateClearedBoardWithClosedCell();
        ValidateDefinitionErrors(catalog);
        ValidateVariantRoundTrip(catalog);
    }

    private static void ValidateCycleSnapshot()
    {
        ExpectFailure<ArgumentOutOfRangeException>(() => new ShelfCycleSnapshot(0, 3, 1), "Cycles C0: an open duration of zero passed.");
        ExpectFailure<ArgumentOutOfRangeException>(() => new ShelfCycleSnapshot(3, 0, 1), "Cycles C0: a closed duration of zero passed.");
        ExpectFailure<ArgumentOutOfRangeException>(() => new ShelfCycleSnapshot(3, 3, 0), "Cycles C0: zero moves to the switch passed.");
        ExpectFailure<ArgumentOutOfRangeException>(() => new ShelfCycleSnapshot(2, 4, 5), "Cycles C0: moves to the switch longer than both durations passed.");
        ExpectFailure<ArgumentOutOfRangeException>(() => new ShelfCycleSnapshot(3, 3, 2).Advance(true, 3, out _), "Cycles C0: a cycle advanced past its switch.");
        ExpectFailure<ArgumentException>(
            () => new ShelfStateSnapshot(new[] { Column(), Column(), Column() }, true, true, Array.Empty<ItemType>(), Cycle(3, 3, 1)),
            "Cycles C0: a conveyor shelf became cyclic.");
        ExpectFailure<ArgumentException>(
            () => new ShelfStateSnapshot(new[] { Column(), Column(), Column() }, true, false, new[] { ItemType.Ball }, Cycle(3, 3, 1)),
            "Cycles C0: a filtered shelf became cyclic.");
        ExpectFailure<InvalidOperationException>(() => OpenShelf(Column(), Column(), Column()).AdvanceCycle(1, out _), "Cycles C0: a regular shelf advanced a cycle.");

        ShelfCycleSnapshot cycle = Cycle(2, 4, 1).Advance(true, out bool switched);
        ShelfCycleSnapshot nextCycle = cycle.Advance(false, out bool nextSwitched);

        if (!switched || cycle.MovesToSwitch != 4 || nextSwitched || nextCycle.MovesToSwitch != 3)
            throw new InvalidOperationException("Cycles C0: a switch must start the duration of the new state.");
    }

    private static void ValidateTransferKeepsCounter()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(true, 3, 3, 1, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Lamp), Column(), Column(ItemType.Toy)));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 0), new ColumnPosition(1, 1), "C1");

        if (simulation.MatchCount != 0
            || simulation.SwitchedShelfIndexes.Count != 0
            || simulation.State.Shelves[0].Cycle.MovesToSwitch != 1
            || !simulation.State.Shelves[0].IsOpen)
        {
            throw new InvalidOperationException("Cycles C1: a transfer without a match must not touch the counter.");
        }
    }

    private static void ValidateSingleMatchAdvancesCounter()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(true, 3, 3, 2, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Toy), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "C2");

        if (simulation.MatchCount != 1
            || simulation.SwitchedShelfIndexes.Count != 0
            || simulation.MatchCountAfterSwitch != 0
            || simulation.State.Shelves[0].Cycle.MovesToSwitch != 1
            || !simulation.State.Shelves[0].IsOpen)
        {
            throw new InvalidOperationException("Cycles C2: a single match must take one move from the counter.");
        }
    }

    private static void ValidateDoubleSwapMatchAdvancesOnce()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(true, 3, 3, 2, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Lamp), Column(ItemType.Toy)),
            OpenShelf(Column(ItemType.Toy), Column(ItemType.Toy), Column(ItemType.Lamp)));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(1, 2), new ColumnPosition(2, 2), "C3");

        if (!simulation.IsSwap || simulation.MatchCount != 2 || simulation.State.Shelves[0].Cycle.MovesToSwitch != 1)
            throw new InvalidOperationException("Cycles C3: a double match by a swap must take one move from the counter.");
    }

    private static void ValidateCascadeAdvancesOnce()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(true, 3, 3, 3, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            OpenShelf(Column(ItemType.Lamp, ItemType.Toy), Column(ItemType.Lamp, ItemType.Toy), Column(ItemType.Bear, ItemType.Toy)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.MapBall), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "C4");

        if (simulation.MatchCount != 2 || simulation.Matches[1].Type != ItemType.Toy || simulation.State.Shelves[0].Cycle.MovesToSwitch != 2)
            throw new InvalidOperationException("Cycles C4: a cascade must take one move from the counter.");
    }

    private static void ValidateClosingKeepsQueues()
    {
        ShelfStateSnapshot cell = CyclicShelf(true, 3, 3, 1, Column(ItemType.Ball, ItemType.Bear), Column(ItemType.Plant), Column(ItemType.MapBall));
        BoardStateSnapshot board = Board(
            cell,
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Toy), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "C5");
        ShelfStateSnapshot closedCell = simulation.State.Shelves[0];

        if (closedCell.IsOpen
            || closedCell.Cycle.MovesToSwitch != 3
            || !simulation.SwitchedShelfIndexes.SequenceEqual(new[] { 0 })
            || !simulation.AffectedShelfIndexes.Contains(0)
            || !closedCell.Columns.Select(column => string.Join(",", column.Items)).SequenceEqual(cell.Columns.Select(column => string.Join(",", column.Items))))
        {
            throw new InvalidOperationException("Cycles C5: a filled cell must close with its queues kept in order.");
        }
    }

    private static void ValidateClosedReadyRowDoesNotMatch()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(false, 3, 3, 2, Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Ball)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Toy), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "C6");

        if (board.CanMatch(0)
            || simulation.MatchCount != 1
            || simulation.State.Shelves[0].IsOpen
            || simulation.State.Shelves[0].Columns.Any(column => column.FrontItem != ItemType.Ball)
            || simulation.State.Shelves[0].Cycle.MovesToSwitch != 1)
        {
            throw new InvalidOperationException("Cycles C6: a ready row in a closed cell must not match.");
        }
    }

    private static void ValidateMatchAfterOpeningDoesNotAdvance()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(false, 3, 3, 1, Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Ball)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Toy), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(2, 0), new ColumnPosition(1, 2), "C7");
        ShelfStateSnapshot openedCell = simulation.State.Shelves[0];

        if (simulation.MatchCount != 2
            || simulation.MatchCountAfterSwitch != 1
            || simulation.Matches[1].ShelfIndex != 0
            || !openedCell.IsOpen
            || openedCell.Cycle.MovesToSwitch != 3
            || openedCell.Columns.Any(column => column.Count > 0))
        {
            throw new InvalidOperationException("Cycles C7: a row revealed by opening must match without taking a move from the counter.");
        }
    }

    private static void ValidateSimultaneousSwitch()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(true, 3, 3, 1, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            CyclicShelf(false, 3, 3, 1, Column(ItemType.Toy), Column(ItemType.MapBall), Column(ItemType.Beauty)),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.TrainToy), Column()));
        BoardMoveSimulation simulation = Simulate(board, new ColumnPosition(3, 0), new ColumnPosition(2, 2), "C8");

        if (!simulation.SwitchedShelfIndexes.SequenceEqual(new[] { 0, 1 })
            || simulation.State.Shelves[0].IsOpen
            || !simulation.State.Shelves[1].IsOpen
            || simulation.State.Shelves[0].Cycle.MovesToSwitch != 3
            || simulation.State.Shelves[1].Cycle.MovesToSwitch != 3)
        {
            throw new InvalidOperationException("Cycles C8: swing cells reaching zero together must switch in one change.");
        }
    }

    private static void ValidateCarouselRevolution()
    {
        BoardStateSnapshot start = Board(
            CyclicShelf(true, 2, 4, 2, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            CyclicShelf(false, 2, 4, 2, Column(ItemType.Toy), Column(ItemType.MapBall), Column(ItemType.Beauty)),
            CyclicShelf(false, 2, 4, 4, Column(ItemType.Lamp), Column(ItemType.TrainToy), Column(ItemType.Ball)),
            OpenShelf(Column(), Column(), Column()));
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        int[] expectedOpenCells = { 0, 0, 1, 1, 2, 2 };
        BoardStateSnapshot board = start;

        foreach (int expectedOpenCell in expectedOpenCells)
        {
            int[] openCells = Enumerable.Range(0, 3).Where(shelfIndex => board.Shelves[shelfIndex].IsOpen).ToArray();

            if (!openCells.SequenceEqual(new[] { expectedOpenCell }))
                throw new InvalidOperationException($"Cycles C9: the carousel opened cells {string.Join(",", openCells)} instead of {expectedOpenCell}.");

            board = simulator.AdvanceCycles(board, out _);
        }

        if (BoardStateFingerprint.CreateKey(board) != BoardStateFingerprint.CreateKey(start))
            throw new InvalidOperationException("Cycles C9: a full carousel revolution must return to the starting state.");
    }

    private static void ValidateMixedWindows()
    {
        BoardStateSnapshot start = Board(
            CyclicShelf(true, 2, 2, 2, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            CyclicShelf(false, 2, 2, 2, Column(ItemType.Toy), Column(ItemType.MapBall), Column(ItemType.Beauty)),
            CyclicShelf(true, 3, 3, 3, Column(ItemType.Lamp), Column(ItemType.TrainToy), Column(ItemType.Ball)),
            CyclicShelf(false, 3, 3, 3, Column(ItemType.Bear), Column(ItemType.Plant), Column(ItemType.Toy)),
            OpenShelf(Column(), Column(), Column()));
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        int[][] expectedSwitches = { new int[0], new[] { 0, 1 }, new[] { 2, 3 }, new[] { 0, 1 }, new int[0], new[] { 0, 1, 2, 3 } };
        BoardStateSnapshot board = start;

        for (int step = 0; step < expectedSwitches.Length * 2; step++)
        {
            board = simulator.AdvanceCycles(board, out IReadOnlyList<int> switchedShelfIndexes);
            int[] expected = expectedSwitches[step % expectedSwitches.Length];

            if (!switchedShelfIndexes.SequenceEqual(expected) || board.Shelves.Take(4).Count(shelf => shelf.IsOpen) != 2)
                throw new InvalidOperationException($"Cycles C18: step {step + 1} switched cells {string.Join(",", switchedShelfIndexes)} instead of {string.Join(",", expected)}.");
        }

        if (BoardStateFingerprint.CreateKey(board) != BoardStateFingerprint.CreateKey(start))
            throw new InvalidOperationException("Cycles C18: pairs with different windows must return to the start after a common period.");

        BoardStateSnapshot stuckBoard = Board(
            CyclicShelf(true, 2, 2, 1, Column(), Column(), Column()),
            CyclicShelf(false, 2, 2, 1, Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Ball)),
            CyclicShelf(true, 3, 3, 3, Column(), Column(), Column()),
            CyclicShelf(false, 3, 3, 3, Column(ItemType.Bear), Column(ItemType.Bear), Column(ItemType.Bear)),
            OpenShelf(Column(), Column(), Column()));
        BoardStateSnapshot rescuedBoard = CycleDeadlockRule.ApplyRescue(stuckBoard, false, out IReadOnlyList<int> rescuedShelfIndexes);

        if (!CycleDeadlockRule.IsStuck(stuckBoard)
            || !rescuedShelfIndexes.SequenceEqual(new[] { 0, 1 })
            || rescuedBoard.Shelves[0].IsOpen
            || !rescuedBoard.Shelves[1].IsOpen
            || rescuedBoard.Shelves[2].Cycle.MovesToSwitch != 2
            || rescuedBoard.Shelves[3].Cycle.MovesToSwitch != 2)
        {
            throw new InvalidOperationException("Cycles C18: a rescue must skip the slower pair by the moves to the nearest switch.");
        }
    }

    private static void ValidateDeadlockWithoutOpenItems()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(false, 3, 3, 2, Column(ItemType.Ball), Column(ItemType.Ball), Column(ItemType.Ball)),
            CyclicShelf(true, 3, 3, 2, Column(), Column(), Column()),
            OpenShelf(Column(), Column(), Column()));

        if (!CycleDeadlockRule.IsStuck(board))
            throw new InvalidOperationException("Cycles C10: a board without open items must be stuck.");

        BoardStateSnapshot rescuedBoard = new BoardMoveSimulator().ApplyCycleRescue(board, false, out IReadOnlyList<MatchInfo> matches);
        CycleDeadlockRule.ApplyRescue(board, false, out IReadOnlyList<int> switchedShelfIndexes);

        if (!switchedShelfIndexes.SequenceEqual(new[] { 0, 1 })
            || !rescuedBoard.Shelves[0].IsOpen
            || rescuedBoard.Shelves[1].IsOpen
            || rescuedBoard.Shelves[0].Cycle.MovesToSwitch != 3
            || matches.Count != 1
            || !rescuedBoard.IsCleared
            || CycleDeadlockRule.IsStuck(rescuedBoard))
        {
            throw new InvalidOperationException("Cycles C10: a rescue must skip to the next switch and play the revealed match.");
        }
    }

    private static void ValidateDeadlockWithoutOpenTriple()
    {
        BoardStateSnapshot stuckBoard = Board(
            CyclicShelf(false, 3, 3, 2, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            CyclicShelf(true, 3, 3, 1, Column(ItemType.Ball), Column(), Column()),
            OpenShelf(Column(ItemType.Ball, ItemType.Bear), Column(ItemType.Bear, ItemType.Plant), Column(ItemType.Plant)));
        BoardStateSnapshot deepTripleBoard = Board(
            CyclicShelf(false, 3, 3, 2, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            CyclicShelf(true, 3, 3, 1, Column(ItemType.Bear), Column(), Column()),
            OpenShelf(Column(ItemType.Plant, ItemType.Ball), Column(ItemType.Bear, ItemType.Ball), Column(ItemType.Plant, ItemType.Ball)));

        if (!CycleDeadlockRule.IsStuck(stuckBoard) || CycleDeadlockRule.IsStuck(deepTripleBoard))
            throw new InvalidOperationException("Cycles C11: a deadlock must count every open item at any depth and require a triple.");

        BoardStateSnapshot rescuedBoard = CycleDeadlockRule.ApplyRescue(stuckBoard, false, out IReadOnlyList<int> switchedShelfIndexes);

        if (!switchedShelfIndexes.SequenceEqual(new[] { 1 })
            || rescuedBoard.Shelves[0].IsOpen
            || rescuedBoard.Shelves[0].Cycle.MovesToSwitch != 1
            || rescuedBoard.Shelves[1].IsOpen
            || rescuedBoard.Shelves[1].Cycle.MovesToSwitch != 3)
        {
            throw new InvalidOperationException("Cycles C11: a rescue must advance every cell by the moves to the nearest switch.");
        }
    }

    private static void ValidateRepeatedDeadlockStopsCycles()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(false, 3, 3, 2, Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant)),
            CyclicShelf(true, 3, 3, 2, Column(ItemType.Lamp), Column(), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(ItemType.Lamp), Column()),
            OpenShelf(Column(ItemType.Ball, ItemType.Bear, ItemType.Plant), Column(ItemType.Ball, ItemType.Bear), Column(ItemType.Plant)));
        BoardStateSnapshot stoppedBoard = CycleDeadlockRule.ApplyRescue(board, true, out IReadOnlyList<int> switchedShelfIndexes);

        if (stoppedBoard.HasCycles
            || stoppedBoard.Shelves.Any(shelf => !shelf.IsOpen)
            || !switchedShelfIndexes.SequenceEqual(new[] { 0 })
            || CycleDeadlockRule.IsStuck(stoppedBoard))
        {
            throw new InvalidOperationException("Cycles C12: a repeated deadlock must open every cell and stop the cycles.");
        }

        BoardMoveSimulation simulation = Simulate(stoppedBoard, new ColumnPosition(1, 0), new ColumnPosition(2, 2), "C12");

        if (simulation.MatchCount != 1 || simulation.SwitchedShelfIndexes.Count != 0 || simulation.State.Shelves.Any(shelf => !shelf.IsOpen || shelf.IsCyclic))
            throw new InvalidOperationException("Cycles C12: stopped cells must stay open after the next match.");
    }

    private static void ValidateFingerprintCycles()
    {
        ColumnStateSnapshot[] cellColumns = { Column(ItemType.Ball), Column(ItemType.Bear), Column(ItemType.Plant) };
        ShelfStateSnapshot otherShelf = OpenShelf(Column(ItemType.Lamp), Column(), Column());
        BoardStateSnapshot[] boards =
        {
            Board(OpenShelf(cellColumns), otherShelf),
            Board(CyclicShelf(true, 3, 3, 1, cellColumns), otherShelf),
            Board(CyclicShelf(true, 3, 3, 2, cellColumns), otherShelf),
            Board(CyclicShelf(false, 3, 3, 2, cellColumns), otherShelf),
            Board(CyclicShelf(true, 2, 4, 2, cellColumns), otherShelf)
        };

        if (boards.Select(BoardStateFingerprint.CreateKey).Distinct().Count() != boards.Length
            || boards.Select(BoardStateFingerprint.CreateHash).Distinct().Count() != boards.Length)
        {
            throw new InvalidOperationException("Cycles C14: the same items with another cycle state must differ in key and hash.");
        }

        BoardStateSnapshot cyclicBoard = Board(CyclicShelf(true, 3, 3, 1, cellColumns), otherShelf);
        BoardStateSnapshot reorderedBoard = Board(otherShelf, CyclicShelf(true, 3, 3, 1, cellColumns));

        if (BoardStateFingerprint.CreateKey(cyclicBoard) == BoardStateFingerprint.CreateKey(reorderedBoard))
            throw new InvalidOperationException("Cycles C14: shelves of a board with cycles must keep their physical order in the search key.");
    }

    private static void ValidateIntroHintLevels(LevelCatalog catalog)
    {
        int[] chapterStarts = catalog.Levels.Where(level => CyclicShelfIntroHint.IsChapterStart(catalog, level)).Select(level => level.Number).ToArray();
        int[] queueStarts = catalog.Levels.Where(level => CyclicShelfIntroHint.IsQueueStart(catalog, level)).Select(level => level.Number).ToArray();
        int[] mixedWindowsStarts = catalog.Levels.Where(level => CyclicShelfIntroHint.IsMixedWindowsStart(catalog, level)).Select(level => level.Number).ToArray();

        if (!chapterStarts.SequenceEqual(new[] { 26 }) || !queueStarts.SequenceEqual(new[] { 27 }) || !mixedWindowsStarts.SequenceEqual(new[] { 29 }))
        {
            throw new InvalidOperationException(
                $"Cycles C17: the intro hint must start on levels 26, 27 and 29, found [{string.Join(",", chapterStarts)}], [{string.Join(",", queueStarts)}] and [{string.Join(",", mixedWindowsStarts)}].");
        }
    }

    private static void ValidateLevelsWithoutCycles(LevelCatalog catalog)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        foreach (LevelEntry level in catalog.Levels.Where(level => !level.Definition.HasCyclicShelves))
        {
            foreach (TimedLevelVariant variant in level.Definition.Variants)
            {
                BoardStateSnapshot state = level.Definition.AddCyclicShelves(variant.CreateLayout());

                if (state.HasCycles || BoardStateFingerprint.CreateHash(state) != variant.LayoutHash)
                    throw new InvalidOperationException($"Cycles C15: level {level.Number}, seed {variant.Seed} changed its layout hash.");

                foreach (TimedLevelMove move in variant.CreateSolution())
                {
                    if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                        || !simulation.IsAllowed
                        || simulation.SwitchedShelfIndexes.Count != 0
                        || simulation.MatchCountAfterSwitch != 0)
                    {
                        throw new InvalidOperationException($"Cycles C15: level {level.Number}, seed {variant.Seed} no longer replays without cycles.");
                    }

                    state = simulation.State;
                }
            }
        }
    }

    private static void ValidateMovesWithClosedCellRejected()
    {
        BoardStateSnapshot board = Board(
            CyclicShelf(false, 3, 3, 2, Column(ItemType.Ball), Column(ItemType.Bear), Column()),
            OpenShelf(Column(ItemType.Lamp), Column(), Column(ItemType.Toy)));
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        if (simulator.TrySimulate(board, new ColumnPosition(0, 0), new ColumnPosition(1, 1), out _)
            || simulator.TrySimulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 2), out _)
            || simulator.TrySimulate(board, new ColumnPosition(1, 0), new ColumnPosition(0, 0), out _)
            || simulator.TrySimulate(board, new ColumnPosition(0, 1), new ColumnPosition(1, 2), out _))
        {
            throw new InvalidOperationException("Cycles C16: a move from, into or a swap with a closed cell must be rejected.");
        }
    }

    private static void ValidateShelfBoardCycles()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf firstCell = CreateFilledShelf("FirstCell", ItemType.Ball, temporaryObjects);
            Shelf secondCell = CreateFilledShelf("SecondCell", ItemType.Bear, temporaryObjects);
            Shelf conveyorShelf = CreateFilledShelf("ConveyorShelf", ItemType.Plant, temporaryObjects);
            Shelf filteredShelf = CreateFilledShelf("FilteredShelf", ItemType.Lamp, temporaryObjects);
            Shelf emptyShelf = TemporaryShelfFactory.CreateShelf("EmptyShelf", temporaryObjects);
            Shelf foreignShelf = CreateFilledShelf("ForeignShelf", ItemType.Toy, temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, firstCell, secondCell, conveyorShelf, filteredShelf, emptyShelf);
            board.Initialize();
            board.MarkConveyor(conveyorShelf);
            board.SetFilter(filteredShelf, new[] { ItemType.Lamp });

            ExpectFailure<InvalidOperationException>(() => board.MarkCyclic(conveyorShelf, Cycle(3, 3, 1), true), "Cycles CB1: a conveyor shelf became cyclic.");
            ExpectFailure<InvalidOperationException>(() => board.MarkCyclic(filteredShelf, Cycle(3, 3, 1), true), "Cycles CB1: a filtered shelf became cyclic.");
            ExpectFailure<InvalidOperationException>(() => board.MarkCyclic(foreignShelf, Cycle(3, 3, 1), true), "Cycles CB1: a shelf from another board became cyclic.");
            ExpectFailure<InvalidOperationException>(() => board.MarkCyclic(firstCell, Cycle(2, 4, 3), true), "Cycles CB1: the first switch came later than the starting state lasts.");
            ExpectFailure<InvalidOperationException>(() => board.SetCyclicShelfOpen(emptyShelf, false), "Cycles CB1: a regular shelf was switched as a cell.");

            board.MarkCyclic(firstCell, Cycle(3, 3, 1), true);
            board.MarkCyclic(secondCell, Cycle(3, 3, 1), false);
            ExpectFailure<InvalidOperationException>(() => board.MarkCyclic(firstCell, Cycle(3, 3, 1), true), "Cycles CB1: a shelf became cyclic twice.");
            ExpectFailure<InvalidOperationException>(() => board.MarkConveyor(firstCell), "Cycles CB1: a cell became a conveyor.");
            ExpectFailure<InvalidOperationException>(() => board.SetFilter(firstCell, new[] { ItemType.Ball }), "Cycles CB1: a cell became filtered.");
            ExpectFailure<InvalidOperationException>(() => board.CloseKeyLockedShelf(firstCell), "Cycles CB1: a cell was locked by a key.");

            BoardStateSnapshot startBoard = board.CreateSnapshot();
            BoardStateSnapshot expectedBoard = new BoardMoveSimulator().AdvanceCycles(startBoard, out IReadOnlyList<int> expectedSwitches);

            if (!board.HasCycles
                || !board.IsCyclic(firstCell)
                || board.IsCyclic(emptyShelf)
                || !board.IsShelfClosed(secondCell)
                || board.CanPickUp(secondCell.Columns[0])
                || startBoard.Shelves[1].IsOpen
                || startBoard.Shelves[1].Cycle.MovesToSwitch != 1)
            {
                throw new InvalidOperationException("Cycles CB2: the board does not report its cells.");
            }

            IReadOnlyList<Shelf> switchedShelves = board.AdvanceCycles();

            if (!switchedShelves.SequenceEqual(new[] { firstCell, secondCell })
                || board.IsCyclePhaseOpen(firstCell)
                || !board.IsCyclePhaseOpen(secondCell)
                || board.IsShelfClosed(firstCell)
                || !board.IsShelfClosed(secondCell))
            {
                throw new InvalidOperationException("Cycles CB2: advancing must switch phases and leave the physical state to the switch animation.");
            }

            ExpectFailure<InvalidOperationException>(() => board.ApplyCycleRescue(false), "Cycles CB2: a rescue ran before the switch finished.");

            board.SetCyclicShelfOpen(firstCell, false);
            board.SetCyclicShelfOpen(secondCell, true);

            if (BoardStateFingerprint.CreateKey(board.CreateSnapshot()) != BoardStateFingerprint.CreateKey(expectedBoard)
                || !expectedSwitches.SequenceEqual(new[] { 0, 1 })
                || firstCell.Columns.Any(column => column.FrontItem == null || column.FrontItem.Type != ItemType.Ball))
            {
                throw new InvalidOperationException("Cycles CB2: the board and the simulator disagree after a switch.");
            }
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateShelfBoardRescue()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf firstCell = CreateFilledShelf("FirstCell", ItemType.Ball, temporaryObjects);
            Shelf secondCell = CreateFilledShelf("SecondCell", ItemType.Bear, temporaryObjects);
            Shelf thirdCell = CreateFilledShelf("ThirdCell", ItemType.Plant, temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, firstCell, secondCell, thirdCell);
            board.Initialize();
            board.MarkCyclic(firstCell, Cycle(2, 4, 2), true);
            board.MarkCyclic(secondCell, Cycle(2, 4, 2), false);
            board.MarkCyclic(thirdCell, Cycle(2, 4, 4), false);
            BoardStateSnapshot expectedBoard = CycleDeadlockRule.ApplyRescue(board.CreateSnapshot(), false, out _);
            IReadOnlyList<Shelf> switchedShelves = board.ApplyCycleRescue(false);

            if (!switchedShelves.SequenceEqual(new[] { firstCell, secondCell }) || board.IsCyclePhaseOpen(firstCell) || !board.IsCyclePhaseOpen(secondCell))
                throw new InvalidOperationException("Cycles CB3: a board rescue must switch the cells nearest to their switch.");

            board.SetCyclicShelfOpen(firstCell, false);
            board.SetCyclicShelfOpen(secondCell, true);

            if (BoardStateFingerprint.CreateKey(board.CreateSnapshot()) != BoardStateFingerprint.CreateKey(expectedBoard))
                throw new InvalidOperationException("Cycles CB3: the board and the deadlock rule disagree after a rescue.");

            switchedShelves = board.ApplyCycleRescue(true);

            if (!switchedShelves.SequenceEqual(new[] { firstCell, thirdCell })
                || board.HasCycles
                || !board.IsCyclic(firstCell)
                || !board.IsCyclePhaseOpen(firstCell)
                || !board.IsCyclePhaseOpen(thirdCell)
                || board.AdvanceCycles().Count != 0)
            {
                throw new InvalidOperationException("Cycles CB3: a repeated rescue must open every cell and stop the cycles.");
            }

            board.SetCyclicShelfOpen(firstCell, true);
            board.SetCyclicShelfOpen(thirdCell, true);

            if (board.CreateSnapshot().Shelves.Any(shelf => !shelf.IsOpen || shelf.IsCyclic))
                throw new InvalidOperationException("Cycles CB3: stopped cells must become regular open shelves.");
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateClearedBoardWithClosedCell()
    {
        List<GameObject> temporaryObjects = new List<GameObject>();

        try
        {
            Shelf cell = TemporaryShelfFactory.CreateShelf("Cell", temporaryObjects);
            Shelf refillShelf = TemporaryShelfFactory.CreateShelf("RefillShelf", temporaryObjects);
            Shelf regularShelf = TemporaryShelfFactory.CreateShelf("RegularShelf", temporaryObjects);
            ShelfBoard board = TemporaryShelfFactory.CreateBoard(temporaryObjects, cell, refillShelf, regularShelf);
            board.Initialize();
            board.MarkCyclic(cell, Cycle(3, 3, 2), false);

            if (!board.IsCleared || !board.CreateSnapshot().IsCleared)
                throw new InvalidOperationException("Cycles C13: an empty closed cell must not block the win.");

            board.CloseShelf(refillShelf);

            if (board.IsCleared)
                throw new InvalidOperationException("Cycles C13: a closed refill shelf must still block the win.");
        }
        finally
        {
            TemporaryShelfFactory.Destroy(temporaryObjects);
        }
    }

    private static void ValidateDefinitionErrors(LevelCatalog catalog)
    {
        TimedLevelDefinition template = catalog.Levels[0].Definition;
        BoardStateSnapshot boardShape = Board(Enumerable.Range(0, DefinitionShelfCount).Select(_ => OpenShelf(Column(), Column(), Column())).ToArray());
        BoardStateSnapshot layout = DefinitionLayout(Column(ItemType.Plant));
        CyclicShelfDefinitionData swingOpen = new CyclicShelfDefinitionData(0, true, 3, 3, 2);
        CyclicShelfDefinitionData swingClosed = new CyclicShelfDefinitionData(1, false, 3, 3, 3);
        CyclicShelfDefinitionData[] noCyclicShelves = Array.Empty<CyclicShelfDefinitionData>();
        int[] noConveyors = Array.Empty<int>();
        List<TimedLevelDefinition> definitions = new List<TimedLevelDefinition>();

        try
        {
            Validate(CreateDefinition(template, new[] { swingOpen, swingClosed }, noConveyors, 0, layout, definitions));
            Validate(CreateDefinition(template, new[] { swingOpen, new CyclicShelfDefinitionData(1, false, 2, 4, 3) }, noConveyors, 0, layout, definitions));

            ExpectDefinitionFailure(new[] { swingOpen, swingOpen }, noConveyors, 0, layout, "a duplicated cyclic shelf index passed validation.");
            ExpectDefinitionFailure(new[] { swingOpen }, noConveyors, 0, layout, "a single cyclic shelf passed validation.");
            ExpectDefinitionFailure(new[] { swingOpen, swingClosed }, new[] { 2 }, 0, layout, "cyclic shelves combined with a conveyor passed validation.");
            ExpectDefinitionFailure(
                new[] { new CyclicShelfDefinitionData(0, true, 2, 4, 3), new CyclicShelfDefinitionData(1, false, 2, 4, 4) },
                noConveyors,
                0,
                layout,
                "a first switch later than the starting state passed validation.");
            ExpectDefinitionFailure(
                new[] { swingOpen, new CyclicShelfDefinitionData(1, true, 3, 3, 3) },
                noConveyors,
                0,
                layout,
                "a cell closed in the variant but starting open passed validation.");
            ExpectDefinitionFailure(
                new[] { swingOpen, new CyclicShelfDefinitionData(7, false, 3, 3, 3) },
                noConveyors,
                0,
                layout,
                "a cyclic shelf outside the board passed validation.");
            ExpectDefinitionFailure(
                new[] { swingOpen, swingClosed, new CyclicShelfDefinitionData(2, false, 3, 3, 3) },
                noConveyors,
                0,
                layout,
                "cyclic shelves leaving one regular shelf passed validation.");
            ExpectDefinitionFailure(noCyclicShelves, noConveyors, 1, layout, "a preparation minimum without cyclic shelves passed validation.");
            ExpectDefinitionFailure(new[] { swingOpen, swingClosed }, noConveyors, 0, DefinitionLayout(Column()), "a closed cell with an empty column passed validation.");
        }
        finally
        {
            foreach (TimedLevelDefinition definition in definitions)
                UnityEngine.Object.DestroyImmediate(definition);
        }

        void Validate(TimedLevelDefinition definition) => TimedLevelValidator.ValidateForRuntime(definition, boardShape, TimedLevelLayoutRules.GeneratorVersion);

        void ExpectDefinitionFailure(
            IReadOnlyList<CyclicShelfDefinitionData> cyclicShelves,
            IReadOnlyList<int> conveyorShelfIndices,
            int minimumPreparationMoves,
            BoardStateSnapshot variantLayout,
            string message)
        {
            ExpectFailure<InvalidOperationException>(
                () => Validate(CreateDefinition(template, cyclicShelves, conveyorShelfIndices, minimumPreparationMoves, variantLayout, definitions)),
                $"Cycles D1: {message}");
        }
    }

    private static void ValidateVariantRoundTrip(LevelCatalog catalog)
    {
        BoardStateSnapshot layout = DefinitionLayout(Column(ItemType.Plant));
        CyclicShelfDefinitionData[] cyclicShelves = { new CyclicShelfDefinitionData(0, true, 3, 3, 2), new CyclicShelfDefinitionData(1, false, 3, 3, 3) };
        List<TimedLevelDefinition> definitions = new List<TimedLevelDefinition>();

        try
        {
            TimedLevelDefinition definition = CreateDefinition(catalog.Levels[0].Definition, cyclicShelves, Array.Empty<int>(), 0, layout, definitions);
            BoardStateSnapshot restored = definition.Variants[0].CreateLayout();
            BoardStateSnapshot playable = definition.AddCyclicShelves(restored);

            if (BoardStateFingerprint.CreateHash(restored) != definition.Variants[0].LayoutHash
                || restored.HasCycles
                || !restored.Shelves[0].IsOpen
                || restored.Shelves[1].IsOpen
                || !playable.Shelves[0].IsCyclic
                || playable.Shelves[0].Cycle.MovesToSwitch != 2
                || playable.Shelves[1].IsOpen
                || playable.Shelves[1].Cycle.MovesToSwitch != 3
                || playable.Shelves[2].IsCyclic
                || BoardStateFingerprint.CreateKey(playable) != BoardStateFingerprint.CreateKey(layout)
                || definition.GetMechanicElementCount(LevelMechanic.CyclicShelves) != 2)
            {
                throw new InvalidOperationException("Cycles D2: a layout with cyclic shelves does not survive the variant round trip.");
            }
        }
        finally
        {
            foreach (TimedLevelDefinition definition in definitions)
                UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static BoardStateSnapshot DefinitionLayout(ColumnStateSnapshot closedCellLastColumn)
    {
        ColumnStateSnapshot regularMiddleColumn = closedCellLastColumn.Count > 0 ? Column(ItemType.Lamp) : Column(ItemType.Lamp, ItemType.Plant);

        return Board(
            CyclicShelf(true, 3, 3, 2, Column(ItemType.Lamp, ItemType.Ball), Column(ItemType.Bear, ItemType.Lamp), Column(ItemType.Plant)),
            CyclicShelf(false, 3, 3, 3, Column(ItemType.Ball), Column(ItemType.Bear), closedCellLastColumn),
            OpenShelf(Column(ItemType.Ball, ItemType.Bear), regularMiddleColumn, Column(ItemType.Plant)),
            OpenShelf(Column(), Column(), Column()));
    }

    private static TimedLevelDefinition CreateDefinition(
        TimedLevelDefinition template,
        IReadOnlyList<CyclicShelfDefinitionData> cyclicShelves,
        IReadOnlyList<int> conveyorShelfIndices,
        int minimumPreparationMoves,
        BoardStateSnapshot layout,
        List<TimedLevelDefinition> definitions)
    {
        TimedLevelDefinition definition = UnityEngine.Object.Instantiate(template);
        definition.hideFlags = HideFlags.HideAndDontSave;
        definitions.Add(definition);
        SerializedObject serializedDefinition = new SerializedObject(definition);
        WriteIntegers(serializedDefinition.FindProperty("_conveyorShelfIndices"), conveyorShelfIndices);
        WriteIntegers(serializedDefinition.FindProperty("_keyLockedShelfIndices"), Array.Empty<int>());
        serializedDefinition.FindProperty("_closedShelves").arraySize = 0;
        serializedDefinition.FindProperty("_refillSettings").objectReferenceValue = null;
        serializedDefinition.FindProperty("_shelfFilters").arraySize = 0;
        serializedDefinition.FindProperty("_filteredEmptyColumnCount").intValue = 0;
        serializedDefinition.FindProperty("_emptyColumnCount").intValue = DefinitionEmptyColumnCount;
        serializedDefinition.FindProperty("_minimumItemsBehindShutter").intValue = 0;
        serializedDefinition.FindProperty("_minimumPreparationMoves").intValue = minimumPreparationMoves;
        CyclicShelfDefinitionData.WriteAll(serializedDefinition.FindProperty("_cyclicShelves"), cyclicShelves);

        SerializedProperty itemGroups = serializedDefinition.FindProperty("_itemGroups");
        itemGroups.arraySize = DefinitionTypes.Length;

        for (int index = 0; index < DefinitionTypes.Length; index++)
        {
            itemGroups.GetArrayElementAtIndex(index).FindPropertyRelative("_type").enumValueIndex = (int)DefinitionTypes[index];
            itemGroups.GetArrayElementAtIndex(index).FindPropertyRelative("_groupCount").intValue = 1;
        }

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        TimedLevelVariantGenerator.WriteVariants(definition, new[] { CreateVariant(layout) });
        return definition;
    }

    private static TimedLevelVariant CreateVariant(BoardStateSnapshot layout)
    {
        BoardStateSnapshot storedLayout = Board(layout.Shelves.Select(shelf => shelf.WithCycle(null, shelf.IsOpen)).ToArray());
        TimedLevelMove[] solution = { new TimedLevelMove(new ColumnPosition(2, 2), new ColumnPosition(3, 0)) };
        return new TimedLevelVariant(1, TimedLevelLayoutRules.GeneratorVersion, solution.Length, BoardStateFingerprint.CreateHash(storedLayout), storedLayout, solution);
    }

    private static void WriteIntegers(SerializedProperty property, IReadOnlyList<int> values)
    {
        property.arraySize = values.Count;

        for (int index = 0; index < values.Count; index++)
            property.GetArrayElementAtIndex(index).intValue = values[index];
    }

    private static Shelf CreateFilledShelf(string name, ItemType type, List<GameObject> temporaryObjects)
    {
        Shelf shelf = TemporaryShelfFactory.CreateShelf(name, temporaryObjects);

        foreach (ShelfColumn column in shelf.Columns)
            column.Append(TemporaryShelfFactory.CreateItem(type, temporaryObjects));

        return shelf;
    }

    private static BoardMoveSimulation Simulate(BoardStateSnapshot board, ColumnPosition source, ColumnPosition target, string caseName)
    {
        if (!new BoardMoveSimulator().TrySimulate(board, source, target, out BoardMoveSimulation simulation) || !simulation.IsAllowed)
            throw new InvalidOperationException($"Cycles {caseName}: the move was rejected.");

        if (board.ItemCount - simulation.MatchCount * ShelfCapacity != simulation.State.ItemCount)
            throw new InvalidOperationException($"Cycles {caseName}: items were created or destroyed.");

        return simulation;
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

    private static ShelfCycleSnapshot Cycle(int openMoves, int closedMoves, int movesToSwitch) => new ShelfCycleSnapshot(openMoves, closedMoves, movesToSwitch);

    private static ColumnStateSnapshot Column(params ItemType[] items) => new ColumnStateSnapshot(items);

    private static ShelfStateSnapshot OpenShelf(params ColumnStateSnapshot[] columns) => new ShelfStateSnapshot(columns, true);

    private static ShelfStateSnapshot CyclicShelf(bool isOpen, int openMoves, int closedMoves, int movesToSwitch, params ColumnStateSnapshot[] columns)
    {
        return new ShelfStateSnapshot(columns, isOpen, false, Array.Empty<ItemType>(), Cycle(openMoves, closedMoves, movesToSwitch));
    }

    private static BoardStateSnapshot Board(params ShelfStateSnapshot[] shelves) => new BoardStateSnapshot(shelves);
}
