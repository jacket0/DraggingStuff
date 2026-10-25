using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class SlimeVisualsSetup
{
    private const string ScenePath = "Assets/Scenes/EighthLevel.unity";
    private const string MaterialFolder = "Assets/Materials/Slime";
    private const string PrefabFolder = "Assets/Prefabs/Slime";
    private const string SpotPrefabPath = PrefabFolder + "/SlimeSpot.prefab";
    private const string FontPath = "Assets/Fonts/Code New Roman SDF.asset";
    private const string ControllerName = "Slime";
    private const string ForecastName = "SlimeForecast";
    private const string SourceName = "SlimeSource";
    private const string SessionScriptPath = "Assets/Scripts/Slime/SlimeLevelSession.cs";
    private static readonly Vector3 BottomSourceScale = new Vector3(0.38f, 0.1f, 0.14f);
    private static readonly Vector3 SideSourceScale = new Vector3(0.12f, 0.24f, 0.16f);
    private static readonly Vector3 BottomSourceOffset = new Vector3(0f, -0.25f, 0.06f);
    private static readonly Vector3 ForecastOffset = new Vector3(0f, 0.2f, 0.14f);
    private const float SideSourceOffset = 0.2f;
    private static readonly Vector3 PuddleScale = new Vector3(0.29f, 0.05f, 0.24f);
    private static readonly Vector3 DripPosition = new Vector3(0f, -0.06f, 0.1f);
    private static readonly Vector3 DripScale = new Vector3(0.12f, 0.15f, 0.04f);
    private static readonly Vector3 ForecastDropScale = new Vector3(0.1f, 0.1f, 0.1f);
    private static readonly Vector3 ForecastTextPosition = new Vector3(0f, 0.12f, 0f);
    private static readonly Quaternion ForecastTextRotation = Quaternion.Euler(0f, 180f, 0f);
    private static readonly Color SlimeLime = new Color(0.6f, 1f, 0.1f, 0.85f);
    private static readonly Color ForecastLime = new Color(0.6f, 1f, 0.1f, 0.7f);

    [MenuItem("Tools/Slime/Setup Visuals")]
    public static void Setup()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        Material spotMaterial = CreateMaterial("SlimeSpot", SlimeLime);
        Material forecastMaterial = CreateMaterial("SlimeForecast", ForecastLime);
        CreateSpotPrefab(spotMaterial);
        AssetDatabase.SaveAssets();

        Scene scene = OpenScene();
        SlimeSpotView spotPrefab = Load<SlimeSpotView>(SpotPrefabPath);
        GameObject host = FindOrCreateRoot(ControllerName);
        SlimeBoardView boardView = GetOrAdd<SlimeBoardView>(host);
        SlimeController controller = GetOrAdd<SlimeController>(host);
        SlimeForecastView forecast = CreateForecast(host.transform, forecastMaterial);
        ShelfBoard board = FindSingle<ShelfBoard>();
        SlimeSourceView source = CreateSource(host.transform, board, spotMaterial);
        LevelSession session = EnsureSlimeSession();

        Assign(boardView, "_shelfBoard", FindSingle<ShelfBoard>());
        Assign(boardView, "_spotPrefab", spotPrefab);
        Assign(boardView, "_forecast", forecast);
        Assign(boardView, "_source", source);
        SerializedObject serializedBoardView = new SerializedObject(boardView);
        serializedBoardView.FindProperty("_forecastOffset").vector3Value = ForecastOffset;
        serializedBoardView.ApplyModifiedPropertiesWithoutUndo();
        Assign(session, "_slime", controller);
        Assign(controller, "_session", session);
        Assign(controller, "_shelfBoard", FindSingle<ShelfBoard>());
        Assign(controller, "_dragController", FindSingle<ShelfItemDragController>());
        Assign(controller, "_view", boardView);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("SLIME_VISUALS_PASS");
    }

    private static void CreateSpotPrefab(Material material)
    {
        GameObject root = new GameObject("SlimeSpot");

        try
        {
            GameObject blob = new GameObject("Blob");
            blob.transform.SetParent(root.transform, false);
            CreateBlobPart("Puddle", blob.transform, Vector3.zero, PuddleScale, material);
            CreateBlobPart("Drip", blob.transform, DripPosition, DripScale, material);

            SlimeSpotView spot = root.AddComponent<SlimeSpotView>();
            Assign(spot, "_blob", blob.transform);
            PrefabUtility.SaveAsPrefabAsset(root, SpotPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateBlobPart(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static SlimeSourceView CreateSource(Transform parent, ShelfBoard board, Material material)
    {
        Transform existing = parent.Find(SourceName);

        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        SlimeNeighborhood neighborhood = Load<SlimeNeighborhood>(SlimeNeighborhoodGeometry.NeighborhoodPath);
        board.Initialize();
        SlimeNeighborhoodGeometry.Validate(board, neighborhood);
        GameObject root = new GameObject(SourceName);
        root.transform.SetParent(parent, false);
        float bottomHeight = board.Shelves.Min(shelf => shelf.transform.position.y);
        float centerX = board.Shelves.Average(shelf => shelf.transform.position.x);
        List<Transform> blobs = new List<Transform>();

        foreach (ColumnPosition column in FindSourceColumns(board, bottomHeight))
        {
            Shelf shelf = board.Shelves[column.ShelfIndex];
            Vector3 anchor = shelf.ColumnViews[column.ColumnIndex].ItemAnchor.position;
            bool isBottom = Mathf.Abs(shelf.transform.position.y - bottomHeight) < 0.05f;
            Vector3 offset = isBottom ? BottomSourceOffset : new Vector3(Mathf.Sign(anchor.x - centerX) * SideSourceOffset, 0f, 0f);
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = $"SourceBlob_{column.ShelfIndex}_{column.ColumnIndex}";
            blob.transform.SetParent(root.transform, false);
            blob.transform.position = anchor + offset;
            blob.transform.localScale = isBottom ? BottomSourceScale : SideSourceScale;
            UnityEngine.Object.DestroyImmediate(blob.GetComponent<Collider>());
            MeshRenderer renderer = blob.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            blobs.Add(blob.transform);
        }

        SlimeSourceView source = root.AddComponent<SlimeSourceView>();
        SerializedObject serializedSource = new SerializedObject(source);
        SerializedProperty blobsProperty = serializedSource.FindProperty("_blobs");
        blobsProperty.arraySize = blobs.Count;

        for (int index = 0; index < blobs.Count; index++)
            blobsProperty.GetArrayElementAtIndex(index).objectReferenceValue = blobs[index];

        serializedSource.ApplyModifiedPropertiesWithoutUndo();
        return source;
    }

    private static IEnumerable<ColumnPosition> FindSourceColumns(ShelfBoard board, float bottomHeight)
    {
        ColumnPosition[] columns = board.Shelves
            .SelectMany((shelf, shelfIndex) => Enumerable.Range(0, shelf.Capacity).Select(columnIndex => new ColumnPosition(shelfIndex, columnIndex)))
            .ToArray();

        foreach (IGrouping<int, ColumnPosition> row in columns.GroupBy(column => Mathf.RoundToInt(board.Shelves[column.ShelfIndex].transform.position.y * 20f)))
        {
            ColumnPosition[] screenOrder = row.OrderByDescending(column => GetAnchor(board, column).x).ToArray();
            bool isBottom = Mathf.Abs(board.Shelves[screenOrder[0].ShelfIndex].transform.position.y - bottomHeight) < 0.05f;

            if (isBottom)
            {
                foreach (ColumnPosition column in screenOrder)
                    yield return column;

                continue;
            }

            yield return screenOrder[0];
            yield return screenOrder[screenOrder.Length - 1];
        }
    }

    private static Vector3 GetAnchor(ShelfBoard board, ColumnPosition column) => board.Shelves[column.ShelfIndex].ColumnViews[column.ColumnIndex].ItemAnchor.position;

    private static LevelSession EnsureSlimeSession()
    {
        LevelSession session = FindSingle<LevelSession>();

        if (session is SlimeLevelSession)
            return session;

        GameObject host = session.gameObject;
        SerializedObject serializedSession = new SerializedObject(session);
        serializedSession.FindProperty("m_Script").objectReferenceValue = Load<MonoScript>(SessionScriptPath);
        serializedSession.ApplyModifiedPropertiesWithoutUndo();
        SlimeLevelSession slimeSession = host.GetComponent<SlimeLevelSession>();

        if (slimeSession == null)
            throw new InvalidOperationException($"{host.name}: the level session could not be switched to {nameof(SlimeLevelSession)}.");

        return slimeSession;
    }

    private static SlimeForecastView CreateForecast(Transform parent, Material material)
    {
        Transform existing = parent.Find(ForecastName);

        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        GameObject root = new GameObject(ForecastName);
        root.transform.SetParent(parent, false);

        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        drop.name = "Drop";
        drop.transform.SetParent(root.transform, false);
        drop.transform.localScale = ForecastDropScale;
        UnityEngine.Object.DestroyImmediate(drop.GetComponent<Collider>());
        MeshRenderer renderer = drop.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        GameObject text = new GameObject("Countdown");
        text.transform.SetParent(root.transform, false);
        text.transform.localPosition = ForecastTextPosition;
        text.transform.localRotation = ForecastTextRotation;
        TextMeshPro countdown = text.AddComponent<TextMeshPro>();
        countdown.font = Load<TMP_FontAsset>(FontPath);
        countdown.fontSize = 1.6f;
        countdown.alignment = TextAlignmentOptions.Center;
        countdown.color = Color.white;
        countdown.rectTransform.sizeDelta = new Vector2(0.4f, 0.25f);
        countdown.text = "2";

        SlimeForecastView forecast = root.AddComponent<SlimeForecastView>();
        Assign(forecast, "_drop", drop.transform);
        Assign(forecast, "_countdownText", countdown);
        return forecast;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Specular");

        if (shader == null)
            throw new InvalidOperationException("The transparent specular shader was not found.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.color = color;
        material.SetColor("_SpecColor", new Color(0.9f, 1f, 0.8f));
        material.SetFloat("_Shininess", 0.15f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Scene OpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path == ScenePath)
            return scene;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            throw new OperationCanceledException("The scene switch was cancelled.");

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindOrCreateRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return new GameObject(name);
    }

    private static T GetOrAdd<T>(GameObject host) where T : Component
    {
        T component = host.GetComponent<T>();
        return component != null ? component : host.AddComponent<T>();
    }

    private static T FindSingle<T>() where T : UnityEngine.Object
    {
        T[] found = UnityEngine.Object.FindObjectsOfType<T>(true);

        if (found.Length != 1)
            throw new InvalidOperationException($"Expected exactly one {typeof(T).Name} in the scene, found {found.Length}.");

        return found[0];
    }

    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        if (value == null)
            throw new InvalidOperationException($"{target.name}.{field}: value is missing.");

        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);

        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name} has no field {field}.");

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
            throw new InvalidOperationException($"Asset {path} of type {typeof(T).Name} was not found.");

        return asset;
    }
}
