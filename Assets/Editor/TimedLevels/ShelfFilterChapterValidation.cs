using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ShelfFilterChapterValidation
{
    private const int ChapterLevelCount = 5;
    private const int RequiredVariantCount = 12;

    [MenuItem("Tools/Timed Levels/Validate Filter Chapter")]
    public static void Validate()
    {
        BoardStateSnapshot shape = ShelfFilterGenerationPreview.CreateBoardShape();
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        foreach (TimedLevelDefinition definition in LoadDefinitions())
        {
            TimedLevelValidator.ValidateForRuntime(definition, shape, TimedLevelLayoutRules.GeneratorVersion);

            if (definition.Variants.Count < RequiredVariantCount)
                throw new InvalidOperationException($"{definition.name}: {definition.Variants.Count} variants, {RequiredVariantCount} required.");

            for (int variantIndex = 0; variantIndex < definition.Variants.Count; variantIndex++)
                ValidateSolution(simulator, definition, variantIndex);
        }

        Debug.Log("FILTER_CHAPTER_VALIDATION_PASS");
    }

    [MenuItem("Tools/Timed Levels/Filter Chapter Bot Report")]
    public static void ReportBots()
    {
        foreach (TimedLevelDefinition definition in LoadDefinitions())
            ShelfFilterBotReport.Report(definition.name, definition.Variants);
    }

    private static IEnumerable<TimedLevelDefinition> LoadDefinitions()
    {
        for (int chapterLevel = 1; chapterLevel <= ChapterLevelCount; chapterLevel++)
        {
            LevelEntry level = AssetDatabase.LoadAssetAtPath<LevelEntry>(ShelfFilterSandbox.GetLevelEntryPath(chapterLevel));

            if (level == null || level.Definition == null)
                throw new InvalidOperationException($"{ShelfFilterSandbox.GetLevelEntryPath(chapterLevel)} is missing or has no definition.");

            yield return level.Definition;
        }
    }

    private static void ValidateSolution(BoardMoveSimulator simulator, TimedLevelDefinition definition, int variantIndex)
    {
        TimedLevelVariant variant = definition.Variants[variantIndex];
        BoardStateSnapshot state = variant.CreateLayout();

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation))
                throw new InvalidOperationException($"{definition.name}: variant {variantIndex + 1} solution does not replay.");

            if (simulation.MatchCount != (simulation.IsSwap ? 0 : 1))
                throw new InvalidOperationException($"{definition.name}: variant {variantIndex + 1} solution move makes {simulation.MatchCount} matches.");

            state = simulation.State;
        }

        if (!state.IsCleared)
            throw new InvalidOperationException($"{definition.name}: variant {variantIndex + 1} solution does not clear the board.");

        if (TimedLevelLayoutRules.CountFilteredMoves(variant.CreateLayout(), variant.CreateSolution()) < definition.MinimumFilteredMoveCount)
            throw new InvalidOperationException($"{definition.name}: variant {variantIndex + 1} has too few filtered moves.");
    }
}
