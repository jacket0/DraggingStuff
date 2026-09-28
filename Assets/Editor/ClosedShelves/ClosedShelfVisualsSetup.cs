using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG.LanguageLegacy;

public static class ClosedShelfVisualsSetup
{
    private const string SpriteFolder = "Assets/Sprites/ClosedShelves";
    private const string SoundFolder = "Assets/Audio/Sounds/ClosedShelves";
    private const string MaterialFolder = "Assets/Materials/ClosedShelves";
    private const string PrefabFolder = "Assets/Prefabs/ClosedShelves";
    private const string ScenePath = "Assets/Scenes/FourthLevel.unity";
    private const string GlowPath = "Assets/Sprites/glow.png";
    private const string StarPath = "Assets/Sprites/UI/TimedLevels/Stars/LevelStarShine-512.png";
    private const string FontPath = "Assets/Fonts/Code New Roman SDF.asset";
    private const string CatalogPath = "Assets/Settings/Endless/ShelfItemCatalog.asset";
    private const string ToastName = "RefillStoppedToast";
    private const string IntroBubbleName = "ClosedShelfIntroBubble";

    private static readonly Color Cream = Hex("FFF3DC");
    private static readonly Color Brown = Hex("6B3A1E");
    private static readonly Color RingBackground = Hex("E8D6B4");
    private static readonly Color RingFill = Hex("F4A72C");
    private static readonly Color[] MarkerColors = { Hex("40D1FF"), Hex("7DFF6B"), Hex("FF5FA3"), Hex("B87DFF") };

    [MenuItem("Tools/Closed Shelves/Build Visual Assets")]
    public static void BuildAssets()
    {
        ConfigureImporters();
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        Material dust = CreateMaterial("ShelfDustCover", "Custom/ShelfDustCover", null);
        dust.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpriteFolder}/DustNoise.png"));
        dust.SetColor("_Color", new Color(0.50f, 0.46f, 0.42f, 0.74f));
        EditorUtility.SetDirty(dust);

        Material additiveGlow = CreateMaterial("AdditiveGlow", "Legacy Shaders/Particles/Additive", AssetDatabase.LoadAssetAtPath<Texture2D>(GlowPath));
        Material puff = CreateMaterial("DustPuff", "Legacy Shaders/Particles/Alpha Blended", AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpriteFolder}/Puff.png"));
        Material trail = CreateMaterial("StarTrail", "Legacy Shaders/Particles/Additive", null);

        ConditionSlotView slot = BuildSlotPrefab();
        BuildCoverPrefab(dust, additiveGlow, puff, slot);
        BuildStarPrefab(additiveGlow, trail);
        BuildMarkerPrefab();

        ShelfItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ShelfItemCatalog>(CatalogPath);

        if (catalog.Entries.Any(entry => entry.Icon == null))
            ItemIconBaker.Run();

        AssetDatabase.SaveAssets();
        Debug.Log("CLOSED_SHELF_VISUAL_ASSETS_PASS");
    }

    [MenuItem("Tools/Closed Shelves/Setup FourthLevel Visuals")]
    public static void SetupScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        ClosedShelvesController controller = FindSingle<ClosedShelvesController>();
        ShelfBoard board = FindSingle<ShelfBoard>();
        MoveResolutionPlayer resolutionPlayer = FindSingle<MoveResolutionPlayer>();
        MatchAudioPlayer matchAudio = FindSingle<MatchAudioPlayer>();
        ShelfItemDragController dragController = FindSingle<ShelfItemDragController>();
        Camera camera = (Camera)new SerializedObject(resolutionPlayer).FindProperty("_camera").objectReferenceValue;
        GameObject host = controller.gameObject;

        foreach (Shelf shelf in board.GetComponentsInChildren<Shelf>(true))
        {
            ClosedShelfCoverAnchor anchor = shelf.GetComponent<ClosedShelfCoverAnchor>();

            if (anchor == null)
                anchor = Undo.AddComponent<ClosedShelfCoverAnchor>(shelf.gameObject);

            Undo.RecordObject(anchor, "Fit closed shelf anchor");
            anchor.FitToColumns();
            EditorUtility.SetDirty(anchor);
        }

        ClosedShelfAudioPlayer audio = GetOrAdd<ClosedShelfAudioPlayer>(host);
        AudioSource source = host.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.outputAudioMixerGroup = matchAudio.GetComponent<AudioSource>().outputAudioMixerGroup;
        Assign(audio, "_starClip", Load<AudioClip>($"{SoundFolder}/StarChime.wav"));
        Assign(audio, "_chordClip", Load<AudioClip>($"{SoundFolder}/ConditionChord.wav"));
        Assign(audio, "_dustClip", Load<AudioClip>($"{SoundFolder}/DustPuff.wav"));
        Assign(audio, "_popClip", Load<AudioClip>($"{SoundFolder}/ItemPop.wav"));
        Assign(audio, "_rejectClip", Load<AudioClip>($"{SoundFolder}/RejectThud.wav"));

        ConditionStarEffect starEffect = GetOrAdd<ConditionStarEffect>(host);
        Assign(starEffect, "_resolutionPlayer", resolutionPlayer);
        Assign(starEffect, "_starPrefab", Load<ConditionStarView>($"{PrefabFolder}/ConditionStar.prefab"));
        Assign(starEffect, "_camera", camera);

        ClosedShelfDropFeedback dropFeedback = GetOrAdd<ClosedShelfDropFeedback>(host);
        Assign(dropFeedback, "_dragController", dragController);
        Assign(dropFeedback, "_closedShelves", controller);
        Assign(dropFeedback, "_audio", audio);
        Assign(dropFeedback, "_camera", camera);

        Assign(controller, "_coverPrefab", Load<ClosedShelfCoverView>($"{PrefabFolder}/ClosedShelfCover.prefab"));
        Assign(controller, "_markerPrefab", Load<TargetShelfMarkerView>($"{PrefabFolder}/TargetShelfMarker.prefab"));
        Assign(controller, "_starEffect", starEffect);
        Assign(controller, "_audio", audio);
        Assign(controller, "_itemCatalog", Load<ShelfItemCatalog>(CatalogPath));
        Assign(controller, "_camera", camera);
        Assign(controller, "_shelfIcon", LoadSprite("ShelfIcon"));
        AssignColors(controller, "_markerColors", MarkerColors);

        Canvas hud = FindSingle<CampaignTimerView>().GetComponentInParent<Canvas>().rootCanvas;
        Transform existingToast = hud.transform.Find(ToastName);

        if (existingToast != null)
            Undo.DestroyObjectImmediate(existingToast.gameObject);

        BuildToast(hud.transform, controller);

        Transform existingBubble = hud.transform.Find(IntroBubbleName);

        if (existingBubble != null)
            Undo.DestroyObjectImmediate(existingBubble.gameObject);

        RectTransform bubble = BuildIntroBubble(hud.transform);
        ClosedShelfIntroHint introHint = GetOrAdd<ClosedShelfIntroHint>(host);
        Assign(introHint, "_session", new SerializedObject(controller).FindProperty("_session").objectReferenceValue);
        Assign(introHint, "_closedShelves", controller);
        Assign(introHint, "_catalog", Load<LevelCatalog>("Assets/Levels/Menu/MainLevelCatalog.asset"));
        Assign(introHint, "_text", bubble.GetComponentInChildren<TextMeshProUGUI>(true));
        Assign(introHint, "_camera", camera);
        Assign(introHint, "_bubble", bubble);
        Assign(introHint, "_bubbleGroup", bubble.GetComponent<CanvasGroup>());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("CLOSED_SHELF_SCENE_SETUP_PASS");
    }

    private static void ConfigureImporters()
    {
        ConfigureSprite("Pixel", new Vector2(0.5f, 1f), Vector4.zero, 100f);
        ConfigureSprite("Cobweb", new Vector2(0f, 1f), Vector4.zero, 100f);
        ConfigureSprite("Puff", new Vector2(0.5f, 0.5f), Vector4.zero, 100f);
        ConfigureSprite("Ring", new Vector2(0.5f, 0.5f), Vector4.zero, 100f);
        ConfigureSprite("Disc", new Vector2(0.5f, 0.5f), Vector4.zero, 100f);
        ConfigureSprite("TagBackground", new Vector2(0.5f, 0.5f), new Vector4(36f, 44f, 36f, 36f), 100f);
        ConfigureSprite("ToastBackground", new Vector2(0.5f, 0.5f), new Vector4(36f, 36f, 36f, 36f), 100f);
        ConfigureSprite("TagGlow", new Vector2(0.5f, 0.5f), new Vector4(44f, 44f, 44f, 44f), 100f);
        ConfigureSprite("Fill", new Vector2(0.5f, 0.5f), Vector4.zero, 100f);
        ConfigureSprite("ShelfHighlight", new Vector2(0.5f, 0.5f), new Vector4(40f, 40f, 40f, 40f), 800f);
        ConfigureSprite("ShelfIcon", new Vector2(0.5f, 0.5f), Vector4.zero, 100f);

        TextureImporter noise = (TextureImporter)AssetImporter.GetAtPath($"{SpriteFolder}/DustNoise.png");
        noise.textureType = TextureImporterType.Default;
        noise.wrapMode = TextureWrapMode.Repeat;
        noise.mipmapEnabled = true;
        noise.SaveAndReimport();
    }

    private static void ConfigureSprite(string name, Vector2 pivot, Vector4 border, float pixelsPerUnit)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath($"{SpriteFolder}/{name}.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = pixelsPerUnit;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteBorder = border;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static Material CreateMaterial(string name, string shaderName, Texture texture)
    {
        Shader shader = Shader.Find(shaderName);

        if (shader == null)
            throw new InvalidOperationException($"Shader {shaderName} was not found.");

        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;

        if (texture != null)
            material.mainTexture = texture;

        EditorUtility.SetDirty(material);
        return material;
    }

    private static ConditionSlotView BuildSlotPrefab()
    {
        GameObject root = new GameObject("ConditionSlot", typeof(RectTransform));
        RectTransform rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(120f, 160f);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredWidth = 120f;
        layout.preferredHeight = 160f;

        CreateImage("RingBackground", root.transform, LoadSprite("Ring"), RingBackground, new Vector2(0f, 22f), new Vector2(112f, 112f));
        Image ringFill = CreateImage("RingFill", root.transform, LoadSprite("Ring"), RingFill, new Vector2(0f, 22f), new Vector2(112f, 112f));
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Top;
        ringFill.fillClockwise = true;
        ringFill.fillAmount = 0f;
        Image icon = CreateImage("Icon", root.transform, LoadSprite("ShelfIcon"), Color.white, new Vector2(0f, 22f), new Vector2(86f, 86f));
        icon.preserveAspect = true;

        TextMeshProUGUI count = CreateText("Count", root.transform, "0/0", 40f, Brown, new Vector2(0f, -56f), new Vector2(130f, 46f));
        count.fontStyle = FontStyles.Bold;

        ConditionSlotView view = root.AddComponent<ConditionSlotView>();
        Assign(view, "_ringFill", ringFill);
        Assign(view, "_icon", icon);
        Assign(view, "_count", count);

        return SavePrefab<ConditionSlotView>(root, "ConditionSlot");
    }

    private static void BuildCoverPrefab(Material dust, Material additiveGlow, Material puff, ConditionSlotView slotPrefab)
    {
        GameObject root = new GameObject("ClosedShelfCover");

        GameObject dustObject = new GameObject("DustOverlay");
        dustObject.transform.SetParent(root.transform, false);
        dustObject.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer dustRenderer = dustObject.AddComponent<MeshRenderer>();
        dustRenderer.sharedMaterial = dust;
        dustRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dustRenderer.receiveShadows = false;
        dustRenderer.sortingOrder = 100;

        SpriteRenderer cobwebLeft = CreateSprite("CobwebLeft", root.transform, LoadSprite("Cobweb"), new Color(1f, 1f, 1f, 0.85f), 101, null);
        SpriteRenderer cobwebRight = CreateSprite("CobwebRight", root.transform, LoadSprite("Cobweb"), new Color(1f, 1f, 1f, 0.85f), 101, null);
        ParticleSystem motes = CreateMotes(root.transform, additiveGlow);
        ParticleSystem puffSystem = CreatePuff(root.transform, puff);
        SpriteRenderer flash = CreateSprite("RevealFlash", root.transform, AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath), new Color(1f, 0.9f, 0.65f, 0f), 104, additiveGlow);

        GameObject pivot = new GameObject("TagPivot");
        pivot.transform.SetParent(root.transform, false);
        SpriteRenderer thread = CreateSprite("Thread", pivot.transform, LoadSprite("Pixel"), Brown, 105, null);

        GameObject tagObject = new GameObject("TagCanvas", typeof(RectTransform));
        tagObject.transform.SetParent(pivot.transform, false);
        RectTransform tagRect = (RectTransform)tagObject.transform;
        tagRect.pivot = new Vector2(0.5f, 1f);
        tagRect.sizeDelta = new Vector2(420f, 220f);
        tagRect.localScale = Vector3.one * 0.0013f;
        Canvas canvas = tagObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 106;
        CanvasGroup tagGroup = tagObject.AddComponent<CanvasGroup>();
        tagGroup.blocksRaycasts = false;
        tagGroup.interactable = false;

        GameObject panel = new GameObject("Slots", typeof(RectTransform));
        panel.transform.SetParent(tagObject.transform, false);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = Vector2.zero;
        Image background = panel.AddComponent<Image>();
        background.sprite = LoadSprite("TagBackground");
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        background.raycastTarget = false;
        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 24);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject glow = new GameObject("Glow", typeof(RectTransform));
        glow.transform.SetParent(panel.transform, false);
        RectTransform glowRect = (RectTransform)glow.transform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-34f, -34f);
        glowRect.offsetMax = new Vector2(34f, 34f);
        glow.AddComponent<LayoutElement>().ignoreLayout = true;
        Image glowImage = glow.AddComponent<Image>();
        glowImage.sprite = LoadSprite("TagGlow");
        glowImage.type = Image.Type.Sliced;
        glowImage.color = new Color(1f, 0.84f, 0.47f, 0.9f);
        glowImage.raycastTarget = false;
        CanvasGroup glowGroup = glow.AddComponent<CanvasGroup>();
        glowGroup.alpha = 0f;

        ClosedShelfCoverView view = root.AddComponent<ClosedShelfCoverView>();
        Assign(view, "_dust", dustRenderer);
        Assign(view, "_cobwebLeft", cobwebLeft);
        Assign(view, "_cobwebRight", cobwebRight);
        Assign(view, "_motes", motes);
        Assign(view, "_puff", puffSystem);
        Assign(view, "_revealFlash", flash);
        Assign(view, "_tagPivot", pivot.transform);
        Assign(view, "_thread", thread);
        Assign(view, "_tag", tagObject.transform);
        Assign(view, "_tagGroup", tagGroup);
        Assign(view, "_tagGlow", glowGroup);
        Assign(view, "_slotsRoot", panel.transform);
        Assign(view, "_slotPrefab", slotPrefab);

        SavePrefab<ClosedShelfCoverView>(root, "ClosedShelfCover");
    }

    private static ParticleSystem CreateMotes(Transform parent, Material material)
    {
        GameObject gameObject = new GameObject("Motes");
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        ParticleSystem system = gameObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 2f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.02f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
        main.startColor = new Color(1f, 0.95f, 0.85f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 8f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 0.3f, 0.02f);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.02f;
        noise.frequency = 0.5f;

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(0f, 1f, 0f));

        ParticleSystemRenderer renderer = gameObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 102;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return system;
    }

    private static ParticleSystem CreatePuff(Transform parent, Material material)
    {
        GameObject gameObject = new GameObject("Puff");
        gameObject.transform.SetParent(parent, false);
        ParticleSystem system = gameObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.65f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startSize = 0.14f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.86f, 0.82f, 0.78f, 1f);
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 30;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1.7f));

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(0.9f, 0.9f, 0f));

        ParticleSystemRenderer renderer = gameObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 103;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return system;
    }

    private static void BuildStarPrefab(Material additiveGlow, Material trailMaterial)
    {
        GameObject root = new GameObject("ConditionStar");
        SpriteRenderer star = root.AddComponent<SpriteRenderer>();
        star.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
        star.sortingOrder = 111;

        Sprite glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        SpriteRenderer glow = CreateSprite("Glow", root.transform, glowSprite, new Color(1f, 0.82f, 0.35f, 0.8f), 110, additiveGlow);
        glow.transform.localScale = Vector3.one * (star.sprite.bounds.size.x * 1.8f / glowSprite.bounds.size.x);

        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.minVertexDistance = 0.01f;
        trail.widthMultiplier = 0.05f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.sharedMaterial = trailMaterial;
        trail.sortingOrder = 109;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        ConditionStarView view = root.AddComponent<ConditionStarView>();
        Assign(view, "_star", star);
        Assign(view, "_trail", trail);

        SavePrefab<ConditionStarView>(root, "ConditionStar");
    }

    private static void BuildMarkerPrefab()
    {
        GameObject root = new GameObject("TargetShelfMarker");
        SpriteRenderer fill = CreateSprite("Fill", root.transform, LoadSprite("Fill"), Color.white, 97, null);
        SpriteRenderer backFrame = CreateSprite("BackFrame", root.transform, LoadSprite("ShelfHighlight"), Color.white, 98, null);
        SpriteRenderer frontFrame = CreateSprite("FrontFrame", root.transform, LoadSprite("ShelfHighlight"), Color.white, 99, null);
        backFrame.drawMode = SpriteDrawMode.Sliced;
        frontFrame.drawMode = SpriteDrawMode.Sliced;

        TargetShelfMarkerView view = root.AddComponent<TargetShelfMarkerView>();
        Assign(view, "_frontFrame", frontFrame);
        Assign(view, "_backFrame", backFrame);
        Assign(view, "_fill", fill);

        SavePrefab<TargetShelfMarkerView>(root, "TargetShelfMarker");
    }

    private static void BuildToast(Transform hud, ClosedShelvesController controller)
    {
        GameObject root = new GameObject(ToastName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create refill toast");
        root.transform.SetParent(hud, false);
        root.layer = hud.gameObject.layer;
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        panel.layer = root.layer;
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -185f);
        panelRect.sizeDelta = new Vector2(760f, 110f);
        Image background = panel.AddComponent<Image>();
        background.sprite = LoadSprite("ToastBackground");
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        CanvasGroup group = panel.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        TextMeshProUGUI text = CreateText("Text", panel.transform, "Новых предметов больше не будет", 42f, Cream, Vector2.zero, Vector2.zero);
        text.gameObject.layer = root.layer;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 10f);
        textRect.offsetMax = new Vector2(-30f, -10f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 24f;
        text.fontSizeMax = 42f;

        LanguageYG localization = text.gameObject.AddComponent<LanguageYG>();
        localization.textMPComponent = text;
        localization.ru = "Новых предметов больше не будет";
        localization.en = "No more new items";
        localization.tr = "Artık yeni eşya gelmeyecek";

        RefillStoppedToastView view = root.AddComponent<RefillStoppedToastView>();
        Assign(view, "_closedShelves", controller);
        Assign(view, "_panel", panelRect);
        Assign(view, "_canvasGroup", group);
        panel.SetActive(false);
    }

    private static RectTransform BuildIntroBubble(Transform hud)
    {
        GameObject bubble = new GameObject(IntroBubbleName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(bubble, "Create closed shelf intro bubble");
        bubble.transform.SetParent(hud, false);
        bubble.layer = hud.gameObject.layer;
        RectTransform rect = (RectTransform)bubble.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(620f, 124f);
        Image background = bubble.AddComponent<Image>();
        background.sprite = LoadSprite("TagBackground");
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        CanvasGroup group = bubble.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        TextMeshProUGUI text = CreateText("Text", bubble.transform, string.Empty, 32f, Brown, Vector2.zero, Vector2.zero);
        text.gameObject.layer = bubble.layer;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 14f);
        textRect.offsetMax = new Vector2(-24f, -8f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = 32f;
        text.enableWordWrapping = true;

        bubble.SetActive(false);
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color, Vector2 position, Vector2 rectSize)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Load<TMP_FontAsset>(FontPath);
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Color color, int sortingOrder, Material material)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (material != null)
            renderer.sharedMaterial = material;

        return renderer;
    }

    private static Gradient FadeGradient(float startAlpha, float middleAlpha, float endAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(middleAlpha, 0.25f), new GradientAlphaKey(endAlpha, 1f) });
        return gradient;
    }

    private static T SavePrefab<T>(GameObject root, string name) where T : Component
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<T>();
    }

    private static T GetOrAdd<T>(GameObject host) where T : Component
    {
        T component = host.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(host);
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

    private static void AssignColors(UnityEngine.Object target, string field, Color[] colors)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        property.arraySize = colors.Length;

        for (int index = 0; index < colors.Length; index++)
            property.GetArrayElementAtIndex(index).colorValue = colors[index];

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
            throw new InvalidOperationException($"Asset {path} of type {typeof(T).Name} was not found.");

        return asset;
    }

    private static Sprite LoadSprite(string name) => Load<Sprite>($"{SpriteFolder}/{name}.png");

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int split = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString($"#{hex}", out Color color) ? color : Color.magenta;
}
