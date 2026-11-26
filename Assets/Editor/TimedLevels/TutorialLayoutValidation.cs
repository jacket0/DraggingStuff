using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TutorialLayoutValidation
{
    private const string DefinitionPath = "Assets/Levels/TutorialLevel.asset";
    private const int ExpectedItemCount = 15;
    private const int ExpectedTypeCount = 3;

    private static readonly TutorialStep[] Route =
    {
        new TutorialStep("A", new ColumnPosition(1, 1), new ColumnPosition(0, 2), false),
        new TutorialStep("B", new ColumnPosition(1, 2), new ColumnPosition(0, 2), false),
        new TutorialStep("C", new ColumnPosition(1, 1), new ColumnPosition(2, 1), true),
        new TutorialStep("D", new ColumnPosition(1, 0), new ColumnPosition(2, 0), true),
        new TutorialStep("E", new ColumnPosition(2, 1), new ColumnPosition(1, 1), false)
    };

    public static void Run()
    {
        LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath);

        if (definition == null)
            throw new InvalidOperationException(nameof(LevelDefinition));

        BoardStateSnapshot board = CreateBoard(definition);
        ValidateComposition(board);

        BoardMoveSimulator simulator = new BoardMoveSimulator();

        foreach (TutorialStep step in Route)
        {
            ValidateOnlyStepKindMatches(simulator, board, step);

            if (!simulator.TrySimulate(board, step.Source, step.Target, out BoardMoveSimulation simulation)
                || !simulation.IsAllowed || simulation.IsSwap != step.IsSwap || simulation.MatchCount != 1)
            {
                throw new InvalidOperationException($"Tutorial step {step.Name}: the route move must be allowed and make exactly one match.");
            }

            board = simulation.State;
        }

        if (!board.IsCleared)
            throw new InvalidOperationException("Tutorial: the route must clear the board.");

        Debug.Log("TUTORIAL_LAYOUT_PASS");
    }

    private static BoardStateSnapshot CreateBoard(LevelDefinition definition)
    {
        ShelfStateSnapshot[] shelves = definition.Shelves
            .Select(shelf => new ShelfStateSnapshot(shelf.Columns
                .Select(column => new ColumnStateSnapshot(column.ItemPrefabs.Select(item => item.Type).ToArray()))
                .ToArray()))
            .ToArray();
        return new BoardStateSnapshot(shelves);
    }

    private static void ValidateComposition(BoardStateSnapshot board)
    {
        List<ItemType> items = board.Shelves.SelectMany(shelf => shelf.Columns).SelectMany(column => column.Items).ToList();
        IGrouping<ItemType, ItemType>[] groups = items.GroupBy(type => type).ToArray();

        if (items.Count != ExpectedItemCount || groups.Length != ExpectedTypeCount || groups.Any(group => group.Count() % 3 != 0))
            throw new InvalidOperationException($"Tutorial: expected {ExpectedItemCount} items of {ExpectedTypeCount} types in triples.");
    }

    private static void ValidateOnlyStepKindMatches(BoardMoveSimulator simulator, BoardStateSnapshot board, TutorialStep step)
    {
        bool hasMatchingMove = false;
        bool hasMatchingSwap = false;

        foreach (ColumnPosition source in AllPositions(board))
        {
            foreach (ColumnPosition target in AllPositions(board))
            {
                if (source.Equals(target) || !simulator.TrySimulate(board, source, target, out BoardMoveSimulation simulation)
                    || !simulation.IsAllowed || simulation.MatchCount == 0)
                    continue;

                if (simulation.IsSwap)
                    hasMatchingSwap = true;
                else
                    hasMatchingMove = true;
            }
        }

        bool isTaught = step.IsSwap ? hasMatchingSwap : hasMatchingMove;
        bool hasOtherKind = step.IsSwap ? hasMatchingMove : hasMatchingSwap;

        if (!isTaught || hasOtherKind)
        {
            string kind = step.IsSwap ? "a swap" : "a move to an empty column";
            throw new InvalidOperationException($"Tutorial step {step.Name}: only {kind} may make a match.");
        }
    }

    private static IEnumerable<ColumnPosition> AllPositions(BoardStateSnapshot board)
    {
        for (int shelfIndex = 0; shelfIndex < board.Shelves.Count; shelfIndex++)
        {
            for (int columnIndex = 0; columnIndex < board.Shelves[shelfIndex].Columns.Count; columnIndex++)
                yield return new ColumnPosition(shelfIndex, columnIndex);
        }
    }

    private readonly struct TutorialStep
    {
        public string Name { get; }
        public ColumnPosition Source { get; }
        public ColumnPosition Target { get; }
        public bool IsSwap { get; }

        public TutorialStep(string name, ColumnPosition source, ColumnPosition target, bool isSwap)
        {
            Name = name;
            Source = source;
            Target = target;
            IsSwap = isSwap;
        }
    }
}
