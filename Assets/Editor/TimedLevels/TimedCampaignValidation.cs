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
    private const int ExpectedLevelCount = 20;
    private const int ChapterCount = 4;
    private const int LevelsPerChapter = 5;
    private const string FirstClosedShelfDefinitionPath = "Assets/Levels/Timed/TimedLevel_C3_01.asset";
    private const string LevelResultWindowPath = "Assets/Prefabs/UI/Level/LevelResultWindow.prefab";

    private static readonly Dictionary<LevelMechanic, string> MechanicSceneNames = new Dictionary<LevelMechanic, string>
    {
        { LevelMechanic.Basics, "SimpleLevel" },
        { LevelMechanic.ShelfFilter, "ThirdLevel" },
        { LevelMechanic.ClosedShelves, "FourthLevel" },
        { LevelMechanic.Conveyor, "FifthLevel" }
    };

    [MenuItem("Tools/Timed Levels/Validate Campaign")]
    public static void Run()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        if (catalog == null || catalog.Levels.Count != ExpectedLevelCount)
            throw new InvalidOperationException(nameof(LevelCatalog));

        ValidateDefinitions(catalog);
        ValidateMoveSimulation();
        ValidateClosedShelfModel();
        ValidateClosedShelfBoardInvariant(catalog);
        ValidateChapters(catalog);
        ConveyorModelValidation.Run(catalog);
        ShelfFilterModelValidation.Run(catalog);
        ValidateTripleRefillGenerator();
        ValidateRevealedShelfLatinSquare();
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

        if (catalog == null || catalog.Levels.Count != ExpectedLevelCount)
            throw new InvalidOperationException(nameof(LevelCatalog));

        ValidateDefinitions(catalog);
        ValidateMoveSimulation();
        ValidateClosedShelfModel();
        ValidateClosedShelfBoardInvariant(catalog);
        ValidateChapters(catalog);
        ConveyorModelValidation.Run(catalog);
        ShelfFilterModelValidation.Run(catalog);
        ValidateTripleRefillGenerator();
        ValidateRevealedShelfLatinSquare();
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
                || simulation.MatchCount != (simulation.IsSwap ? 0 : 1))
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

        if (matchingSwap.Matches.Count != 1
            || matchingSwap.Matches[0].ShelfIndex != 1
            || matchingSwap.Matches[0].Type != ItemType.Ball
            || matchingSwap.Matches[0].ItemCount != 3)
        {
            throw new InvalidOperationException("Matching swap match info");
        }
    }

    private static void ValidateClosedShelfModel()
    {
        BoardMoveSimulator simulator = new BoardMoveSimulator();

        BoardStateSnapshot closedTargetBoard = new BoardStateSnapshot(new[]
        {
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(new[] { ItemType.Ball }),
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(Array.Empty<ItemType>())
            }),
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(Array.Empty<ItemType>())
            }, isOpen: false)
        });

        if (simulator.TrySimulate(closedTargetBoard, new ColumnPosition(0, 0), new ColumnPosition(1, 0), out _))
            throw new InvalidOperationException("Move into closed shelf");

        BoardStateSnapshot closedSourceBoard = new BoardStateSnapshot(new[]
        {
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(new[] { ItemType.Ball }),
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(Array.Empty<ItemType>())
            }, isOpen: false),
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(Array.Empty<ItemType>())
            })
        });

        if (simulator.TrySimulate(closedSourceBoard, new ColumnPosition(0, 0), new ColumnPosition(1, 0), out _))
            throw new InvalidOperationException("Move out of closed shelf");

        BoardStateSnapshot emptyColumnBoard = new BoardStateSnapshot(new[]
        {
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(Array.Empty<ItemType>())
            }, isOpen: false),
            new ShelfStateSnapshot(new[]
            {
                new ColumnStateSnapshot(Array.Empty<ItemType>()),
                new ColumnStateSnapshot(new[] { ItemType.Ball }),
                new ColumnStateSnapshot(Array.Empty<ItemType>())
            })
        });

        if (emptyColumnBoard.EmptyColumnCount != 2)
            throw new InvalidOperationException("Closed shelf empty column count");
    }

    private static void ValidateClosedShelfBoardInvariant(LevelCatalog catalog)
    {
        LevelEntry level = catalog.Levels[0];
        string scenePath = FindScenePath(level.SceneName);
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        ShelfBoard board = FindInScene<ShelfBoard>(scene);
        board.Initialize();

        if (!board.IsCleared || board.HasClosedShelves)
            throw new InvalidOperationException("Closed shelf invariant: unexpected initial state");

        Shelf shelf = board.Shelves[0];
        board.CloseShelf(shelf);

        if (!board.HasClosedShelves || !board.IsShelfClosed(shelf) || board.IsShelfAvailable(shelf) || board.IsCleared)
            throw new InvalidOperationException("Closed shelf invariant: board reports cleared with a closed empty shelf");

        board.OpenShelf(shelf);

        if (board.HasClosedShelves || !board.IsCleared)
            throw new InvalidOperationException("Closed shelf invariant: shelf did not reopen");
    }

    private static void ValidateChapters(LevelCatalog catalog)
    {
        if (catalog.Chapters.Count != ChapterCount)
            throw new InvalidOperationException($"Catalog: {catalog.Chapters.Count} chapters, {ChapterCount} expected.");

        int expectedNumber = 1;

        foreach (LevelChapter chapter in catalog.Chapters)
        {
            if (chapter.Levels.Count != LevelsPerChapter)
                throw new InvalidOperationException($"{chapter.name}: {chapter.Levels.Count} levels, {LevelsPerChapter} expected.");

            string sceneName = MechanicSceneNames[chapter.Mechanic];

            foreach (LevelEntry level in chapter.Levels)
            {
                if (level.Number != expectedNumber)
                    throw new InvalidOperationException($"{chapter.name}: {level.name} has number {level.Number}, {expectedNumber} expected.");

                if (level.SceneName != sceneName)
                    throw new InvalidOperationException($"Level {level.Number}: {chapter.Mechanic} levels belong to {sceneName}.");

                foreach (LevelMechanic mechanic in MechanicSceneNames.Keys)
                {
                    int count = level.Definition.GetMechanicElementCount(mechanic);
                    bool isExpected = mechanic == chapter.Mechanic && mechanic != LevelMechanic.Basics;
                    bool isAllowed = isExpected || IsSecondaryMechanicAllowed(chapter.Mechanic, mechanic);

                    if (isExpected && count == 0 || !isAllowed && count > 0)
                        throw new InvalidOperationException($"Level {level.Number}: {count} {mechanic} elements in a {chapter.Mechanic} chapter.");
                }

                expectedNumber++;
            }
        }
    }

    private static bool IsSecondaryMechanicAllowed(LevelMechanic chapterMechanic, LevelMechanic mechanic) =>
        chapterMechanic == LevelMechanic.Conveyor && mechanic == LevelMechanic.ShelfFilter;

    private static void ValidateTripleRefillGenerator()
    {
        TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(FirstClosedShelfDefinitionPath);

        if (definition == null || definition.RefillSettings == null)
            throw new InvalidOperationException($"{FirstClosedShelfDefinitionPath}: missing refill settings for the triple generator check.");

        ShelfRefillSettings settings = definition.RefillSettings;
        ItemType[] levelTypes = definition.ItemGroups.Select(group => group.Type).Distinct().ToArray();

        if (levelTypes.Length == 0)
            throw new InvalidOperationException($"{FirstClosedShelfDefinitionPath}: no item types configured.");

        System.Random random = new System.Random(20260921);

        for (int iteration = 0; iteration < 10000; iteration++)
        {
            (List<ItemType>[][] shelves, bool[] shelfOpen) = CreateRandomTripleBoard(random, levelTypes, random.Next(9, 15));
            BoardStateSnapshot before = ToSnapshot(shelves, shelfOpen);
            Dictionary<ItemType, int> remaining = CreateRandomRemaining(random, levelTypes);
            int emptyColumnReserve = Math.Min(before.EmptyColumnCount, random.Next(0, 4));

            TripleRefillGenerator generator = new TripleRefillGenerator(random, settings, levelTypes);
            GenerationBatch batch = generator.GenerateRefill(before, remaining, emptyColumnReserve);

            VerifyTripleBatch(before, batch, emptyColumnReserve, iteration);
        }
    }

    private static (List<ItemType>[][] Shelves, bool[] Open) CreateRandomTripleBoard(System.Random random, IReadOnlyList<ItemType> levelTypes, int shelfCount)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            bool[] shelfOpen = new bool[shelfCount];
            int openCount = 0;

            for (int shelfIndex = 0; shelfIndex < shelfCount; shelfIndex++)
            {
                shelfOpen[shelfIndex] = random.NextDouble() < 0.75;

                if (shelfOpen[shelfIndex])
                    openCount++;
            }

            // Real closed-shelf levels close at most a handful of shelves at once (see the level
            // table in the design doc, up to 3 of 14); mirror that so forced same-type triples
            // (from a demanding remaining-count) always have enough distinct columns to land on.
            int minimumOpenShelves = Math.Max(6, shelfCount - 3);

            for (int shelfIndex = 0; openCount < minimumOpenShelves && shelfIndex < shelfCount; shelfIndex++)
            {
                if (shelfOpen[shelfIndex])
                    continue;

                shelfOpen[shelfIndex] = true;
                openCount++;
            }

            List<ItemType>[][] shelves = new List<ItemType>[shelfCount][];
            List<ColumnPosition> openPositions = new List<ColumnPosition>();

            for (int shelfIndex = 0; shelfIndex < shelfCount; shelfIndex++)
            {
                shelves[shelfIndex] = new[] { new List<ItemType>(), new List<ItemType>(), new List<ItemType>() };

                if (shelfOpen[shelfIndex])
                {
                    for (int columnIndex = 0; columnIndex < 3; columnIndex++)
                        openPositions.Add(new ColumnPosition(shelfIndex, columnIndex));
                }
            }

            int tripleCount = random.Next(0, 15);

            for (int triple = 0; triple < tripleCount; triple++)
            {
                ItemType type = levelTypes[random.Next(levelTypes.Count)];

                for (int placement = 0; placement < 3; placement++)
                {
                    ColumnPosition position;
                    int guard = 0;

                    do
                    {
                        position = openPositions[random.Next(openPositions.Count)];
                        guard++;
                    }
                    while (shelves[position.ShelfIndex][position.ColumnIndex].Count >= TimedLevelLayoutRules.MaximumColumnDepth - 1 && guard < 50);

                    shelves[position.ShelfIndex][position.ColumnIndex].Add(type);
                }
            }

            if (HasAnyFrontMatch(shelves, shelfOpen))
                continue;

            return (shelves, shelfOpen);
        }

        throw new InvalidOperationException("Failed to build a random triple-refill test board.");
    }

    private static bool HasAnyFrontMatch(List<ItemType>[][] shelves, bool[] shelfOpen)
    {
        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            if (!shelfOpen[shelfIndex])
                continue;

            List<ItemType>[] columns = shelves[shelfIndex];

            if (columns.All(column => column.Count > 0 && column[0] == columns[0][0]))
                return true;
        }

        return false;
    }

    private static BoardStateSnapshot ToSnapshot(List<ItemType>[][] shelves, bool[] shelfOpen)
    {
        ShelfStateSnapshot[] snapshotShelves = new ShelfStateSnapshot[shelves.Length];

        for (int shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            ColumnStateSnapshot[] columns = shelves[shelfIndex].Select(column => new ColumnStateSnapshot(column)).ToArray();
            snapshotShelves[shelfIndex] = new ShelfStateSnapshot(columns, shelfOpen[shelfIndex]);
        }

        return new BoardStateSnapshot(snapshotShelves);
    }

    private static Dictionary<ItemType, int> CreateRandomRemaining(System.Random random, IReadOnlyList<ItemType> levelTypes)
    {
        Dictionary<ItemType, int> remaining = new Dictionary<ItemType, int>();
        int needyCount = random.Next(0, Math.Min(levelTypes.Count, 3) + 1);
        List<ItemType> pool = new List<ItemType>(levelTypes);

        for (int index = 0; index < needyCount; index++)
        {
            int poolIndex = random.Next(pool.Count);
            ItemType type = pool[poolIndex];
            pool.RemoveAt(poolIndex);
            remaining[type] = random.Next(1, 4) * 3;
        }

        return remaining;
    }

    private static void VerifyTripleBatch(BoardStateSnapshot before, GenerationBatch batch, int emptyColumnReserve, int iteration)
    {
        Dictionary<ItemType, int> totals = new Dictionary<ItemType, int>();
        int emptyOpenColumnsAfter = 0;

        for (int shelfIndex = 0; shelfIndex < before.Shelves.Count; shelfIndex++)
        {
            ShelfStateSnapshot shelf = before.Shelves[shelfIndex];
            ItemType[][] afterColumns = new ItemType[shelf.Capacity][];

            for (int columnIndex = 0; columnIndex < shelf.Capacity; columnIndex++)
            {
                IReadOnlyList<ItemType> newItems = batch.GetItems(shelfIndex, columnIndex);

                if (!shelf.IsOpen && newItems.Count > 0)
                    throw new InvalidOperationException($"Iteration {iteration}: item placed on a closed shelf {shelfIndex}.");

                IReadOnlyList<ItemType> oldItems = shelf.Columns[columnIndex].Items;
                ItemType[] combined = oldItems.Concat(newItems).ToArray();
                afterColumns[columnIndex] = combined;

                if (combined.Length > TimedLevelLayoutRules.MaximumColumnDepth)
                    throw new InvalidOperationException($"Iteration {iteration}: column {shelfIndex}/{columnIndex} exceeds maximum depth.");

                if (oldItems.Count > 0 && newItems.Count > 0 && oldItems[oldItems.Count - 1] == newItems[0])
                    throw new InvalidOperationException($"Iteration {iteration}: adjacent duplicate at old tail of {shelfIndex}/{columnIndex}.");

                for (int index = 1; index < newItems.Count; index++)
                {
                    if (newItems[index] == newItems[index - 1])
                        throw new InvalidOperationException($"Iteration {iteration}: adjacent duplicate within new items of {shelfIndex}/{columnIndex}.");
                }

                foreach (ItemType type in combined)
                    totals[type] = totals.TryGetValue(type, out int existing) ? existing + 1 : 1;

                if (shelf.IsOpen && combined.Length == 0)
                    emptyOpenColumnsAfter++;
            }

            if (shelf.IsOpen && afterColumns.All(column => column.Length > 0) && afterColumns.Select(column => column[0]).Distinct().Count() == 1)
                throw new InvalidOperationException($"Iteration {iteration}: shelf {shelfIndex} front row became a match after refill.");
        }

        foreach (KeyValuePair<ItemType, int> pair in totals)
        {
            if (pair.Value % 3 != 0)
                throw new InvalidOperationException($"Iteration {iteration}: type {pair.Key} count {pair.Value} is not a multiple of three.");
        }

        if (emptyOpenColumnsAfter < emptyColumnReserve)
            throw new InvalidOperationException($"Iteration {iteration}: empty column reserve violated ({emptyOpenColumnsAfter} < {emptyColumnReserve}).");
    }

    private static void ValidateRevealedShelfLatinSquare()
    {
        TimedLevelDefinition definition = AssetDatabase.LoadAssetAtPath<TimedLevelDefinition>(FirstClosedShelfDefinitionPath);

        if (definition == null || definition.RefillSettings == null)
            throw new InvalidOperationException($"{FirstClosedShelfDefinitionPath}: missing refill settings for the Latin square check.");

        ShelfRefillSettings settings = definition.RefillSettings;
        ItemType[] levelTypes = definition.ItemGroups.Select(group => group.Type).Distinct().ToArray();

        IEnumerable<int> groupCounts = definition.ClosedShelves.Select(shelf => shelf.RevealedGroupCount)
            .Concat(new[] { 3, 6, 9, 12 })
            .Distinct();

        foreach (int groupCount in groupCounts)
        {
            for (int seed = 0; seed < 5; seed++)
            {
                TripleRefillGenerator generator = new TripleRefillGenerator(new System.Random(seed), settings, levelTypes);
                IReadOnlyList<IReadOnlyList<ItemType>> columns = generator.CreateRevealedShelf(groupCount, new Dictionary<ItemType, int>());

                if (columns.Count != 3)
                    throw new InvalidOperationException("Revealed shelf: expected 3 columns.");

                for (int row = 0; row < groupCount; row++)
                {
                    if (columns[0][row] == columns[1][row] && columns[1][row] == columns[2][row])
                        throw new InvalidOperationException($"Revealed shelf row {row} is an instant match (groupCount {groupCount}, seed {seed}).");
                }

                for (int column = 0; column < 3; column++)
                {
                    for (int row = 1; row < groupCount; row++)
                    {
                        if (columns[column][row] == columns[column][row - 1])
                            throw new InvalidOperationException($"Revealed shelf column {column} has adjacent duplicates (groupCount {groupCount}, seed {seed}).");
                    }
                }
            }
        }
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
            _ = FindAllInScene<ScoreView>(scene).Single(view => view.transform.IsChildOf(hud.transform));

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
            ValidateCampaignHud(sceneName, timerView);
            ValidateResultWindowPrefab(sceneName, result);
            ValidateResultLayout(sceneName, result);
            ValidateShelfFilterScene(sceneName, scene, catalog.Levels.Where(level => level.SceneName == sceneName && level.Definition.HasShelfFilters));
        }
    }

    private static void ValidateShelfFilterScene(string sceneName, Scene scene, IEnumerable<LevelEntry> filterLevels)
    {
        int[] filteredShelfIndices = filterLevels
            .SelectMany(level => level.Definition.ShelfFilters)
            .Select(shelfFilter => shelfFilter.ShelfIndex)
            .Distinct()
            .ToArray();

        if (filteredShelfIndices.Length == 0)
            return;

        ShelfFiltersController controller = FindAllInScene<ShelfFiltersController>(scene).Single();

        foreach (string field in new[] { "_session", "_shelfBoard", "_dragController", "_columnRaycaster", "_audio" })
            RequireReference(controller, field);

        ShelfBoard board = GetReference<ShelfBoard>(controller, "_shelfBoard");
        ShelfFilterSignView[] signs = GetReferences<ShelfFilterSignView>(controller, "_signs");

        foreach (int shelfIndex in filteredShelfIndices)
        {
            Shelf shelf = board.Shelves[shelfIndex];

            if (signs.Count(sign => GetReference<Shelf>(sign, "_shelf") == shelf) != 1)
                throw new InvalidOperationException($"{sceneName}: filtered shelf {shelfIndex} needs exactly one sign.");
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

        if (cards.Length != ExpectedLevelCount)
            throw new InvalidOperationException($"Level cards: {cards.Length}");

        foreach (LevelCardView card in cards)
        {
            foreach (string field in new[] { "_timeRoot", "_bestTimeText", "_starRating", "_playRoot", "_currentHighlight", "_newRibbon", "_sticker", "_stickerIcon", "_stickerCountText", "_lockedSilhouette" })
                RequireReference(card, field);

            ValidateLevelCard(card);
        }

        LevelChapterSectionView[] sections = FindAllInScene<LevelChapterSectionView>(scene).ToArray();

        if (sections.Length != ChapterCount || sections.Any(section => section.Header == null || section.Cards.Count != LevelsPerChapter))
            throw new InvalidOperationException($"Main menu: {sections.Length} chapter sections, each needs a header and {LevelsPerChapter} cards.");

        foreach (LevelChapterSectionView section in sections)
        {
            RequireReference(section, "_openFrame");
            RequireReference(section, "_lockedFrame");
        }

        EndlessModeCardView endlessCard = FindAllInScene<EndlessModeCardView>(scene).Single();

        foreach (string field in new[] { "_playRoot", "_lockedHintRoot" })
            RequireReference(endlessCard, field);
    }

    private static void ValidateCampaignHud(string sceneName, CampaignTimerView timerView)
    {
        if (FindAllInScene<CampaignTimerView>(timerView.gameObject.scene).Count() != 1)
            throw new InvalidOperationException($"{sceneName}: duplicate campaign timers.");
    }

    private static void ValidateResultLayout(string sceneName, LevelResultView result)
    {
        RectTransform background = result.transform.Find("Background") as RectTransform;
        RectTransform content = background != null ? background.Find("TimedResultContent") as RectTransform : null;
        RectTransform reviewButton = background != null ? background.Find("ReviewButton") as RectTransform : null;
        RectTransform actionsRow = background != null ? background.Find("ActionsRow") as RectTransform : null;

        if (background == null
            || content == null
            || background.Find("ResultBlock") != null
            || background.Find("Title") != null
            || background.Find("Divider") != null
            || background.GetComponent<VerticalLayoutGroup>() != null
            || reviewButton == null
            || actionsRow == null)
        {
            throw new InvalidOperationException($"{sceneName}: result layout contains obsolete content.");
        }

        if (!IsStackedInside(background, content, reviewButton) || !IsStackedInside(background, reviewButton, actionsRow))
            throw new InvalidOperationException($"{sceneName}: result layout elements overlap or leave the background.");

        RectTransform statistics = content.Find("Statistics") as RectTransform;
        RectTransform remainingItems = statistics != null ? statistics.Find("RemainingItems") as RectTransform : null;
        RectTransform score = statistics != null ? statistics.Find("Score") as RectTransform : null;
        RectTransform bestScore = statistics != null ? statistics.Find("BestScore") as RectTransform : null;

        if (remainingItems == null || score == null || bestScore == null)
            throw new InvalidOperationException($"{sceneName}: result statistics layout.");

        Rect remainingItemsRect = GetRectInParent(remainingItems);

        if (remainingItemsRect.Overlaps(GetRectInParent(score)) || remainingItemsRect.Overlaps(GetRectInParent(bestScore)))
            throw new InvalidOperationException($"{sceneName}: remaining items overlap score.");
    }

    private static void ValidateResultWindowPrefab(string sceneName, LevelResultView result)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelResultWindowPath);

        if (prefab == null
            || PrefabUtility.GetNearestPrefabInstanceRoot(result.gameObject) != result.gameObject
            || PrefabUtility.GetCorrespondingObjectFromSource(result.gameObject) != prefab)
        {
            throw new InvalidOperationException($"{sceneName}: result window is not a {LevelResultWindowPath} instance.");
        }
    }

    private static bool IsStackedInside(RectTransform background, RectTransform upper, RectTransform lower)
    {
        Rect bounds = background.rect;
        Rect upperRect = GetRectInParent(upper);
        Rect lowerRect = GetRectInParent(lower);

        return upperRect.yMin >= lowerRect.yMax
            && upperRect.yMax <= bounds.yMax
            && lowerRect.yMin >= bounds.yMin;
    }

    private static Rect GetRectInParent(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetLocalCorners(corners);
        Vector2 offset = rect.localPosition;
        return Rect.MinMaxRect(corners[0].x + offset.x, corners[0].y + offset.y, corners[2].x + offset.x, corners[2].y + offset.y);
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

        GameProgressData campaignProgress = CreateProgress(900, 80000, 2);
        campaignProgress.Version = GameProgressData.FirstTimedCampaignVersion;

        if (GameProgressMerger.Clone(campaignProgress).Levels.Count != 1)
            throw new InvalidOperationException("Campaign progress migration");

        campaignProgress.Version = GameProgressData.CurrentVersion + 1;

        if (GameProgressMerger.Clone(campaignProgress).Levels.Count != 1)
            throw new InvalidOperationException("Newer progress version");

        ValidateChapterCampaignMigration();

        GameProgressData first = CreateProgress(1000, 80000, 2);
        GameProgressData second = CreateProgress(1200, 90000, 3);
        LevelProgressData merged = GameProgressMerger.Merge(first, second).Levels.Single();

        if (merged.BestScore != 1200 || merged.BestCompletionTimeMilliseconds != 80000 || merged.Stars != 3 || !merged.IsCompleted)
            throw new InvalidOperationException("Progress merge");

        GameProgressData newerBonuses = new GameProgressData
        {
            Bonuses = new List<BonusAmountData> { new BonusAmountData { Id = "hint", Amount = 1 } }
        };
        GameProgressData olderBonuses = new GameProgressData
        {
            TutorialCompleted = true,
            Bonuses = new List<BonusAmountData>
            {
                new BonusAmountData { Id = "hint", Amount = 4 },
                new BonusAmountData { Id = "freeze", Amount = 2 }
            }
        };
        GameProgressData mergedProgress = GameProgressMerger.Merge(newerBonuses, olderBonuses);
        List<BonusAmountData> mergedBonuses = mergedProgress.Bonuses;

        if (mergedBonuses.Single(bonus => bonus.Id == "hint").Amount != 1 || mergedBonuses.Single(bonus => bonus.Id == "freeze").Amount != 2)
            throw new InvalidOperationException("Bonus merge");

        if (!mergedProgress.TutorialCompleted)
            throw new InvalidOperationException("Tutorial merge");
    }

    private static void ValidateChapterCampaignMigration()
    {
        GameProgressData timedCampaign = CreateTimedCampaignProgress(1, 2, 5, 12, 13, 16, 19, 20);
        timedCampaign.EndlessBestScore = 700;
        timedCampaign.TutorialCompleted = true;
        timedCampaign.Bonuses.Add(new BonusAmountData { Id = "hint", Amount = 3 });
        GameProgressData migrated = GameProgressMerger.Clone(timedCampaign);
        int[] expectedNumbers = { 1, 2, 11, 15, 18, 20 };
        long[] expectedScores = { 100, 200, 1300, 1600, 1900, 2000 };

        if (migrated.Version != GameProgressData.CurrentVersion
            || !migrated.Levels.Select(level => level.LevelNumber).SequenceEqual(expectedNumbers)
            || !migrated.Levels.Select(level => level.BestScore).SequenceEqual(expectedScores)
            || migrated.Levels.Any(level => level.Stars != 3 || level.BestCompletionTimeMilliseconds != 60000 || !level.IsCompleted))
        {
            throw new InvalidOperationException("Chapter migration: level records");
        }

        if (migrated.EndlessBestScore != 700 || !migrated.TutorialCompleted || migrated.Bonuses.Single().Amount != 3)
            throw new InvalidOperationException("Chapter migration: endless score, tutorial or bonuses");

        if (!GameProgressMerger.AreEqual(GameProgressMerger.Clone(migrated), migrated))
            throw new InvalidOperationException("Chapter migration: a migrated save changed on the second clone");

        GameProgressData chapterCampaign = new GameProgressData
        {
            Levels = new List<LevelProgressData>
            {
                new LevelProgressData { LevelNumber = 11, BestScore = 500, IsCompleted = true },
                new LevelProgressData { LevelNumber = 13, BestScore = 400, IsCompleted = true }
            }
        };

        foreach (GameProgressData merged in new[]
        {
            GameProgressMerger.Merge(chapterCampaign, CreateTimedCampaignProgress(13)),
            GameProgressMerger.Merge(CreateTimedCampaignProgress(13), chapterCampaign)
        })
        {
            if (!merged.Levels.Select(level => level.LevelNumber).SequenceEqual(new[] { 11, 13 })
                || merged.Levels[0].BestScore != 1300
                || merged.Levels[1].BestScore != 400)
            {
                throw new InvalidOperationException("Chapter migration: merge of timed and chapter campaign saves");
            }
        }
    }

    private static GameProgressData CreateTimedCampaignProgress(params int[] levelNumbers)
    {
        return new GameProgressData
        {
            Version = GameProgressData.FirstTimedCampaignVersion,
            Levels = levelNumbers.Select(number => new LevelProgressData
            {
                LevelNumber = number,
                BestScore = number * 100,
                BestCompletionTimeMilliseconds = 60000,
                Stars = 3,
                IsCompleted = true
            }).ToList()
        };
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

    private static T[] GetReferences<T>(UnityEngine.Object target, string propertyName) where T : UnityEngine.Object
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

        if (property == null || !property.isArray)
            throw new InvalidOperationException($"{target.GetType().Name}.{propertyName}");

        return Enumerable.Range(0, property.arraySize)
            .Select(index => property.GetArrayElementAtIndex(index).objectReferenceValue as T)
            .ToArray();
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
