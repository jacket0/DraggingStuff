using System;
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
    private static readonly Vector3 SpotScale = new Vector3(0.17f, 0.035f, 0.13f);
    private static readonly Vector3 ForecastDropScale = new Vector3(0.06f, 0.06f, 0.06f);
    private static readonly Vector3 ForecastTextPosition = new Vector3(0f, 0.07f, 0f);
    private static readonly Color SlimeLime = new Color(0.55f, 0.9f, 0.15f, 0.72f);
    private static readonly Color ForecastLime = new Color(0.55f, 0.9f, 0.15f, 0.45f);

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

        Assign(boardView, "_shelfBoard", FindSingle<ShelfBoard>());
        Assign(boardView, "_spotPrefab", spotPrefab);
        Assign(boardView, "_forecast", forecast);
        Assign(controller, "_session", FindSingle<LevelSession>());
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
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = "Blob";
            blob.transform.SetParent(root.transform, false);
            blob.transform.localScale = SpotScale;
            UnityEngine.Object.DestroyImmediate(blob.GetComponent<Collider>());
            MeshRenderer renderer = blob.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            SlimeSpotView spot = root.AddComponent<SlimeSpotView>();
            Assign(spot, "_blob", blob.transform);
            PrefabUtility.SaveAsPrefabAsset(root, SpotPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
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
        TextMeshPro countdown = text.AddComponent<TextMeshPro>();
        countdown.font = Load<TMP_FontAsset>(FontPath);
        countdown.fontSize = 0.8f;
        countdown.alignment = TextAlignmentOptions.Center;
        countdown.color = Color.white;
        countdown.rectTransform.sizeDelta = new Vector2(0.3f, 0.15f);
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
