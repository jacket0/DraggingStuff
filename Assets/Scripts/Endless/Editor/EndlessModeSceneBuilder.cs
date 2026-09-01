using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EndlessModeSceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/SimpleLevel.unity";
    private const string EndlessScenePath = "Assets/Scenes/EndlessLevel.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GenerationConfigPath = "Assets/Settings/Endless/EndlessGenerationConfig.asset";
    private const string ItemCatalogPath = "Assets/Settings/Endless/EndlessItemCatalog.asset";
    private const string LayerPrefabPath = "Assets/Prefabs/Endless/ShelfLayer.prefab";
    private const string OneSlotLayerPrefabPath = "Assets/Prefabs/Endless/ShelfLayer_01.prefab";
    private const string TwoSlotLayerPrefabPath = "Assets/Prefabs/Endless/ShelfLayer_02.prefab";
    private const string FourSlotLayerPrefabPath = "Assets/Prefabs/Endless/ShelfLayer_04.prefab";
    private const string FiveSlotLayerPrefabPath = "Assets/Prefabs/Endless/ShelfLayer_05.prefab";
    private const string EndlessCardPrefabPath = "Assets/Prefabs/UI/MainMenu/EndlessModeCard.prefab";
    private const string LevelCardPrefabPath = "Assets/Prefabs/UI/MainMenu/LevelCardButton.prefab";

    static EndlessModeSceneBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    [MenuItem("Tools/DruggingStuff/Build Endless Mode")]
    public static void Build()
    {
        BuildAssets(true);
    }

    [MenuItem("Tools/DruggingStuff/Validate Endless Mode")]
    public static void Validate()
    {
        ValidateAssets();
        Debug.Log("Endless mode validation completed.");
    }

    [MenuItem("Tools/DruggingStuff/Open Endless Level")]
    public static void OpenEndlessLevel()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.isDirty)
            throw new InvalidOperationException($"Scene has unsaved changes: {activeScene.path}");

        EditorSceneManager.OpenScene(EndlessScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/DruggingStuff/Connect Manual Endless Layout")]
    public static void ConnectManualLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException();

        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != EndlessScenePath)
            throw new InvalidOperationException(scene.path);

        ConnectManualLayout(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Manual endless layout connected.");
    }

    private static void BuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EndlessScenePath) != null &&
            AssetDatabase.LoadAssetAtPath<EndlessGenerationConfig>(GenerationConfigPath) != null &&
            AssetDatabase.LoadAssetAtPath<EndlessItemCatalog>(ItemCatalogPath) != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(EndlessCardPrefabPath) != null)
            return;

        BuildAssets(false);
    }

    [MenuItem("Tools/DruggingStuff/Prepare Empty Endless Level")]
    private static void PrepareEmptySceneIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EndlessScenePath) == null)
            return;

        Scene scene = SceneManager.GetSceneByPath(EndlessScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;

        if (wasLoaded && scene.isDirty)
        {
            Debug.LogWarning("EndlessLevel was not cleared because it has unsaved changes.");
            return;
        }

        if (!wasLoaded)
            scene = EditorSceneManager.OpenScene(EndlessScenePath, OpenSceneMode.Additive);

        try
        {
            if (FindAll<Transform>(scene).Any(transform => transform.name == "EndlessLayout"))
                return;

            PrepareEmptyScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("EndlessLevel prepared for manual layout.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void PrepareEmptyScene(Scene scene)
    {
        ShelfBoard shelfBoard = FindSingle<ShelfBoard>(scene);

        while (shelfBoard.transform.childCount > 0)
            Object.DestroyImmediate(shelfBoard.transform.GetChild(0).gameObject);

        SetArrayReferences(shelfBoard, "_shelves", Array.Empty<Object>());

        string[] visualRootNames =
        {
            "GeneralShelfBoard",
            "Ground",
            "Walls",
            "EndlessShelfGeometry"
        };

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (visualRootNames.Contains(root.name))
                Object.DestroyImmediate(root);
        }

        foreach (EndlessDebugView debugView in FindAll<EndlessDebugView>(scene))
            Object.DestroyImmediate(debugView.gameObject);

        GameObject layoutRoot = new GameObject("EndlessLayout");
        SceneManager.MoveGameObjectToScene(layoutRoot, scene);

        GameObject environmentRoot = new GameObject("Environment");
        environmentRoot.transform.SetParent(layoutRoot.transform, false);

        GameObject shelfAnchorsRoot = new GameObject("ShelfAnchors");
        shelfAnchorsRoot.transform.SetParent(layoutRoot.transform, false);

        EndlessSystemsActive(scene, false);
        ConfigureTimer(FindSingle<EndlessTimer>(scene));
        UpgradeTimerView(scene);
    }

    private static void EndlessSystemsActive(Scene scene, bool isActive)
    {
        GameObject systems = scene.GetRootGameObjects().Single(root => root.name == "EndlessSystems");
        systems.SetActive(isActive);
    }

    private static void UpgradeTimerView(Scene scene)
    {
        EndlessTimerView timerView = FindSingle<EndlessTimerView>(scene);
        TMP_Text seconds = GetReference<TMP_Text>(timerView, "_secondsText");
        Transform existingTransform = timerView.transform.Find("AddedTime");
        TMP_Text addedTime = existingTransform != null ? existingTransform.GetComponent<TMP_Text>() : null;

        if (addedTime == null)
        {
            addedTime = CreateText(timerView.transform, "AddedTime", seconds.font, 30f, TextAlignmentOptions.Center);
            SetRect(addedTime.rectTransform, new Vector2(1f, 0.5f), new Vector2(110f, 50f), new Vector2(110f, 0f));
            addedTime.fontStyle = FontStyles.Bold;
            addedTime.raycastTarget = false;
            addedTime.gameObject.SetActive(false);
        }

        SetReference(timerView, "_addedTimeText", addedTime);
        SetFloat(timerView, "_vignetteStartNormalizedTime", 0.3f);
        SetFloat(timerView, "_vignetteFullNormalizedTime", 0.05f);
        SetFloat(timerView, "_maximumVignetteAlpha", 0.12f);
        SetFloat(timerView, "_pressureSmoothingDuration", 0.6f);
    }

    private static void ConnectManualLayout(Scene scene)
    {
        EnsureFolders();

        GameObject layoutRoot = FindSingleGameObject(scene, "EndlessLayout");
        ShelfBoard shelfBoard = FindSingle<ShelfBoard>(scene);
        List<Shelf> shelves = layoutRoot.GetComponentsInChildren<Shelf>(true).ToList();

        if (shelves.Count == 0 || shelves.Distinct().Count() != shelves.Count)
            throw new InvalidOperationException(nameof(shelves));

        foreach (Shelf shelf in shelves)
            ConfigureManualShelf(shelf);

        Dictionary<int, ShelfLayer> layerPrefabs = CreateManualLayerPrefabs(shelves);
        EndlessGenerationConfig generationConfig = AssetDatabase.LoadAssetAtPath<EndlessGenerationConfig>(GenerationConfigPath);
        EndlessItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<EndlessItemCatalog>(ItemCatalogPath);

        if (generationConfig == null)
            throw new InvalidOperationException(GenerationConfigPath);

        if (itemCatalog == null)
            throw new InvalidOperationException(ItemCatalogPath);

        SetArrayReferences(shelfBoard, "_shelves", shelves.Cast<Object>().ToArray());

        ShelfLayerPool layerPool = FindSingle<ShelfLayerPool>(scene);
        ShelfItemPool itemPool = FindSingle<ShelfItemPool>(scene);
        EndlessBoardRefiller refiller = FindSingle<EndlessBoardRefiller>(scene);
        EndlessSession endlessSession = FindSingle<EndlessSession>(scene);
        EndlessTimer timer = FindSingle<EndlessTimer>(scene);
        ComboSystem comboSystem = FindSingle<ComboSystem>(scene);
        ScoreSystem scoreSystem = FindSingle<ScoreSystem>(scene);
        EndlessProgressService progress = FindSingle<EndlessProgressService>(scene);
        EndlessResultRecorder resultRecorder = FindSingle<EndlessResultRecorder>(scene);
        ShelfItemDragController dragController = FindSingle<ShelfItemDragController>(scene);
        MatchRewardController rewardController = FindSingle<MatchRewardController>(scene);
        BonusUseController bonusUseController = FindSingle<BonusUseController>(scene);
        TimerFreezeBonusEffect freezeEffect = FindSingle<TimerFreezeBonusEffect>(scene);
        MoveResolutionPlayer moveResolutionPlayer = FindSingle<MoveResolutionPlayer>(scene);

        if (layoutRoot.transform.parent != dragController.transform)
            layoutRoot.transform.SetParent(dragController.transform, true);

        SetReference(layerPool, "_oneSlotLayerPrefab", layerPrefabs[1]);
        SetReference(layerPool, "_twoSlotLayerPrefab", layerPrefabs[2]);
        SetReference(layerPool, "_threeSlotLayerPrefab", layerPrefabs[3]);
        SetReference(layerPool, "_fourSlotLayerPrefab", layerPrefabs[4]);
        SetReference(layerPool, "_fiveSlotLayerPrefab", layerPrefabs[5]);
        SetReference(itemPool, "_catalog", itemCatalog);

        SetReference(refiller, "_shelfBoard", shelfBoard);
        SetReference(refiller, "_generationConfig", generationConfig);
        SetReference(refiller, "_itemCatalog", itemCatalog);
        SetReference(refiller, "_layerPool", layerPool);
        SetReference(refiller, "_itemPool", itemPool);

        SetReference(endlessSession, "_shelfBoard", shelfBoard);
        SetReference(endlessSession, "_moveResolutionPlayer", moveResolutionPlayer);
        SetReference(endlessSession, "_boardRefiller", refiller);
        SetReference(endlessSession, "_timer", timer);

        SetReference(resultRecorder, "_session", endlessSession);
        SetReference(resultRecorder, "_scoreSystem", scoreSystem);
        SetReference(resultRecorder, "_progress", progress);

        SetReference(dragController, "_levelSession", endlessSession);
        SetReference(rewardController, "_levelSession", endlessSession);
        SetReference(bonusUseController, "_session", endlessSession);
        SetBoolean(bonusUseController, "_ignoreLevelUseLimit", true);
        SetReference(freezeEffect, "_primaryTarget", comboSystem);
        SetArrayReferences(freezeEffect, "_additionalTargets", new Object[] { timer });

        EndlessTimerView timerView = FindSingle<EndlessTimerView>(scene);
        SetReference(timerView, "_timer", timer);

        EndlessResultView resultView = FindSingle<EndlessResultView>(scene);
        EndlessUiController uiController = FindSingle<EndlessUiController>(scene);
        SetReference(uiController, "_resultView", resultView);
        SetReference(uiController, "_session", endlessSession);
        SetReference(uiController, "_resultRecorder", resultRecorder);

        ConfigureTimer(timer);
        UpgradeTimerView(scene);
        ValidateManualLayout(scene, shelves, layerPrefabs);
        EndlessSystemsActive(scene, true);
    }

    private static void ConfigureManualShelf(Shelf shelf)
    {
        shelf.ValidateLayers();

        if (shelf.Layers.Count < 2 || shelf.Layers.Any(layer => !layer.IsEmpty))
            throw new InvalidOperationException(shelf.name);

        Transform dynamicRoot = GetReference<Transform>(shelf, "_dynamicLayerRoot");

        if (dynamicRoot == null)
        {
            dynamicRoot = new GameObject("DynamicLayers").transform;
            dynamicRoot.SetParent(shelf.transform, false);
            SetReference(shelf, "_dynamicLayerRoot", dynamicRoot);
        }

        foreach (ShelfLayer layer in shelf.Layers)
        {
            if (layer.transform.parent != dynamicRoot)
                layer.transform.SetParent(dynamicRoot, true);
        }

        ShelfLayerStackView stackView = shelf.GetComponent<ShelfLayerStackView>();

        if (stackView == null)
            throw new InvalidOperationException(nameof(stackView));

        SerializedObject stackObject = new SerializedObject(stackView);
        stackObject.FindProperty("_useConfiguredPositions").boolValue = true;
        stackObject.FindProperty("_configuredActiveLocalPosition").vector3Value = shelf.Layers[0].transform.localPosition;
        stackObject.FindProperty("_configuredPreviewLocalPosition").vector3Value = shelf.Layers[1].transform.localPosition;
        stackObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Dictionary<int, ShelfLayer> CreateManualLayerPrefabs(IReadOnlyList<Shelf> shelves)
    {
        Dictionary<int, ShelfLayer> prefabs = new Dictionary<int, ShelfLayer>();
        ShelfLayer threeSlotSource = shelves.First(shelf => shelf.Capacity == Shelf.MinimumMatchCapacity).Layers[0];

        for (int capacity = Shelf.MinimumCapacity; capacity <= Shelf.MaximumCapacity; capacity++)
        {
            ShelfLayer sourceLayer = shelves
                .FirstOrDefault(shelf => shelf.Capacity == capacity)
                ?.Layers[0] ?? threeSlotSource;
            string path = GetLayerPrefabPath(capacity);
            prefabs.Add(capacity, SaveLayerPrefab(sourceLayer, capacity, path));
        }

        return prefabs;
    }

    private static ShelfLayer SaveLayerPrefab(ShelfLayer sourceLayer, int capacity, string path)
    {
        GameObject cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cell.prefab");
        ShelfLayerView sourceView = sourceLayer.GetComponent<ShelfLayerView>();

        if (cellPrefab == null || sourceView == null)
            throw new InvalidOperationException(path);

        GameObject layerObject = new GameObject($"ShelfLayer_{capacity:00}");

        try
        {
            ShelfLayer layer = layerObject.AddComponent<ShelfLayer>();
            ShelfLayerView layerView = layerObject.AddComponent<ShelfLayerView>();
            List<ShelfSlot> slots = new List<ShelfSlot>(capacity);
            Vector3[] positions = GetSlotPositions(sourceLayer, capacity);

            for (int slotIndex = 0; slotIndex < capacity; slotIndex++)
            {
                GameObject slotObject = PrefabUtility.InstantiatePrefab(cellPrefab, layerObject.transform) as GameObject;

                if (slotObject == null)
                    throw new InvalidOperationException(nameof(slotObject));

                Transform slotTransform = slotObject.transform;
                slotTransform.localPosition = positions[slotIndex];
                slotTransform.localRotation = Quaternion.identity;
                slotTransform.localScale = Vector3.one;
                slots.Add(slotObject.GetComponent<ShelfSlot>());
            }

            SetArrayReferences(layer, "_slots", slots.Cast<Object>().ToArray());
            SetReference(layerView, "_previewMaterial", GetReference<Material>(sourceView, "_previewMaterial"));

            GameObject prefabObject = PrefabUtility.SaveAsPrefabAsset(layerObject, path);
            ShelfLayer prefab = prefabObject != null ? prefabObject.GetComponent<ShelfLayer>() : null;

            if (prefab == null || prefab.Capacity != capacity)
                throw new InvalidOperationException(path);

            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(layerObject);
        }
    }

    private static Vector3[] GetSlotPositions(ShelfLayer sourceLayer, int capacity)
    {
        if (sourceLayer.Capacity == capacity)
            return sourceLayer.Slots.Select(slot => slot.transform.localPosition).ToArray();

        if (capacity == 2)
            return new[] { new Vector3(-0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f) };

        throw new InvalidOperationException(nameof(capacity));
    }

    private static string GetLayerPrefabPath(int capacity)
    {
        return capacity switch
        {
            1 => OneSlotLayerPrefabPath,
            2 => TwoSlotLayerPrefabPath,
            3 => LayerPrefabPath,
            4 => FourSlotLayerPrefabPath,
            5 => FiveSlotLayerPrefabPath,
            _ => throw new ArgumentOutOfRangeException(nameof(capacity))
        };
    }

    private static void ValidateManualLayout(
        Scene scene,
        IReadOnlyList<Shelf> shelves,
        IReadOnlyDictionary<int, ShelfLayer> layerPrefabs)
    {
        HashSet<ShelfLayer> layers = new HashSet<ShelfLayer>();
        HashSet<ShelfSlot> slots = new HashSet<ShelfSlot>();
        HashSet<Transform> anchors = new HashSet<Transform>();

        foreach (Shelf shelf in shelves)
        {
            shelf.ValidateLayers();
            Transform dynamicRoot = GetReference<Transform>(shelf, "_dynamicLayerRoot");

            if (dynamicRoot == null)
                throw new InvalidOperationException(shelf.name);

            foreach (ShelfLayer layer in shelf.Layers)
            {
                if (layer.transform.parent != dynamicRoot || !layers.Add(layer))
                    throw new InvalidOperationException(layer.name);

                foreach (ShelfSlot slot in layer.Slots)
                {
                    Transform anchor = GetReference<Transform>(slot, "_itemAnchor");

                    if (!slots.Add(slot) || anchor == null || !anchors.Add(anchor))
                        throw new InvalidOperationException(slot.name);

                    if (slot.GetComponent<Collider>() == null)
                        throw new InvalidOperationException(slot.name);
                }
            }
        }

        for (int capacity = Shelf.MinimumCapacity; capacity <= Shelf.MaximumCapacity; capacity++)
        {
            if (!layerPrefabs.TryGetValue(capacity, out ShelfLayer prefab) || prefab.Capacity != capacity)
                throw new InvalidOperationException(GetLayerPrefabPath(capacity));
        }

        FindSingle<Camera>(scene);
        FindSingle<ShelfBoard>(scene);
        FindSingle<ShelfLayerPool>(scene);
        FindSingle<ShelfItemPool>(scene);
    }

    private static void BuildAssets(bool rebuild)
    {
        Scene activeScene = SceneManager.GetActiveScene();

        try
        {
            EnsureFolders();
            EndlessGenerationConfig generationConfig = CreateGenerationConfig(rebuild);
            EndlessItemCatalog itemCatalog = CreateItemCatalog(rebuild);
            BuildEndlessScene(generationConfig, itemCatalog, rebuild);
            EndlessModeCardView cardPrefab = BuildEndlessCard(rebuild);
            ConfigureMainMenu(cardPrefab);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateAssets();
            Debug.Log("Endless mode assets created.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (activeScene.IsValid() && activeScene.isLoaded)
                SceneManager.SetActiveScene(activeScene);
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Settings", "Endless");
        EnsureFolder("Assets/Prefabs", "Endless");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static EndlessGenerationConfig CreateGenerationConfig(bool rebuild)
    {
        EndlessGenerationConfig config = AssetDatabase.LoadAssetAtPath<EndlessGenerationConfig>(GenerationConfigPath);

        if (config != null && !rebuild)
            return config;

        if (config != null)
            AssetDatabase.DeleteAsset(GenerationConfigPath);

        config = ScriptableObject.CreateInstance<EndlessGenerationConfig>();
        AssetDatabase.CreateAsset(config, GenerationConfigPath);
        return config;
    }

    private static EndlessItemCatalog CreateItemCatalog(bool rebuild)
    {
        EndlessItemCatalog catalog = AssetDatabase.LoadAssetAtPath<EndlessItemCatalog>(ItemCatalogPath);

        if (catalog != null && !rebuild)
            return catalog;

        if (catalog != null)
            AssetDatabase.DeleteAsset(ItemCatalogPath);

        catalog = ScriptableObject.CreateInstance<EndlessItemCatalog>();
        SerializedObject serializedCatalog = new SerializedObject(catalog);
        SerializedProperty entries = serializedCatalog.FindProperty("_entries");
        (ItemType type, string path)[] itemPrefabs =
        {
            (ItemType.Ball, "Assets/Prefabs/Items/Ball.prefab"),
            (ItemType.Bear, "Assets/Prefabs/Items/TeddyBear.prefab"),
            (ItemType.Plant, "Assets/Prefabs/Items/Plant.prefab"),
            (ItemType.Lamp, "Assets/Prefabs/Items/Lamp.prefab")
        };

        entries.arraySize = itemPrefabs.Length;

        for (int index = 0; index < itemPrefabs.Length; index++)
        {
            ShelfItem prefab = AssetDatabase.LoadAssetAtPath<ShelfItem>(itemPrefabs[index].path);

            if (prefab == null)
                throw new InvalidOperationException(itemPrefabs[index].path);

            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("_type").enumValueIndex = (int)itemPrefabs[index].type;
            entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("_weight").floatValue = 1f;
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(catalog, ItemCatalogPath);
        return catalog;
    }

    private static void BuildEndlessScene(EndlessGenerationConfig generationConfig, EndlessItemCatalog itemCatalog, bool rebuild)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EndlessScenePath) != null)
        {
            if (!rebuild)
                return;

            AssetDatabase.DeleteAsset(EndlessScenePath);
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, EndlessScenePath))
            throw new InvalidOperationException(EndlessScenePath);

        Scene endlessScene = EditorSceneManager.OpenScene(EndlessScenePath, OpenSceneMode.Additive);

        try
        {
            SceneManager.SetActiveScene(endlessScene);
            ConfigureEndlessScene(endlessScene, generationConfig, itemCatalog);
            EditorSceneManager.MarkSceneDirty(endlessScene);
            EditorSceneManager.SaveScene(endlessScene);
        }
        finally
        {
            EditorSceneManager.CloseScene(endlessScene, true);
        }
    }

    private static void ConfigureEndlessScene(Scene scene, EndlessGenerationConfig generationConfig, EndlessItemCatalog itemCatalog)
    {
        ShelfBoard shelfBoard = FindSingle<ShelfBoard>(scene);
        List<Shelf> shelves = shelfBoard.Shelves.ToList();
        Material woodMaterial = FindWoodMaterial(scene);
        Shelf sourceShelf = shelves.First(shelf => shelf.Capacity == Shelf.MinimumMatchCapacity);
        ShelfLayer layerPrefab = CreateLayerPrefab(sourceShelf, true);
        List<Vector3> shelfCenters = shelves.Select(GetShelfCenter).ToList();

        for (int index = 0; index < shelves.Count; index++)
            ConfigureDynamicShelf(shelves[index]);

        Transform geometryRoot = new GameObject("EndlessShelfGeometry").transform;
        SceneManager.MoveGameObjectToScene(geometryRoot.gameObject, scene);

        Shelf firstExtraShelf = CreateExtraShelf(shelves, shelfCenters, true, geometryRoot, woodMaterial);
        Shelf secondExtraShelf = CreateExtraShelf(shelves, shelfCenters, false, geometryRoot, woodMaterial);
        shelves.Add(firstExtraShelf);
        shelves.Add(secondExtraShelf);

        SetArrayReferences(shelfBoard, "_shelves", shelves.Cast<Object>().ToArray());

        Camera camera = FindSingle<Camera>(scene);
        camera.transform.position -= camera.transform.forward * 1.8f;

        LevelSession levelSession = FindSingle<LevelSession>(scene);
        MoveResolutionPlayer moveResolutionPlayer = FindSingle<MoveResolutionPlayer>(scene);
        ShelfItemDragController dragController = FindSingle<ShelfItemDragController>(scene);
        MatchRewardController rewardController = FindSingle<MatchRewardController>(scene);
        BonusUseController bonusUseController = FindSingle<BonusUseController>(scene);
        ComboSystem comboSystem = FindSingle<ComboSystem>(scene);
        ScoreSystem scoreSystem = FindSingle<ScoreSystem>(scene);
        TimerFreezeBonusEffect freezeEffect = FindSingle<TimerFreezeBonusEffect>(scene);
        LevelHudView hudView = FindSingle<LevelHudView>(scene);
        PauseWindowView pauseWindowView = FindSingle<PauseWindowView>(scene);
        LevelCompletionView levelCompletionView = FindSingle<LevelCompletionView>(scene);

        GameObject systemsObject = new GameObject("EndlessSystems");
        SceneManager.MoveGameObjectToScene(systemsObject, scene);

        Transform poolRoot = new GameObject("Pools").transform;
        poolRoot.SetParent(systemsObject.transform, false);

        Transform layerPoolRoot = new GameObject("LayerPool").transform;
        layerPoolRoot.SetParent(poolRoot, false);

        Transform itemPoolRoot = new GameObject("ItemPool").transform;
        itemPoolRoot.SetParent(poolRoot, false);

        EndlessTimer timer = systemsObject.AddComponent<EndlessTimer>();
        ConfigureTimer(timer);

        ShelfLayerPool layerPool = systemsObject.AddComponent<ShelfLayerPool>();
        SetReference(layerPool, "_threeSlotLayerPrefab", layerPrefab);
        SetReference(layerPool, "_poolRoot", layerPoolRoot);

        ShelfItemPool itemPool = systemsObject.AddComponent<ShelfItemPool>();
        SetReference(itemPool, "_catalog", itemCatalog);
        SetReference(itemPool, "_poolRoot", itemPoolRoot);

        EndlessBoardRefiller refiller = systemsObject.AddComponent<EndlessBoardRefiller>();
        SetReference(refiller, "_shelfBoard", shelfBoard);
        SetReference(refiller, "_generationConfig", generationConfig);
        SetReference(refiller, "_itemCatalog", itemCatalog);
        SetReference(refiller, "_layerPool", layerPool);
        SetReference(refiller, "_itemPool", itemPool);

        EndlessSession endlessSession = systemsObject.AddComponent<EndlessSession>();
        SetReference(endlessSession, "_shelfBoard", shelfBoard);
        SetReference(endlessSession, "_moveResolutionPlayer", moveResolutionPlayer);
        SetReference(endlessSession, "_boardRefiller", refiller);
        SetReference(endlessSession, "_timer", timer);

        EndlessProgressService progress = systemsObject.AddComponent<EndlessProgressService>();
        EndlessResultRecorder resultRecorder = systemsObject.AddComponent<EndlessResultRecorder>();
        SetReference(resultRecorder, "_session", endlessSession);
        SetReference(resultRecorder, "_scoreSystem", scoreSystem);
        SetReference(resultRecorder, "_progress", progress);

        SetReference(dragController, "_levelSession", endlessSession);
        SetReference(rewardController, "_levelSession", endlessSession);
        SetReference(bonusUseController, "_session", endlessSession);
        SetBoolean(bonusUseController, "_ignoreLevelUseLimit", true);
        SetReference(freezeEffect, "_primaryTarget", comboSystem);
        SetArrayReferences(freezeEffect, "_additionalTargets", new Object[] { timer });

        EndlessResultView resultView = ConvertResultView(levelCompletionView);
        Canvas canvas = FindSingle<Canvas>(scene);
        EndlessTimerView timerView = CreateTimerView(canvas.transform, timer);
        CreateDebugView(canvas.transform, endlessSession, timer, refiller);

        LevelUiController levelUiController = FindSingle<LevelUiController>(scene);
        GameObject uiControllerObject = levelUiController.gameObject;
        Object.DestroyImmediate(levelUiController);

        EndlessUiController uiController = uiControllerObject.AddComponent<EndlessUiController>();
        SetReference(uiController, "_hudView", hudView);
        SetReference(uiController, "_pauseWindowView", pauseWindowView);
        SetReference(uiController, "_resultView", resultView);
        SetReference(uiController, "_session", endlessSession);
        SetReference(uiController, "_resultRecorder", resultRecorder);

        RemoveComponentIfPresent<LevelBuilder>(scene);
        RemoveComponentIfPresent<LevelResultRecorder>(scene);
        RemoveComponentIfPresent<LevelProgressService>(scene);
        Object.DestroyImmediate(levelSession);

        timerView.gameObject.name = "EndlessTimer";
        ValidateScene(scene, layerPrefab);
    }

    private static ShelfLayer CreateLayerPrefab(Shelf sourceShelf, bool recreate)
    {
        ShelfLayer existingPrefab = AssetDatabase.LoadAssetAtPath<ShelfLayer>(LayerPrefabPath);

        if (existingPrefab != null && !recreate)
            return existingPrefab;

        if (existingPrefab != null)
            AssetDatabase.DeleteAsset(LayerPrefabPath);

        if (sourceShelf.Layers.Count == 0)
            throw new InvalidOperationException(nameof(sourceShelf.Layers));

        ShelfLayer sourceLayer = sourceShelf.Layers[0];
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sourceLayer.gameObject, LayerPrefabPath);
        ShelfLayer layerPrefab = prefab.GetComponent<ShelfLayer>();

        if (layerPrefab == null || layerPrefab.Slots.Count != Shelf.MinimumMatchCapacity)
            throw new InvalidOperationException(LayerPrefabPath);

        return layerPrefab;
    }

    private static void ConfigureDynamicShelf(Shelf shelf)
    {
        if (shelf.Layers.Count == 0)
            throw new InvalidOperationException(shelf.name);

        Vector3 activePosition = shelf.Layers[0].transform.localPosition;
        Vector3 previewPosition = shelf.Layers.Count > 1
            ? shelf.Layers[1].transform.localPosition
            : activePosition + Vector3.forward * 0.2f;

        ShelfLayer[] layers = shelf.Layers.ToArray();

        foreach (ShelfLayer layer in layers)
            Object.DestroyImmediate(layer.gameObject);

        SerializedObject shelfObject = new SerializedObject(shelf);
        shelfObject.FindProperty("_shelfLayers").arraySize = 0;

        Transform dynamicRoot = new GameObject("DynamicLayers").transform;
        dynamicRoot.SetParent(shelf.transform, false);
        shelfObject.FindProperty("_dynamicLayerRoot").objectReferenceValue = dynamicRoot;
        shelfObject.ApplyModifiedPropertiesWithoutUndo();

        ShelfLayerStackView stackView = shelf.GetComponent<ShelfLayerStackView>();
        SerializedObject stackObject = new SerializedObject(stackView);
        stackObject.FindProperty("_useConfiguredPositions").boolValue = true;
        stackObject.FindProperty("_configuredActiveLocalPosition").vector3Value = activePosition;
        stackObject.FindProperty("_configuredPreviewLocalPosition").vector3Value = previewPosition;
        stackObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Shelf CreateExtraShelf(
        IReadOnlyList<Shelf> shelves,
        IReadOnlyList<Vector3> centers,
        bool useTopRow,
        Transform geometryRoot,
        Material woodMaterial)
    {
        List<float> rowHeights = centers.Select(center => center.y).Distinct(new FloatComparer(0.15f)).OrderByDescending(value => value).ToList();
        int rowIndex = useTopRow ? 0 : Math.Min(2, rowHeights.Count - 1);
        float rowHeight = rowHeights[rowIndex];
        List<int> rowShelves = Enumerable.Range(0, centers.Count)
            .Where(index => Mathf.Abs(centers[index].y - rowHeight) < 0.15f)
            .OrderBy(index => centers[index].x)
            .ToList();

        if (rowShelves.Count < 2)
            throw new InvalidOperationException(nameof(rowShelves));

        float spacing = Mathf.Abs(centers[rowShelves[1]].x - centers[rowShelves[0]].x);
        int templateIndex = useTopRow ? rowShelves[0] : rowShelves[rowShelves.Count - 1];
        Shelf template = shelves[templateIndex];
        Shelf clone = Object.Instantiate(template, template.transform.parent);
        clone.name = useTopRow ? "Shelf_Endless_Top" : "Shelf_Endless_Lower";

        Vector3 cloneCenter = GetShelfCenterFromConfiguredPosition(clone);
        Vector3 desiredCenter = centers[templateIndex] + Vector3.right * (useTopRow ? -spacing : spacing);
        clone.transform.position += desiredCenter - cloneCenter;

        GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.name = $"{clone.name}_Board";
        plank.transform.SetParent(geometryRoot, true);
        plank.transform.position = desiredCenter + Vector3.down * 0.58f + Vector3.forward * 0.12f;
        plank.transform.localScale = new Vector3(spacing * 0.85f, 0.08f, 0.65f);

        Collider plankCollider = plank.GetComponent<Collider>();

        if (plankCollider != null)
            Object.DestroyImmediate(plankCollider);

        MeshRenderer renderer = plank.GetComponent<MeshRenderer>();

        if (woodMaterial != null)
            renderer.sharedMaterial = woodMaterial;

        return clone;
    }

    private static Vector3 GetShelfCenter(Shelf shelf)
    {
        if (!shelf.HasActiveLayer)
            return shelf.transform.position;

        Vector3 center = Vector3.zero;

        foreach (ShelfSlot slot in shelf.ActiveLayer.Slots)
            center += slot.transform.position;

        return center / shelf.ActiveLayer.Slots.Count;
    }

    private static Vector3 GetShelfCenterFromConfiguredPosition(Shelf shelf)
    {
        Transform dynamicRoot = GetReference<Transform>(shelf, "_dynamicLayerRoot");
        return dynamicRoot != null ? dynamicRoot.position : shelf.transform.position;
    }

    private static Material FindWoodMaterial(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.name.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0)
                        return material;
                }
            }
        }

        return null;
    }

    private static void ConfigureTimer(EndlessTimer timer)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(5f, 1.15f),
            new Keyframe(10f, 1.4f),
            new Keyframe(20f, 1.9f),
            new Keyframe(30f, 2.6f),
            new Keyframe(45f, 3.4f),
            new Keyframe(70f, 4.2f));

        for (int index = 0; index < curve.length; index++)
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);

        for (int index = 0; index < curve.length; index++)
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);

        SerializedObject timerObject = new SerializedObject(timer);
        timerObject.FindProperty("_startingTime").floatValue = 35f;
        timerObject.FindProperty("_maximumTime").floatValue = 45f;
        timerObject.FindProperty("_matchTimeReward").floatValue = 2.5f;
        timerObject.FindProperty("_drainMultiplierByMatchCount").animationCurveValue = curve;
        timerObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static EndlessResultView ConvertResultView(LevelCompletionView sourceView)
    {
        Button restartButton = GetReference<Button>(sourceView, "_restartButton");
        Button menuButton = GetReference<Button>(sourceView, "_menuButton");
        TMP_Text finalScoreText = GetReference<TMP_Text>(sourceView, "_finalScoreResult");
        Button reviewButton = GetReference<Button>(sourceView, "_reviewButton");
        GameObject root = sourceView.gameObject;
        TMP_FontAsset font = finalScoreText.font;

        if (reviewButton != null)
            reviewButton.gameObject.SetActive(false);

        LocalizedTextView title = root.GetComponentsInChildren<LocalizedTextView>(true).FirstOrDefault();

        if (title != null)
            ConfigureLocalizedText(title, "ВРЕМЯ ВЫШЛО", "TIME'S UP", "SÜRE DOLDU");

        TMP_Text bestLabel = CreateText(root.transform, "BestScoreLabel", font, 28f, TextAlignmentOptions.Center);
        SetRect(bestLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(280f, 45f), new Vector2(0f, -45f));
        ConfigureLocalizedText(bestLabel.gameObject.AddComponent<LocalizedTextView>(), "ЛУЧШИЙ СЧЁТ", "BEST SCORE", "EN İYİ SKOR");

        TMP_Text bestValue = CreateText(root.transform, "BestScoreValue", font, 34f, TextAlignmentOptions.Center);
        SetRect(bestValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(280f, 50f), new Vector2(0f, -90f));

        Object.DestroyImmediate(sourceView);
        EndlessResultView resultView = root.AddComponent<EndlessResultView>();
        SetReference(resultView, "_restartButton", restartButton);
        SetReference(resultView, "_menuButton", menuButton);
        SetReference(resultView, "_scoreText", finalScoreText);
        SetReference(resultView, "_bestScoreText", bestValue);
        return resultView;
    }

    private static EndlessTimerView CreateTimerView(Transform canvas, EndlessTimer timer)
    {
        TMP_FontAsset font = canvas.GetComponentsInChildren<TMP_Text>(true).First(text => text.font != null).font;

        GameObject vignetteObject = CreateUiObject("EndlessVignette", canvas);
        vignetteObject.transform.SetAsFirstSibling();
        Image vignette = vignetteObject.AddComponent<Image>();
        vignette.raycastTarget = false;
        vignette.color = Color.clear;
        Stretch(vignette.rectTransform);

        GameObject timerObject = CreateUiObject("EndlessTimer", canvas);
        RectTransform timerRect = timerObject.GetComponent<RectTransform>();
        SetRect(timerRect, new Vector2(0.5f, 1f), new Vector2(260f, 82f), new Vector2(0f, -18f));

        Image frame = timerObject.AddComponent<Image>();
        frame.color = new Color(0.28f, 0.12f, 0.06f, 0.95f);

        TMP_Text seconds = CreateText(timerObject.transform, "Seconds", font, 42f, TextAlignmentOptions.Center);
        Stretch(seconds.rectTransform);

        TMP_Text addedTime = CreateText(timerObject.transform, "AddedTime", font, 30f, TextAlignmentOptions.Center);
        SetRect(addedTime.rectTransform, new Vector2(1f, 0.5f), new Vector2(110f, 50f), new Vector2(110f, 0f));
        addedTime.fontStyle = FontStyles.Bold;
        addedTime.raycastTarget = false;
        addedTime.gameObject.SetActive(false);

        GameObject progressObject = CreateUiObject("Progress", timerObject.transform);
        Image progress = progressObject.AddComponent<Image>();
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        SetRect(progress.rectTransform, new Vector2(0.5f, 0f), new Vector2(220f, 10f), new Vector2(0f, 10f));

        EndlessTimerView timerView = timerObject.AddComponent<EndlessTimerView>();
        SetReference(timerView, "_timer", timer);
        SetReference(timerView, "_secondsText", seconds);
        SetReference(timerView, "_addedTimeText", addedTime);
        SetReference(timerView, "_progressImage", progress);
        SetReference(timerView, "_frameImage", frame);
        SetReference(timerView, "_vignetteImage", vignette);
        return timerView;
    }

    private static void CreateDebugView(
        Transform canvas,
        EndlessSession session,
        EndlessTimer timer,
        EndlessBoardRefiller refiller)
    {
        TMP_FontAsset font = canvas.GetComponentsInChildren<TMP_Text>(true).First(text => text.font != null).font;
        TMP_Text text = CreateText(canvas, "EndlessDebug", font, 16f, TextAlignmentOptions.TopLeft);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(260f, 160f);
        rect.anchoredPosition = new Vector2(12f, -110f);

        EndlessDebugView debugView = text.gameObject.AddComponent<EndlessDebugView>();
        SetReference(debugView, "_session", session);
        SetReference(debugView, "_timer", timer);
        SetReference(debugView, "_refiller", refiller);
        SetReference(debugView, "_text", text);
    }

    private static EndlessModeCardView BuildEndlessCard(bool rebuild)
    {
        EndlessModeCardView existing = AssetDatabase.LoadAssetAtPath<EndlessModeCardView>(EndlessCardPrefabPath);

        if (existing != null && !rebuild)
            return existing;

        if (existing != null)
            AssetDatabase.DeleteAsset(EndlessCardPrefabPath);

        GameObject contents = PrefabUtility.LoadPrefabContents(LevelCardPrefabPath);

        try
        {
            LevelCardView levelCard = contents.GetComponent<LevelCardView>();

            if (levelCard == null)
                throw new InvalidOperationException(LevelCardPrefabPath);

            Button button = GetReference<Button>(levelCard, "_button");
            TMP_Text title = GetReference<TMP_Text>(levelCard, "_levelNumberText");
            GameObject recordRoot = GetReference<GameObject>(levelCard, "_recordRoot");
            TMP_Text recordValue = GetReference<TMP_Text>(levelCard, "_recordValueText");
            GameObject lockIcon = GetReference<GameObject>(levelCard, "_lockIcon");

            Object.DestroyImmediate(levelCard);
            title.enableAutoSizing = true;
            title.fontSizeMin = 12f;
            title.fontSizeMax = 30f;
            title.text = "ENDLESS MODE";

            LocalizedTextView localizedTitle = title.GetComponent<LocalizedTextView>();

            if (localizedTitle == null)
                localizedTitle = title.gameObject.AddComponent<LocalizedTextView>();

            ConfigureLocalizedText(localizedTitle, "БЕСКОНЕЧНЫЙ РЕЖИМ", "ENDLESS MODE", "SONSUZ MOD");

            EndlessModeCardView endlessCard = contents.AddComponent<EndlessModeCardView>();
            SetReference(endlessCard, "_button", button);
            SetReference(endlessCard, "_bestScoreValueText", recordValue);
            SetReference(endlessCard, "_recordRoot", recordRoot);
            SetReference(endlessCard, "_lockIcon", lockIcon);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(contents, EndlessCardPrefabPath);
            return prefab.GetComponent<EndlessModeCardView>();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void ConfigureMainMenu(EndlessModeCardView cardPrefab)
    {
        Scene loadedScene = SceneManager.GetSceneByPath(MainMenuScenePath);

        if (loadedScene.IsValid() && loadedScene.isLoaded && loadedScene.isDirty)
            throw new InvalidOperationException("MainMenu has unsaved changes.");

        bool wasLoaded = loadedScene.IsValid() && loadedScene.isLoaded;
        Scene scene = wasLoaded ? loadedScene : EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);

        try
        {
            LevelSelectionController selectionController = FindSingle<LevelSelectionController>(scene);
            EndlessProgressService progress = FindAll<EndlessProgressService>(scene).FirstOrDefault();

            if (progress == null)
                progress = selectionController.gameObject.AddComponent<EndlessProgressService>();

            SetReference(selectionController, "_endlessCardView", cardPrefab);
            SetReference(selectionController, "_endlessProgress", progress);
            SetString(selectionController, "_endlessSceneName", "EndlessLevel");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

        if (scenes.All(scene => scene.path != EndlessScenePath))
            scenes.Add(new EditorBuildSettingsScene(EndlessScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void ValidateAssets()
    {
        ShelfLayer layerPrefab = AssetDatabase.LoadAssetAtPath<ShelfLayer>(LayerPrefabPath);

        if (layerPrefab == null || layerPrefab.Slots.Count != Shelf.MinimumMatchCapacity)
            throw new InvalidOperationException(LayerPrefabPath);

        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.path == EndlessScenePath)
        {
            ValidateScene(activeScene, layerPrefab);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(EndlessScenePath, OpenSceneMode.Additive);

        try
        {
            ValidateScene(scene, layerPrefab);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ValidateScene(Scene scene, ShelfLayer layerPrefab)
    {
        ShelfBoard board = FindSingle<ShelfBoard>(scene);
        GameObject systems = scene.GetRootGameObjects().Single(root => root.name == "EndlessSystems");
        bool isWaitingForManualLayout = FindAll<Transform>(scene).Any(transform => transform.name == "EndlessLayout") && !systems.activeSelf;

        if (isWaitingForManualLayout)
        {
            if (board.Shelves.Count != 0)
                throw new InvalidOperationException($"Endless shelf count: {board.Shelves.Count}");

            ValidateRequiredComponents(scene, layerPrefab);
            return;
        }

        if (board.Shelves.Count == 0)
            throw new InvalidOperationException($"Endless shelf count: {board.Shelves.Count}");

        if (board.Shelves.Distinct().Count() != board.Shelves.Count)
            throw new InvalidOperationException(nameof(board.Shelves));

        foreach (Shelf shelf in board.Shelves)
        {
            if (shelf == null || !Shelf.IsValidCapacity(shelf.Capacity) || GetReference<Transform>(shelf, "_dynamicLayerRoot") == null)
                throw new InvalidOperationException(nameof(shelf));

            shelf.ValidateLayers();

            if (shelf.Layers.Any(layer => !layer.IsEmpty))
                throw new InvalidOperationException(nameof(shelf.Layers));
        }

        if (board.Shelves.All(shelf => shelf.Capacity != Shelf.MinimumMatchCapacity))
            throw new InvalidOperationException("Для бесконечного режима нужна хотя бы одна полка вместимостью 3.");

        Dictionary<int, ShelfLayer> layerPrefabs = new Dictionary<int, ShelfLayer>();

        for (int capacity = Shelf.MinimumCapacity; capacity <= Shelf.MaximumCapacity; capacity++)
        {
            string prefabPath = GetLayerPrefabPath(capacity);
            ShelfLayer prefab = AssetDatabase.LoadAssetAtPath<ShelfLayer>(prefabPath);

            if (prefab == null || prefab.Capacity != capacity)
                throw new InvalidOperationException(prefabPath);

            layerPrefabs.Add(capacity, prefab);
        }

        ValidateManualLayout(scene, board.Shelves, layerPrefabs);

        ShelfLayerPool layerPool = FindSingle<ShelfLayerPool>(scene);
        ShelfItemDragController dragController = FindSingle<ShelfItemDragController>(scene);
        GameObject layoutRoot = FindSingleGameObject(scene, "EndlessLayout");

        if (!layoutRoot.transform.IsChildOf(dragController.transform))
            throw new InvalidOperationException(nameof(layoutRoot));

        if (GetReference<ShelfLayer>(layerPool, "_oneSlotLayerPrefab") != layerPrefabs[1] ||
            GetReference<ShelfLayer>(layerPool, "_twoSlotLayerPrefab") != layerPrefabs[2] ||
            GetReference<ShelfLayer>(layerPool, "_threeSlotLayerPrefab") != layerPrefabs[3] ||
            GetReference<ShelfLayer>(layerPool, "_fourSlotLayerPrefab") != layerPrefabs[4] ||
            GetReference<ShelfLayer>(layerPool, "_fiveSlotLayerPrefab") != layerPrefabs[5])
            throw new InvalidOperationException(nameof(layerPool));

        ValidateRequiredComponents(scene, layerPrefab);
    }

    private static void ValidateRequiredComponents(Scene scene, ShelfLayer layerPrefab)
    {
        if (layerPrefab.Slots.Count != Shelf.MinimumMatchCapacity)
            throw new InvalidOperationException(nameof(layerPrefab));

        FindSingle<Camera>(scene);
        FindSingle<EndlessSession>(scene);
        FindSingle<EndlessBoardRefiller>(scene);
        FindSingle<EndlessTimer>(scene);
        FindSingle<ShelfLayerPool>(scene);
        FindSingle<ShelfItemPool>(scene);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureLocalizedText(LocalizedTextView view, string russian, string english, string turkish)
    {
        SetString(view, "_russianText", russian);
        SetString(view, "_englishText", english);
        SetString(view, "_turkishText", turkish);
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{propertyName}");

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetReference<T>(Object target, string propertyName) where T : Object
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{propertyName}");

        return property.objectReferenceValue as T;
    }

    private static void SetArrayReferences(Object target, string propertyName, IReadOnlyList<Object> values)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{propertyName}");

        property.arraySize = values.Count;

        for (int index = 0; index < values.Count; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{propertyName}");

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{propertyName}");

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{propertyName}");

        property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T FindSingle<T>(Scene scene) where T : Component
    {
        T[] components = FindAll<T>(scene);

        if (components.Length != 1)
            throw new InvalidOperationException($"{typeof(T).Name}: {components.Length}");

        return components[0];
    }

    private static GameObject FindSingleGameObject(Scene scene, string name)
    {
        Transform[] transforms = FindAll<Transform>(scene)
            .Where(transform => transform.name == name)
            .ToArray();

        if (transforms.Length != 1)
            throw new InvalidOperationException($"{name}: {transforms.Length}");

        return transforms[0].gameObject;
    }

    private static T[] FindAll<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static void RemoveComponentIfPresent<T>(Scene scene) where T : Component
    {
        foreach (T component in FindAll<T>(scene))
            Object.DestroyImmediate(component);
    }

    private sealed class FloatComparer : IEqualityComparer<float>
    {
        private readonly float _tolerance;

        public FloatComparer(float tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(float first, float second)
        {
            return Mathf.Abs(first - second) < _tolerance;
        }

        public int GetHashCode(float value)
        {
            return Mathf.RoundToInt(value / _tolerance);
        }
    }
}
