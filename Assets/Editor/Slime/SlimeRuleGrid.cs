using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class SlimeRuleGrid
{
    private const string ScenePath = "Assets/Scenes/EighthLevel.unity";
    private const string PrototypeDefinitionPath = "Assets/Levels/Prototype/TimedLevel_SlimePrototype_1.asset";
    private const int RunsPerVariant = 12;

    private static readonly SlimeBotSimulation.Policy[] Policies =
    {
        SlimeBotSimulation.Policy.Careful,
        SlimeBotSimulation.Policy.Skilled,
        SlimeBotSimulation.Policy.Greedy,
        SlimeBotSimulation.Policy.Baseline
    };

    private static readonly GridConfig[] Configs =
    {
        new GridConfig("own cap2 wave1 p2 start2", 2, 1, 2, true, 0, 2),
        new GridConfig("own cap2 wave1 p3 start0", 2, 1, 3, true, 0, 0),
        new GridConfig("own cap2 wave1 p3 start2", 2, 1, 3, true, 0, 2),
        new GridConfig("own cap2 wave1 p3 start4", 2, 1, 3, true, 0, 4),
        new GridConfig("own cap2 wave1 p4 start2", 2, 1, 4, true, 0, 2),
        new GridConfig("own cap2 wave1 p4 start4", 2, 1, 4, true, 0, 4),
        new GridConfig("own cap2 wave1 p5 start4", 2, 1, 5, true, 0, 4),
        new GridConfig("own cap2 wave1 p3 grace3 start2", 2, 1, 3, true, 3, 2),
        new GridConfig("own cap2 wave2 p4 start2", 2, 2, 4, true, 0, 2),
        new GridConfig("own cap2 wave2 p5 start2", 2, 2, 5, true, 0, 2),
        new GridConfig("own cap1 wave1 p2 start2", 1, 1, 2, true, 0, 2),
        new GridConfig("own cap1 wave2 p3 start2", 1, 2, 3, true, 0, 2),
        new GridConfig("wide cap2 wave1 p2 start2", 2, 1, 2, false, 0, 2),
        new GridConfig("wide cap2 wave2 p4 start2", 2, 2, 4, false, 0, 2)
    };

    [MenuItem("Tools/Timed Levels/Slime/Run Rule Grid")]
    public static void Run()
    {
        SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ShelfBoard board = UnityEngine.Object.FindObjectOfType<ShelfBoard>();
            board.Initialize();
            TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(PrototypeDefinitionPath);
            IReadOnlyList<SlimeColumnLink> links = SlimeNeighborhoodGeometry.CreateLinks(board);
            Dictionary<int, IReadOnlyList<ColumnPosition>> paths = new Dictionary<int, IReadOnlyList<ColumnPosition>>();

            foreach (int cap in Configs.Select(config => config.ColumnsPerShelf).Distinct())
            {
                paths[cap] = SlimeNeighborhoodGeometry.CreateGrowthPath(board, cap);
                Debug.Log($"SLIME_GRID_PATH cap {cap}: {string.Join(" ", paths[cap].Select(column => $"{column.ShelfIndex}.{column.ColumnIndex}"))}");
            }

            int moveLimit = SlimeBalanceBot.GetMoveLimit(definition);
            Stopwatch stopwatch = Stopwatch.StartNew();

            foreach (GridConfig config in Configs)
            {
                SlimeBotReport report = new SlimeBotReport();
                IReadOnlyList<ColumnPosition> path = paths[config.ColumnsPerShelf];
                SlimeRules rules = new SlimeRules(config.ColumnsPerGrowth, !config.OwnShelfOnly, config.GraceMoves);

                for (int variantIndex = 0; variantIndex < definition.Variants.Count; variantIndex++)
                {
                    SlimeSnapshot slime = new SlimeSnapshot(path.Take(config.StartColumns), links, path, config.Period, config.Period + config.GraceMoves, rules);
                    BoardStateSnapshot layout = definition.Variants[variantIndex].CreateLayout().WithSlime(slime);
                    report.RunVariant(layout, variantIndex, RunsPerVariant, moveLimit, Policies);
                }

                Debug.Log(report.FormatGridRow(config.Name));
            }

            Debug.Log($"SLIME_GRID_PASS in {stopwatch.Elapsed.TotalMinutes:F1} min");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }
    }

    private sealed class GridConfig
    {
        public string Name { get; }
        public int ColumnsPerShelf { get; }
        public int ColumnsPerGrowth { get; }
        public int Period { get; }
        public bool OwnShelfOnly { get; }
        public int GraceMoves { get; }
        public int StartColumns { get; }

        public GridConfig(string name, int columnsPerShelf, int columnsPerGrowth, int period, bool ownShelfOnly, int graceMoves, int startColumns)
        {
            Name = name;
            ColumnsPerShelf = columnsPerShelf;
            ColumnsPerGrowth = columnsPerGrowth;
            Period = period;
            OwnShelfOnly = ownShelfOnly;
            GraceMoves = graceMoves;
            StartColumns = startColumns;
        }
    }
}
