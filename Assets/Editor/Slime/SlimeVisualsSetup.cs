using System;
using System.Collections.Generic;
using System.Linq;
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
    private const string PuddleModelPath = "Assets/Models/Slime/SlimePuddle.fbx";
    private const string WallPuddleModelPath = "Assets/Models/Slime/SlimePuddleWall.fbx";
    private const string BridgeModelPath = "Assets/Models/Slime/SlimeBridge.fbx";
    private const string ForecastModelPath = "Assets/Models/Slime/SlimeForecastPuddle.fbx";
    private const string DigitsModelPath = "Assets/Models/Slime/SlimeDigits.fbx";
    private const string ControllerName = "Slime";
    private const string ForecastName = "SlimeForecast";
    private const string SourceName = "SlimeSource";
    private const string SplashName = "SlimeSplash";
    private const string SoundFolder = "Assets/Audio/Sounds/Slime";
    private const string SessionScriptPath = "Assets/Scripts/Slime/SlimeLevelSession.cs";
    private static readonly Vector3 SpotOffset = new Vector3(0f, -0.095f, -0.07f);
    private static readonly Vector3 ForecastOffset = new Vector3(0f, 0.004f, 0f);
    private static readonly Vector3 ForecastDigitPosition = new Vector3(0f, -0.119f, 0.185f);
    private static readonly Vector3 ForecastDigitScale = new Vector3(1.8f, 1.8f, 1.8f);
    private static readonly Vector3 DigitOutlineScale = new Vector3(1.15f, 1.08f, 1.6f);
    private static readonly Quaternion ForecastDigitRotation = Quaternion.identity;
    private static readonly Color SlimeBody = new Color(0.7f, 0.81f, 0.35f, 0.94f);
    private static readonly Color SlimeEdge = new Color(0.35f, 0.56f, 0.16f, 0.96f);
    private static readonly Color SlimeOutlineColor = new Color(0.16f, 0.33f, 0.06f, 1f);
    private static readonly Color DigitBody = new Color(0.92f, 1f, 0.6f, 1f);
    private static readonly Color ForecastBody = new Color(0.55f, 0.71f, 0.27f, 0.45f);
    private static readonly Color ForecastEdge = new Color(0.4f, 0.6f, 0.2f, 0.5f);
    private static readonly Color ForecastOutlineColor = new Color(0.25f, 0.48f, 0.1f, 0.55f);
    private const float SlimeOutlineWidth = 0.009f;
    private const float ForecastOutlineWidth = 0.006f;
    private const float SlimeSelfLight = 0.25f;
    private const float DigitSelfLight = 0.6f;
    private const float PuddleHeightScale = 0.04f;
    private const float HighlightPower = 22f;
    private const float HighlightCut = 0.72f;

    [MenuItem("Tools/Slime/Setup Visuals")]
    public static void Setup()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        Material spotMaterial = CreateMaterial("SlimeSpot", SlimeBody, SlimeEdge, SlimeOutlineColor, SlimeOutlineWidth, SlimeSelfLight, PuddleHeightScale);
        Material forecastMaterial = CreateMaterial("SlimeForecast", ForecastBody, ForecastEdge, ForecastOutlineColor, ForecastOutlineWidth, SlimeSelfLight, PuddleHeightScale);
        Material digitMaterial = CreateMaterial("SlimeDigit", DigitBody, SlimeEdge, Color.clear, 0f, DigitSelfLight, 0.12f, 0.1f);
        Material outlineMaterial = CreateOutlineMaterial();
        CreateSpotPrefab(spotMaterial);
        AssetDatabase.SaveAssets();

        Scene scene = OpenScene();
        SlimeSpotView spotPrefab = Load<SlimeSpotView>(SpotPrefabPath);
        GameObject host = FindOrCreateRoot(ControllerName);
        SlimeBoardView boardView = GetOrAdd<SlimeBoardView>(host);
        SlimeController controller = GetOrAdd<SlimeController>(host);
        SlimeForecastView forecast = CreateForecast(host.transform, forecastMaterial, digitMaterial, outlineMaterial);
        RemoveSource(host.transform);
        LevelSession session = EnsureSlimeSession();

        Assign(boardView, "_shelfBoard", FindSingle<ShelfBoard>());
        Assign(boardView, "_spotPrefab", spotPrefab);
        Assign(boardView, "_forecast", forecast);
        Assign(boardView, "_splash", CreateSplash(host.transform, spotMaterial));
        SerializedObject serializedBoardView = new SerializedObject(boardView);
        serializedBoardView.FindProperty("_forecastOffset").vector3Value = ForecastOffset;
        serializedBoardView.FindProperty("_spotOffset").vector3Value = SpotOffset;
        serializedBoardView.ApplyModifiedPropertiesWithoutUndo();
        Assign(session, "_slime", controller);
        Assign(controller, "_session", session);
        Assign(controller, "_shelfBoard", FindSingle<ShelfBoard>());
        Assign(controller, "_dragController", FindSingle<ShelfItemDragController>());
        Assign(controller, "_columnRaycaster", FindSingle<ShelfColumnRaycaster>());
        Assign(controller, "_view", boardView);
        Assign(controller, "_audio", SetupAudio(host));
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
            GameObject puddle = CreateMeshPart("Puddle", blob.transform, LoadMesh(PuddleModelPath), material);
            GameObject wallPuddle = CreateMeshPart("WallPuddle", blob.transform, LoadMesh(WallPuddleModelPath), material);
            GameObject bridge = CreateMeshPart("Bridge", blob.transform, LoadMesh(BridgeModelPath), material);

            SlimeSpotView spot = root.AddComponent<SlimeSpotView>();
            Assign(spot, "_blob", blob.transform);
            Assign(spot, "_puddle", puddle);
            Assign(spot, "_wallPuddle", wallPuddle.transform);
            Assign(spot, "_bridge", bridge.transform);
            PrefabUtility.SaveAsPrefabAsset(root, SpotPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Mesh[] LoadDigitMeshes()
    {
        Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(DigitsModelPath).OfType<Mesh>().OrderBy(mesh => mesh.name).ToArray();

        if (meshes.Length != 9)
            throw new InvalidOperationException($"{DigitsModelPath}: expected 9 digit meshes, found {meshes.Length}.");

        return meshes;
    }

    private static Mesh LoadMesh(string path)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

        if (mesh == null)
            throw new InvalidOperationException($"{path}: the slime model was not found.");

        return mesh;
    }

    private static GameObject CreateMeshPart(string name, Transform parent, Mesh mesh, Material material)
    {
        GameObject part = GetOrCreateChild(parent, name);
        GetOrAdd<MeshFilter>(part).sharedMesh = mesh;
        MeshRenderer renderer = GetOrAdd<MeshRenderer>(part);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return part;
    }

    private static SlimeSplashView CreateSplash(Transform parent, Material material)
    {
        GameObject root = GetOrCreateChild(parent, SplashName);
        GameObject particlesObject = GetOrCreateChild(root.transform, "Particles");
        particlesObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem particles = GetOrAdd<ParticleSystem>(particlesObject);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);
        main.gravityModifier = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 60f;
        shape.radius = 0.02f;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer renderer = particlesObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        SlimeSplashView splash = GetOrAdd<SlimeSplashView>(root);
        Assign(splash, "_particles", particles);
        return splash;
    }

    private static SlimeAudioPlayer SetupAudio(GameObject host)
    {
        MatchAudioPlayer matchAudio = FindSingle<MatchAudioPlayer>();
        AudioSource source = host.GetComponent<AudioSource>();

        if (source == null)
            source = host.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.outputAudioMixerGroup = matchAudio.GetComponent<AudioSource>().outputAudioMixerGroup;
        SlimeAudioPlayer audio = GetOrAdd<SlimeAudioPlayer>(host);
        Assign(audio, "_growClip", Load<AudioClip>($"{SoundFolder}/SlimeGrow.wav"));
        Assign(audio, "_stuckClip", Load<AudioClip>($"{SoundFolder}/SlimeStuck.wav"));
        Assign(audio, "_cleanClip", Load<AudioClip>($"{SoundFolder}/SlimeClean.wav"));
        Assign(audio, "_dryClip", Load<AudioClip>($"{SoundFolder}/SlimeDry.wav"));
        Assign(audio, "_lossClip", Load<AudioClip>($"{SoundFolder}/SlimeLoss.wav"));
        return audio;
    }

    private static void RemoveSource(Transform parent)
    {
        Transform existing = parent.Find(SourceName);

        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
    }

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

    private static SlimeForecastView CreateForecast(Transform parent, Material material, Material digitMaterial, Material outlineMaterial)
    {
        GameObject root = GetOrCreateChild(parent, ForecastName);

        GameObject drop = CreateMeshPart("Drop", root.transform, LoadMesh(ForecastModelPath), material);

        GameObject digit = CreateMeshPart("Digit", root.transform, LoadDigitMeshes()[0], digitMaterial);
        digit.transform.localPosition = ForecastDigitPosition;
        digit.transform.localRotation = ForecastDigitRotation;
        digit.transform.localScale = ForecastDigitScale;
        GameObject outline = CreateMeshPart("DigitOutline", digit.transform, LoadDigitMeshes()[0], outlineMaterial);
        outline.transform.localScale = DigitOutlineScale;

        SlimeForecastView forecast = GetOrAdd<SlimeForecastView>(root);
        Assign(forecast, "_drop", drop.transform);
        Assign(forecast, "_digit", digit.GetComponent<MeshFilter>());
        Assign(forecast, "_digitOutline", outline.GetComponent<MeshFilter>());
        SerializedObject serializedForecast = new SerializedObject(forecast);
        SerializedProperty meshesProperty = serializedForecast.FindProperty("_digitMeshes");
        Mesh[] digitMeshes = LoadDigitMeshes();
        meshesProperty.arraySize = digitMeshes.Length;

        for (int index = 0; index < digitMeshes.Length; index++)
            meshesProperty.GetArrayElementAtIndex(index).objectReferenceValue = digitMeshes[index];

        serializedForecast.ApplyModifiedPropertiesWithoutUndo();
        return forecast;
    }

    private static Material CreateOutlineMaterial()
    {
        string path = $"{MaterialFolder}/SlimeDigitOutline.mat";
        Shader shader = Shader.Find("Custom/SlimeOutline");

        if (shader == null)
            throw new InvalidOperationException("The Custom/SlimeOutline shader was not found.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.color = SlimeOutlineColor;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateMaterial(string name, Color color, Color edgeColor, Color outlineColor, float outlineWidth, float selfLight, float heightScale = 0.02f, float shadeStrength = 0.35f)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Shader shader = Shader.Find("Custom/SlimeJelly");

        if (shader == null)
            throw new InvalidOperationException("The Custom/SlimeJelly shader was not found.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.color = color;
        material.SetColor("_EdgeColor", edgeColor);
        material.SetFloat("_HeightScale", heightScale);
        material.SetFloat("_ShadeStrength", shadeStrength);
        material.SetColor("_OutlineColor", outlineColor);
        material.SetFloat("_OutlineWidth", outlineWidth);
        material.SetFloat("_SelfLight", selfLight);
        material.SetFloat("_HighlightPower", HighlightPower);
        material.SetFloat("_HighlightCut", HighlightCut);
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

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
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
