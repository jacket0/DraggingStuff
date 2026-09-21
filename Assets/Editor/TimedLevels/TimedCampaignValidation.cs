using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TimedCampaignValidation
{
    private const string CatalogPath = "Assets/Levels/Menu/MainLevelCatalog.asset";

    [MenuItem("Tools/Timed Levels/Validate Campaign")]
    public static void Run()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null || catalog.Levels.Count != 12)
            throw new InvalidOperationException(nameof(LevelCatalog));

        ValidateDefinitions(catalog);
        ValidateMoveSimulation();
        ValidateCampaignScenes(catalog);
        ValidateEndlessScene();
        ValidateSwapHoverViews();
        ValidateMainMenu();
        ValidateProgressMigration();
        catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        ValidateStars(catalog.Levels[0].Definition);
        ValidateVariantSelection(catalog);
        ValidateImages();
        Debug.Log("TIMED_CAMPAIGN_VALIDATION_PASS");
    }

    [MenuItem("Tools/Timed Levels/Validate Gameplay Changes")]
    public static void RunGameplayChanges()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null || catalog.Levels.Count != 12)
            throw new InvalidOperationException(nameof(LevelCatalog));

        ValidateDefinitions(catalog);
        ValidateMoveSimulation();
        ValidateSwapHoverViews();
        Debug.Log("GAMEPLAY_CHANGES_VALIDATION_PASS");
    }

    private static void ValidateDefinitions(LevelCatalog catalog)
    {
        foreach (LevelEntry level in catalog.Levels)
        {
            if (level == null || !level.CanBeStarted || level.Definition.Variants.Count < 12)
                throw new InvalidOperationException(nameof(LevelEntry));

            string scenePath = FindScenePath(level.SceneName);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ShelfBoard board = FindInScene<ShelfBoard>(scene);
            board.Initialize();
            BoardStateSnapshot boardShape = board.CreateSnapshot();
            TimedLevelValidator.ValidateForRuntime(level.Definition, boardShape, TimedLevelLayoutRules.GeneratorVersion);

            foreach (TimedLevelVariant variant in level.Definition.Variants)
            {
                ValidateSolution(level.Number, variant);

                if (level.Number == 5)
                    ValidateInitialMatchHint(variant);
            }

            int median = level.Definition.Variants.Select(variant => variant.MoveCount).OrderBy(value => value).ElementAt(level.Definition.Variants.Count / 2);

            if (level.Definition.Variants.Any(variant => Math.Abs(variant.MoveCount - median) > median * 0.1f))
                throw new InvalidOperationException($"Level {level.Number}: move count spread");
        }
    }

    private static void ValidateSolution(int levelNumber, TimedLevelVariant variant)
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot state = variant.CreateLayout();

        foreach (TimedLevelMove move in variant.CreateSolution())
        {
            if (!simulator.TrySimulate(state, move.Source, move.Target, out BoardMoveSimulation simulation)
                || !simulation.IsAllowed
                || simulation.MatchCount != 1)
            {
                throw new InvalidOperationException($"Level {levelNumber}, seed {variant.Seed}: invalid solution.");
            }

            state = simulation.State;
        }

        if (!state.IsCleared)
            throw new InvalidOperationException($"Level {levelNumber}, seed {variant.Seed}: incomplete solution.");
    }

    private static void ValidateMoveSimulation()
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();
        BoardStateSnapshot board = new BoardStateSnapshot(new[]
        {
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(new[] { ItemType.Ball, ItemType.Plant }),
                new ColumnStateSnapshot(new[] { ItemType.Bear }),
                new ColumnStateSnapshot(new[] { ItemType.Plant })
            }),
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(new[] { ItemType.Bear, ItemType.Lamp }),
                new ColumnStateSnapshot(new[] { ItemType.Ball }),
                new ColumnStateSnapshot(new[] { ItemType.Bear })
            })
        });

        if (!simulator.TrySimulate(board, new ColumnPosition(0, 0), new ColumnPosition(1, 0), out BoardMoveSimulation swap)
            || !swap.IsAllowed
            || !swap.IsSwap
            || swap.MatchCount != 0
            || swap.State.ItemCount != board.ItemCount
            || swap.State.Shelves[0].Columns[0].Items[0] != ItemType.Bear
            || swap.State.Shelves[0].Columns[0].Items[1] != ItemType.Plant
            || swap.State.Shelves[1].Columns[0].Items[0] != ItemType.Ball
            || swap.State.Shelves[1].Columns[0].Items[1] != ItemType.Lamp)
        {
            throw new InvalidOperationException("Swap simulation");
        }

        if (simulator.TrySimulate(board, new ColumnPosition(0, 1), new ColumnPosition(1, 0), out _))
            throw new InvalidOperationException("Equal type swap");

        BoardStateSnapshot matchingBoard = new BoardStateSnapshot(new[]
        {
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(new[] { ItemType.Ball }),
                new ColumnStateSnapshot(new[] { ItemType.Bear }),
                new ColumnStateSnapshot(new[] { ItemType.Plant })
            }),
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(new[] { ItemType.Bear }),
                new ColumnStateSnapshot(new[] { ItemType.Ball }),
                new ColumnStateSnapshot(new[] { ItemType.Ball })
            })
        });

        if (!simulator.TrySimulate(matchingBoard, new ColumnPosition(0, 0), new ColumnPosition(1, 0), out BoardMoveSimulation matchingSwap)
            || !matchingSwap.IsSwap
            || matchingSwap.MatchCount != 1
            || matchingSwap.State.ItemCount != matchingBoard.ItemCount - 3)
        {
            throw new InvalidOperationException("Matching swap simulation");
        }
    }

    private static void ValidateInitialMatchHint(TimedLevelVariant variant)
    {
        BoardStateSnapshot layout = variant.CreateLayout();
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        for (int targetShelfIndex = 0; targetShelfIndex < layout.Shelves.Count; targetShelfIndex++)
        {
            ShelfStateSnapshot targetShelf = layout.Shelves[targetShelfIndex];

            if (targetShelf.Capacity != 4)
                continue;

            for (int targetColumnIndex = 0; targetColumnIndex < targetShelf.Capacity; targetColumnIndex++)
            {
                if (!targetShelf.Columns[targetColumnIndex].IsEmpty)
                    continue;

                ItemType? type = null;
                bool hasThreeMatchingItems = true;

                for (int columnIndex = 0; columnIndex < targetShelf.Capacity; columnIndex++)
                {
                    if (columnIndex == targetColumnIndex)
                        continue;

                    ItemType? frontItem = targetShelf.Columns[columnIndex].FrontItem;

                    if (!frontItem.HasValue || type.HasValue && frontItem.Value != type.Value)
                    {
                        hasThreeMatchingItems = false;
                        break;
                    }

                    type = frontItem;
                }

                if (!hasThreeMatchingItems || !type.HasValue)
                    continue;

                ColumnPosition target = new ColumnPosition(targetShelfIndex, targetColumnIndex);

                for (int sourceShelfIndex = 0; sourceShelfIndex < layout.Shelves.Count; sourceShelfIndex++)
                {
                    ShelfStateSnapshot sourceShelf = layout.Shelves[sourceShelfIndex];

                    for (int sourceColumnIndex = 0; sourceColumnIndex < sourceShelf.Capacity; sourceColumnIndex++)
                    {
                        ColumnStateSnapshot sourceColumn = sourceShelf.Columns[sourceColumnIndex];

                        if (sourceColumn.IsEmpty || sourceColumn.FrontItem.Value != type.Value)
                            continue;

                        ColumnPosition source = new ColumnPosition(sourceShelfIndex, sourceColumnIndex);

                        if (simulator.TrySimulate(layout, source, target, out BoardMoveSimulation simulation)
                            && simulation.IsAllowed
                            && simulation.MatchCount > 0)
                        {
                            return;
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException($"Level 5, seed {variant.Seed}: initial match hint is unavailable.");
    }

    private static void ValidateCampaignScenes(LevelCatalog catalog)
    {
        foreach (string sceneName in catalog.Levels.Select(level => level.SceneName).Distinct())
        {
            Scene scene = EditorSceneManager.OpenScene(FindScenePath(sceneName), OpenSceneMode.Single);
            ValidateNoMissingScripts(scene);

            LevelSession session = FindInScene<LevelSession>(scene);
            LevelResultView result = FindInScene<LevelResultView>(scene);
            CampaignTimerView timerView = FindInScene<CampaignTimerView>(scene);
            LevelHudView hud = FindInScene<LevelHudView>(scene);
            ScoreView scoreView = FindAllInScene<ScoreView>(scene).Single(view => view.transform.IsChildOf(hud.transform));

            if (FindAllInScene<LevelBuilder>(scene).Any())
                throw new InvalidOperationException($"{sceneName}: old builder");

            RequireReference(session, "_levelBuilder");
            RequireReference(session, "_timer");
            RequireReference(session, "_scoreSystem");
            RequireReference(timerView, "_levelSession");
            RequireReference(timerView, "_timeText");
            RequireReference(timerView, "_startHint");
            RequireReference(timerView, "_availableStars");
            RequireReference(timerView, "_frameImage");
            RequireReference(timerView, "_clockImage");
            RequireReference(result, "_earnedStars");
            RequireReference(result, "_timeResult");
            RequireReference(result, "_bestTimeResult");
            RequireReference(result, "_bestScoreResult");
            RequireReference(result, "_remainingItemsRoot");
            RequireReference(result, "_lightRays");
            RequireReference(result, "_defeatClock");
            ValidateCampaignHud(sceneName, timerView, scoreView);
            ValidateResultLayout(sceneName, result);
        }
    }

    private static void ValidateEndlessScene()
    {
        Scene scene = EditorSceneManager.OpenScene(FindScenePath("EndlessLevel"), OpenSceneMode.Single);
        ValidateNoMissingScripts(scene);
        EndlessTimer timer = FindInScene<EndlessTimer>(scene);

        if (timer.GetComponent<CountdownTimer>() == null)
            throw new InvalidOperationException("Endless countdown timer");

        RequireReference(timer, "_countdownTimer");
    }

    private static void ValidateSwapHoverViews()
    {
        string[] sceneNames = { "SimpleLevel", "SecondLevel", "ThirdLevel", "TutorialLevel", "EndlessLevel" };

        foreach (string sceneName in sceneNames)
        {
            Scene scene = EditorSceneManager.OpenScene(FindScenePath(sceneName), OpenSceneMode.Single);
            ShelfItemDragController dragController = FindInScene<ShelfItemDragController>(scene);

            if (dragController.GetComponent<ShelfSwapHoverView>() == null)
                throw new InvalidOperationException($"{sceneName}: {nameof(ShelfSwapHoverView)}");

            InitialMatchHintController[] initialHints = FindAllInScene<InitialMatchHintController>(scene).ToArray();

            if (sceneName == "SecondLevel")
            {
                if (initialHints.Length != 1)
                    throw new InvalidOperationException($"{sceneName}: {nameof(InitialMatchHintController)}");

                RequireReference(initialHints[0], "_session");
                RequireReference(initialHints[0], "_suggestionProvider");
                RequireReference(initialHints[0], "_presenter");
                RequireReference(initialHints[0], "_dragController");
                RequireReference(initialHints[0], "_idleMovePrompt");
            }
            else if (initialHints.Length != 0)
            {
                throw new InvalidOperationException($"{sceneName}: unexpected {nameof(InitialMatchHintController)}");
            }
        }
    }

    private static void ValidateMainMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(FindScenePath("MainMenu"), OpenSceneMode.Single);
        ValidateNoMissingScripts(scene);
        LevelCardView[] cards = FindAllInScene<LevelCardView>(scene).ToArray();

        if (cards.Length != 12)
            throw new InvalidOperationException($"Level cards: {cards.Length}");

        foreach (LevelCardView card in cards)
        {
            RequireReference(card, "_timeRoot");
            RequireReference(card, "_bestTimeText");
            RequireReference(card, "_starRating");
            ValidateLevelCard(card);
        }
    }

    private static void ValidateCampaignHud(string sceneName, CampaignTimerView timerView, ScoreView scoreView)
    {
        RectTransform timer = timerView.transform as RectTransform;
        RectTransform score = scoreView.transform as RectTransform;

        if (timer == null || score == null || timer.anchorMin.x != 0f || score.anchorMin.x != 1f)
            throw new InvalidOperationException($"{sceneName}: campaign HUD panels are not separated.");

        if (FindAllInScene<CampaignTimerView>(timer.gameObject.scene).Count() != 1)
            throw new InvalidOperationException($"{sceneName}: duplicate campaign timers.");
    }

    private static void ValidateResultLayout(string sceneName, LevelResultView result)
    {
        Transform background = result.transform.Find("Background");
        RectTransform reviewButton = background != null ? background.Find("ReviewButton") as RectTransform : null;
        RectTransform actionsRow = background != null ? background.Find("ActionsRow") as RectTransform : null;

        if (background == null
            || background.Find("TimedResultContent") == null
            || background.Find("ResultBlock") != null
            || background.Find("Title") != null
            || background.Find("Divider") != null
            || background.GetComponent<VerticalLayoutGroup>() != null
            || reviewButton == null
            || actionsRow == null
            || reviewButton.anchoredPosition.y != -120f
            || actionsRow.anchoredPosition.y != -185f)
        {
            throw new InvalidOperationException($"{sceneName}: result layout contains obsolete content.");
        }
    }

    private static void ValidateLevelCard(LevelCardView card)
    {
        Transform progress = card.transform.Find("ButtonVisual/CampaignProgress");
        StarRatingView stars = GetReference<StarRatingView>(card, "_starRating");
        SerializedProperty starList = new SerializedObject(stars).FindProperty("_stars");

        if (progress == null || card.transform.Find("TimedProgress") != null || starList == null || starList.arraySize != 3)
            throw new InvalidOperationException($"{card.name}: campaign progress layout.");

        Image left = starList.GetArrayElementAtIndex(0).objectReferenceValue as Image;
        Image center = starList.GetArrayElementAtIndex(1).objectReferenceValue as Image;
        Image right = starList.GetArrayElementAtIndex(2).objectReferenceValue as Image;

        if (left == null || center == null || right == null || center.rectTransform.sizeDelta.x <= left.rectTransform.sizeDelta.x || center.rectTransform.sizeDelta.x <= right.rectTransform.sizeDelta.x)
            throw new InvalidOperationException($"{card.name}: center star is not emphasized.");
    }

    private static void ValidateProgressMigration()
    {
        GameProgressData legacy = new GameProgressData
        {
            Version = 1,
            LegacyDataImported = true,
            EndlessBestScore = 1250,
            Levels = new List<LevelProgressData>
            {
                new LevelProgressData { LevelNumber = 1, BestScore = 900, IsCompleted = true }
            },
            Bonuses = new List<BonusAmountData>
            {
                new BonusAmountData { Id = "hint", Amount = 4 }
            }
        };
        GameProgressData migrated = GameProgressMerger.Clone(legacy);

        if (migrated.Levels.Count != 0 || migrated.EndlessBestScore != 1250 || migrated.Bonuses.Count != 1 || !migrated.LegacyDataImported)
            throw new InvalidOperationException("Legacy migration");

        GameProgressData first = CreateProgress(1000, 80000, 2);
        GameProgressData second = CreateProgress(1200, 90000, 3);
        LevelProgressData merged = GameProgressMerger.Merge(first, second).Levels.Single();

        if (merged.BestScore != 1200 || merged.BestCompletionTimeMilliseconds != 80000 || merged.Stars != 3 || !merged.IsCompleted)
            throw new InvalidOperationException("Progress merge");
    }

    private static GameProgressData CreateProgress(long score, long time, int stars)
    {
        return new GameProgressData
        {
            Levels = new List<LevelProgressData>
            {
                new LevelProgressData
                {
                    LevelNumber = 1,
                    BestScore = score,
                    BestCompletionTimeMilliseconds = time,
                    Stars = stars,
                    IsCompleted = true
                }
            }
        };
    }

    private static void ValidateStars(TimedLevelDefinition definition)
    {
        long threeStarThreshold = definition.ThreeStarTimeSeconds * 1000L;
        long twoStarThreshold = definition.TwoStarTimeSeconds * 1000L;

        if (LevelStarCalculator.Calculate(true, threeStarThreshold, definition) != 3
            || LevelStarCalculator.Calculate(true, threeStarThreshold + 1, definition) != 2
            || LevelStarCalculator.Calculate(true, twoStarThreshold, definition) != 2
            || LevelStarCalculator.Calculate(true, twoStarThreshold + 1, definition) != 1
            || LevelStarCalculator.Calculate(false, 0, definition) != 0)
        {
            throw new InvalidOperationException("Star thresholds");
        }
    }

    private static void ValidateVariantSelection(LevelCatalog catalog)
    {
        foreach (LevelEntry level in catalog.Levels)
        {
            int previous = TimedLevelVariantSelector.Select(level.Number, level.Definition.Variants.Count);

            for (int attempt = 0; attempt < 50; attempt++)
            {
                int current = TimedLevelVariantSelector.Select(level.Number, level.Definition.Variants.Count);

                if (current == previous)
                    throw new InvalidOperationException($"Level {level.Number}: repeated variant.");

                previous = current;
            }
        }
    }

    private static void ValidateImages()
    {
        ValidateImage("Assets/Sprites/UI/TimedLevels/Stars/LevelStarEmpty-512.png", 512, 512);
        ValidateImage("Assets/Sprites/UI/TimedLevels/Stars/LevelStarShine-512.png", 512, 512);
        ValidateImage("Assets/Sprites/UI/TimedLevels/Timer/CampaignClockIcon-512.png", 512, 512);
        ValidateImage("Assets/Sprites/UI/TimedLevels/Result/ResultLightRays-1024.png", 1024, 1024);
        ValidateImage("Assets/Sprites/UI/TimedLevels/Result/ResultSparkle-256.png", 256, 256);
    }

    private static void ValidateImage(string path, int width, int height)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        if (texture == null || texture.width != width || texture.height != height)
            throw new InvalidOperationException(path);

        if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency)
            throw new InvalidOperationException(path);
    }

    private static void ValidateNoMissingScripts(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    throw new InvalidOperationException($"{scene.name}: {transform.name} has a missing script");
            }
        }
    }

    private static void RequireReference(UnityEngine.Object target, string propertyName)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

        if (property == null || property.objectReferenceValue == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{propertyName}");
    }

    private static T GetReference<T>(UnityEngine.Object target, string propertyName) where T : UnityEngine.Object
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static string FindScenePath(string sceneName)
    {
        string path = AssetDatabase.FindAssets($"{sceneName} t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(candidate => string.Equals(System.IO.Path.GetFileNameWithoutExtension(candidate), sceneName, StringComparison.Ordinal));

        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException(sceneName);

        return path;
    }

    private static T FindInScene<T>(Scene scene) where T : UnityEngine.Object
    {
        T value = FindAllInScene<T>(scene).FirstOrDefault();

        if (value == null)
            throw new InvalidOperationException($"{scene.name}: {typeof(T).Name}");

        return value;
    }

    private static IEnumerable<T> FindAllInScene<T>(Scene scene) where T : UnityEngine.Object
    {
        return Resources.FindObjectsOfTypeAll<T>().Where(value => GetScene(value) == scene);
    }

    private static Scene GetScene(UnityEngine.Object value)
    {
        return value switch
        {
            Component component => component.gameObject.scene,
            GameObject gameObject => gameObject.scene,
            _ => default
        };
    }
}
