using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KeyLockVisualsSetup
{
    private const string OldKeyMaterialPath = "Assets/Materials/KeyLocks/KeyGold.mat";
    private const string KeyMaterialPath = "Assets/Materials/KeyLocks/KeyIron.mat";
    private const float KeyBodyDrop = -0.076f;
    private const string GlowTextureFolder = "Assets/Sprites/KeyLocks";
    private const string GlowTexturePath = "Assets/Sprites/KeyLocks/SoftGlow.png";
    private const string GlowMaterialPath = "Assets/Materials/KeyLocks/KeyGlow.mat";
    private const string GlowTemplateMaterialPath = "Assets/Materials/FlashEffect.mat";
    private const int GlowTextureSize = 128;
    private const string SparkMaterialPath = "Assets/Materials/MatchExplosionStar.mat";
    private const string DustMaterialPath = "Assets/Materials/ClosedShelves/DustPuff.mat";
    private const string KeyPrefabPath = "Assets/Prefabs/Keys/ShelfKey.prefab";
    private const string ScenePath = "Assets/Scenes/SixthLevel.unity";
    private const string DoorEffectsName = "SafeDoorEffects";

    private static readonly Color Iron = new Color(0.16f, 0.13f, 0.11f);
    private static readonly Color HaloColor = new Color(1f, 0.82f, 0.35f, 0.4f);
    private static readonly Color SparkColor = new Color(1f, 0.86f, 0.4f, 1f);
    private static readonly Color FlashColor = new Color(1f, 0.85f, 0.45f, 0.9f);
    private static readonly Color DustColor = new Color(0.78f, 0.76f, 0.72f, 0.8f);

    [MenuItem("Tools/Key Locks/Setup Visuals")]
    public static void Setup()
    {
        Material keyMaterial = CreateKeyMaterial();
        Material glow = CreateGlowMaterial();
        Material spark = Load<Material>(SparkMaterialPath);
        Material dust = Load<Material>(DustMaterialPath);

        BuildKeyPrefab(keyMaterial, glow, spark);
        AssetDatabase.SaveAssets();
        SetupScene(glow, spark, dust);
        Debug.Log("KEY_LOCK_VISUALS_PASS");
    }

    private static Material CreateKeyMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(KeyMaterialPath) == null && AssetDatabase.LoadAssetAtPath<Material>(OldKeyMaterialPath) != null)
        {
            string error = AssetDatabase.RenameAsset(OldKeyMaterialPath, "KeyIron");

            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(KeyMaterialPath);

        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, KeyMaterialPath);
        }

        material.color = Iron;
        material.SetFloat("_Metallic", 0.7f);
        material.SetFloat("_Glossiness", 0.6f);
        material.DisableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", Color.black);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateGlowMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath) == null)
            CreateGlowTexture();

        Material template = Load<Material>(GlowTemplateMaterialPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);

        if (material == null)
        {
            material = new Material(template);
            AssetDatabase.CreateAsset(material, GlowMaterialPath);
        }

        material.shader = template.shader;
        material.CopyPropertiesFromMaterial(template);
        material.mainTexture = Load<Texture2D>(GlowTexturePath);
        material.color = new Color(1.5f, 1.5f, 1.5f, 1f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateGlowTexture()
    {
        EnsureFolder(GlowTextureFolder);
        Texture2D texture = new Texture2D(GlowTextureSize, GlowTextureSize, TextureFormat.RGBA32, false);
        float center = (GlowTextureSize - 1) * 0.5f;

        for (int y = 0; y < GlowTextureSize; y++)
        {
            for (int x = 0; x < GlowTextureSize; x++)
            {
                float distance = Mathf.Clamp01(new Vector2(x - center, y - center).magnitude / center);
                float alpha = Mathf.Pow(1f - distance, 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        System.IO.File.WriteAllBytes(GlowTexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(GlowTexturePath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(GlowTexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void BuildKeyPrefab(Material keyMaterial, Material glow, Material spark)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(KeyPrefabPath);

        try
        {
            Transform body = GetOrCreateChild(root.transform, "Body");
            body.localPosition = new Vector3(0f, KeyBodyDrop, 0f);
            Transform model = MoveChild(root.transform, body, "Model");
            MoveChild(root.transform, body, "MarkPoint");

            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = keyMaterial;

            foreach (string effect in new[] { "Halo", "Trail", "Sparks" })
            {
                RemoveChild(root.transform, effect);
                RemoveChild(body, effect);
            }

            Vector3 center = new Vector3(0f, 0.09f, 0f);
            ParticleSystem halo = CreateHalo(body, center + new Vector3(0f, 0f, -0.015f), glow);
            ParticleSystem trail = CreateTrail(body, center, spark);
            ParticleSystem sparks = CreateBurst(body, "Sparks", center, spark, 14, new Vector2(0.4f, 0.8f), new Vector2(0.025f, 0.05f), 0.45f);

            ShelfKeyView view = root.GetComponent<ShelfKeyView>();

            if (view == null)
                view = root.AddComponent<ShelfKeyView>();

            Assign(view, "_body", body);
            Assign(view, "_model", model);
            Assign(view, "_halo", halo);
            Assign(view, "_trail", trail);
            Assign(view, "_sparks", sparks);
            Assign(root.GetComponent<ShelfKey>(), "_view", view);
            SerializedObject presentation = new SerializedObject(root.GetComponent<ShelfItemPresentation>());
            presentation.FindProperty("_keepsMaterialsInPreview").boolValue = true;
            presentation.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, KeyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetupScene(Material glow, Material spark, Material dust)
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        SafeDoorView[] doors = UnityEngine.Object.FindObjectsOfType<SafeDoorView>(true);

        if (doors.Length == 0)
            throw new InvalidOperationException($"{ScenePath}: no safe doors were found.");

        foreach (SafeDoorView door in doors)
            SetupDoor(door, glow, spark, dust);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetupDoor(SafeDoorView door, Material glow, Material spark, Material dust)
    {
        SerializedObject serializedDoor = new SerializedObject(door);
        Transform lockPoint = (Transform)serializedDoor.FindProperty("_lock").objectReferenceValue;
        Transform bars = (Transform)serializedDoor.FindProperty("_bars").objectReferenceValue;
        Transform bottomRail = bars.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == "RailBottom");

        if (bottomRail == null)
            throw new InvalidOperationException($"{door.name}: the bars have no RailBottom.");

        RemoveChild(door.transform, DoorEffectsName);
        GameObject effects = new GameObject(DoorEffectsName);
        effects.transform.SetParent(door.transform, false);
        Transform root = effects.transform;
        Vector3 toCamera = lockPoint.forward;

        ParticleSystem unlockFlash = CreateFlash(root, root.InverseTransformPoint(lockPoint.position + toCamera * 0.03f), glow);
        CreateBurst(unlockFlash.transform, "UnlockSparks", Vector3.zero, spark, 18, new Vector2(0.45f, 0.9f), new Vector2(0.02f, 0.045f), 0.5f);
        ParticleSystem barsDust = CreateDust(root, root.InverseTransformPoint(bottomRail.position + toCamera * 0.02f), dust);
        ParticleSystem openSparkles = CreateSparkles(root, root.InverseTransformPoint(bars.position + Vector3.up * 0.06f + toCamera * 0.05f), spark);

        Assign(door, "_unlockFlash", unlockFlash);
        Assign(door, "_barsDust", barsDust);
        Assign(door, "_openSparkles", openSparkles);
    }

    private static ParticleSystem CreateHalo(Transform parent, Vector3 position, Material material)
    {
        ParticleSystem system = CreateSystem("Halo", parent, position, material, 101);
        ParticleSystem.MainModule main = system.main;
        main.duration = 1.2f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0f;
        main.startSize = 0.38f;
        main.startColor = HaloColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 2;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.6f, 1) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.75f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.75f)));

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(0f, 1f, 0.5f, 0f));

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.alignment = ParticleSystemRenderSpace.Local;
        return system;
    }

    private static ParticleSystem CreateTrail(Transform parent, Vector3 position, Material material)
    {
        ParticleSystem system = CreateSystem("Trail", parent, position, material, 102);
        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = SparkColor;
        main.gravityModifier = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 45f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.02f;

        AddShrink(system);
        return system;
    }

    private static ParticleSystem CreateBurst(Transform parent, string name, Vector3 position, Material material, int count, Vector2 speed, Vector2 sizeRange, float lifetime)
    {
        ParticleSystem system = CreateSystem(name, parent, position, material, 103);
        ParticleSystem.MainModule main = system.main;
        main.duration = lifetime;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = SparkColor;
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.02f;

        AddShrink(system);
        return system;
    }

    private static ParticleSystem CreateFlash(Transform parent, Vector3 position, Material material)
    {
        ParticleSystem system = CreateSystem("UnlockFlash", parent, position, material, 104);
        ParticleSystem.MainModule main = system.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = 0.28f;
        main.startSpeed = 0f;
        main.startSize = 0.45f;
        main.startColor = FlashColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f));

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(1f, 1f, 0.6f, 0f));
        return system;
    }

    private static ParticleSystem CreateDust(Transform parent, Vector3 position, Material material)
    {
        ParticleSystem system = CreateSystem("BarsDust", parent, position, material, 100);
        ParticleSystem.MainModule main = system.main;
        main.duration = 0.8f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = DustColor;
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 18;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.95f, 0.02f, 0.03f);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(0f, 0.8f, 0.5f, 0f));
        return system;
    }

    private static ParticleSystem CreateSparkles(Transform parent, Vector3 position, Material material)
    {
        ParticleSystem system = CreateSystem("OpenSparkles", parent, position, material, 105);
        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = SparkColor;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.95f, 0.3f, 0.04f);

        AddShrink(system);
        return system;
    }

    private static ParticleSystem CreateSystem(string name, Transform parent, Vector3 position, Material material, int sortingOrder)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        ParticleSystem system = gameObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystemRenderer renderer = gameObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static void AddShrink(ParticleSystem system)
    {
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(1f, 1f, 0.8f, 0f));
    }

    private static Gradient FadeGradient(float startAlpha, float riseAlpha, float holdAlpha, float endAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(riseAlpha, 0.3f), new GradientAlphaKey(holdAlpha, 0.7f), new GradientAlphaKey(endAlpha, 1f) });
        return gradient;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);

        if (child != null)
            return child;

        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Transform MoveChild(Transform from, Transform to, string name)
    {
        Transform child = to.Find(name);

        if (child != null)
            return child;

        child = from.Find(name);

        if (child == null)
            throw new InvalidOperationException($"{KeyPrefabPath}: the key has no {name} child.");

        child.SetParent(to, false);
        return child;
    }

    private static void RemoveChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);

        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
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
