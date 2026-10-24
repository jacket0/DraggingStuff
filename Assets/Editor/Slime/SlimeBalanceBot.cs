using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SlimeBalanceBot
{
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";
    private const double SecondsPerMove = 2.5;
    private const int PreviewRunsPerPolicy = 10;
    private const int FullRunsPerPolicy = 50;

    [MenuItem("Tools/Timed Levels/Slime/Run Balance Bot (Preview)")]
    public static void RunPreview() => RunCatalog(PreviewRunsPerPolicy);

    [MenuItem("Tools/Timed Levels/Slime/Run Balance Bot")]
    public static void RunFull() => RunCatalog(FullRunsPerPolicy);

    public static void RunCatalog(int runsPerPolicy)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null)
            throw new InvalidOperationException($"{CatalogPath} was not found.");

        LevelEntry[] levels = catalog.Levels.Where(level => level.Definition.HasSlime).ToArray();

        if (levels.Length == 0)
            Debug.Log("SLIME_BOT: the catalog has no slime levels yet.");

        foreach (LevelEntry level in levels)
            Debug.Log(Run(level.Definition, $"level {level.Number}", runsPerPolicy));
    }

    public static string Run(TimedLevelDefinition definition, string title, int runsPerPolicy)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (!definition.HasSlime || definition.Variants.Count == 0)
            throw new InvalidOperationException($"{definition.name}: no slime or variants to play.");

        int moveLimit = GetMoveLimit(definition);
        SlimeBotReport report = new SlimeBotReport();

        for (int variantIndex = 0; variantIndex < definition.Variants.Count; variantIndex++)
            report.RunVariant(definition.AddSlime(definition.Variants[variantIndex].CreateLayout()), variantIndex, runsPerPolicy, moveLimit);

        return report.Format(title, moveLimit);
    }

    public static int GetMoveLimit(TimedLevelDefinition definition) => Math.Max(1, (int)(definition.TimeLimitSeconds / SecondsPerMove));
}
