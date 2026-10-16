using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CyclicShelfVisualsSetup
{
    private const string ScenePath = "Assets/Scenes/SeventhLevel.unity";
    private const string ShutterModelPath = "Assets/Models/CyclicShelves/LockerShutter.fbx";
    private const string BoardModelPath = "Assets/Models/CyclicShelves/DepartureBoard.fbx";
    private const string MaterialFolder = "Assets/Materials/CyclicShelves";
    private const string SpriteFolder = "Assets/Sprites/CyclicShelves";
    private const string AtlasPath = "Assets/Sprites/CyclicShelves/SplitFlapDigits.png";
    private const string FontPath = "Assets/Fonts/Code New Roman SDF.asset";
    private const string SoundFolder = "Assets/Audio/Sounds/CyclicShelves";
    private const string ShutterName = "Shutter";
    private const string GlassName = "Glass";
    private const string PanelName = "Panel";
    private const string LampName = "StateLamp";
    private const string DisplayName = "SplitFlap";
    private const float CurtainHeight = 0.38f;
    private const float ClosingWarningDrop = 0.07f;
    private static readonly Vector3 CurtainPosition = new Vector3(0f, 0.25f, 0.2f);
    private static readonly Vector3 LampPosition = new Vector3(0.25f, 0f, 0.02f);
    private static readonly Vector3 DisplayPosition = new Vector3(0f, 0f, 0.0215f);
    private static readonly Vector3 IconPosition = new Vector3(0.25f, 0f, 0.044f);
    private static readonly Vector2 CardSize = new Vector2(0.135f, 0.166f);
    private static readonly Vector2 IconSize = new Vector2(0.046f, 0.057f);
    private const float DigitSpacing = 0.15f;
    private static readonly string[] StubSlotParts = { "FlapLeft", "FlapRight", "FlapSplit", "Digits" };

    private static readonly Color CurtainSteel = new Color(0.66f, 0.69f, 0.72f);
    private static readonly Color HandleSteel = new Color(0.2f, 0.21f, 0.23f);
    private static readonly Color GlassTint = new Color(0.2f, 0.26f, 0.3f, 0.42f);
    private static readonly Color BezelSteel = new Color(0.3f, 0.31f, 0.34f);
    private static readonly Color WindowBlack = new Color(0.025f, 0.025f, 0.03f);

    [MenuItem("Tools/Cyclic Shelves/Setup Visuals")]
    public static void Setup()
    {
        EnsureFolder(SpriteFolder);
        Texture2D atlas = SplitFlapAtlasBuilder.Build(AtlasPath, Load<TMP_FontAsset>(FontPath));
        Material curtain = CreateMaterial("ShutterCurtain", "Custom/ShutterCurtain", CurtainSteel, 0.55f, 0.45f);
        Material handle = CreateMaterial("ShutterHandle", "Custom/ShutterCurtain", HandleSteel, 0.5f, 0.35f);
        Material glass = CreateMaterial("ShutterCurtainGlass", "Custom/ShutterCurtainGlass", GlassTint, 0f, 0.9f);
        Material bezel = CreateMaterial("DepartureBezel", "Standard", BezelSteel, 0.6f, 0.5f);
        Material window = CreateMaterial("DepartureWindow", "Standard", WindowBlack, 0f, 0.2f);
        Material splitFlap = CreateMaterial("SplitFlap", "Unlit/Transparent Cutout", Color.white, 0f, 0f);
        splitFlap.mainTexture = atlas;
        splitFlap.SetFloat("_Cutoff", 0.5f);
        AssetDatabase.SaveAssets();

        Scene scene = OpenScene();
        CyclicShelvesController controller = FindSingle<CyclicShelvesController>();

        foreach (ShelfShutterView shutter in UnityEngine.Object.FindObjectsOfType<ShelfShutterView>(true))
            SetupShutter(shutter, curtain, handle, glass);

        SetupBoard(FindSingle<DepartureBoardView>(), bezel, window, splitFlap);
        Assign(controller, "_audio", SetupAudio(controller));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("CYCLIC_SHELF_VISUALS_PASS");
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

    private static void SetupShutter(ShelfShutterView shutter, Material curtainMaterial, Material handleMaterial, Material glassMaterial)
    {
        shutter.name = ShutterName;
        SerializedObject serialized = new SerializedObject(shutter);
        GameObject housing = (GameObject)serialized.FindProperty("_housing").objectReferenceValue;
        Transform curtain = (Transform)serialized.FindProperty("_curtain").objectReferenceValue;

        if (housing == null || curtain == null)
            throw new InvalidOperationException($"{shutter.Shelf.name}: the shutter has no housing or curtain.");

        SetMesh(housing, LoadMesh(ShutterModelPath, "ShutterHousing"), housing.GetComponent<MeshRenderer>().sharedMaterial);
        ResetTransform(housing.transform, Vector3.zero);
        RemoveCollider(housing);

        foreach (Transform child in curtain.Cast<Transform>().ToArray())
            UnityEngine.Object.DestroyImmediate(child.gameObject);

        ResetTransform(curtain, CurtainPosition);
        MeshRenderer curtainRenderer = SetMesh(curtain.gameObject, LoadMesh(ShutterModelPath, "ShutterCurtain"), curtainMaterial, handleMaterial);
        GameObject glass = new GameObject(GlassName);
        glass.transform.SetParent(curtain, false);
        MeshRenderer glassRenderer = SetMesh(glass, LoadMesh(ShutterModelPath, "ShutterGlass"), glassMaterial);
        glassRenderer.shadowCastingMode = ShadowCastingMode.Off;
        curtain.gameObject.SetActive(false);

        SerializedProperty renderers = serialized.FindProperty("_curtainRenderers");
        renderers.arraySize = 2;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = curtainRenderer;
        renderers.GetArrayElementAtIndex(1).objectReferenceValue = glassRenderer;
        serialized.FindProperty("_curtainHeight").floatValue = CurtainHeight;
        serialized.FindProperty("_closingWarningDrop").floatValue = ClosingWarningDrop;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupBoard(DepartureBoardView board, Material bezel, Material window, Material splitFlap)
    {
        Transform panel = board.transform.Find(PanelName);

        if (panel == null)
            throw new InvalidOperationException($"{board.name} has no {PanelName}.");

        Material panelMaterial = panel.GetComponent<MeshRenderer>().sharedMaterial;
        SetMesh(panel.gameObject, LoadMesh(BoardModelPath, "DepartureBoardPanel"), panelMaterial, bezel, window);
        ResetTransform(panel, Vector3.zero);
        RemoveCollider(panel.gameObject);

        foreach (DepartureBoardSlotView slot in board.GetComponentsInChildren<DepartureBoardSlotView>(true))
        {
            Vector3 slotPosition = slot.transform.localPosition;
            slot.transform.localPosition = new Vector3(slotPosition.x, slotPosition.y, 0f);

            foreach (string part in StubSlotParts)
            {
                Transform stub = slot.transform.Find(part);

                if (stub != null)
                    UnityEngine.Object.DestroyImmediate(stub.gameObject);
            }

            Transform lamp = slot.transform.Find(LampName);

            if (lamp == null)
                throw new InvalidOperationException($"{slot.name} has no {LampName}.");

            SetMesh(lamp.gameObject, LoadMesh(BoardModelPath, "DepartureLamp"), lamp.GetComponent<MeshRenderer>().sharedMaterial);
            ResetTransform(lamp, LampPosition);
            RemoveCollider(lamp.gameObject);

            Transform display = slot.transform.Find(DisplayName);

            if (display == null)
            {
                display = new GameObject(DisplayName, typeof(MeshFilter), typeof(MeshRenderer), typeof(SplitFlapDisplay)).transform;
                display.SetParent(slot.transform, false);
            }

            ResetTransform(display, DisplayPosition);
            MeshRenderer displayRenderer = display.GetComponent<MeshRenderer>();
            displayRenderer.sharedMaterial = splitFlap;
            displayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            displayRenderer.receiveShadows = false;
            SerializedObject displaySettings = new SerializedObject(display.GetComponent<SplitFlapDisplay>());
            displaySettings.FindProperty("_cardSize").vector2Value = CardSize;
            displaySettings.FindProperty("_digitSpacing").floatValue = DigitSpacing;
            displaySettings.FindProperty("_iconPosition").vector3Value = IconPosition - DisplayPosition;
            displaySettings.FindProperty("_iconSize").vector2Value = IconSize;
            displaySettings.ApplyModifiedPropertiesWithoutUndo();
            Assign(slot, "_display", display.GetComponent<SplitFlapDisplay>());
        }
    }

    private static CyclicShelfAudioPlayer SetupAudio(CyclicShelvesController controller)
    {
        MatchAudioPlayer matchAudio = FindSingle<MatchAudioPlayer>();
        GameObject host = controller.gameObject;
        AudioSource source = host.GetComponent<AudioSource>();

        if (source == null)
            source = host.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.outputAudioMixerGroup = matchAudio.GetComponent<AudioSource>().outputAudioMixerGroup;
        CyclicShelfAudioPlayer audio = host.GetComponent<CyclicShelfAudioPlayer>();

        if (audio == null)
            audio = host.AddComponent<CyclicShelfAudioPlayer>();

        AssignIfExists(audio, "_flipClip", $"{SoundFolder}/SplitFlap.wav");
        AssignIfExists(audio, "_warningClip", $"{SoundFolder}/DepartureChime.wav");
        AssignIfExists(audio, "_shutterDownClip", $"{SoundFolder}/ShutterDown.wav");
        AssignIfExists(audio, "_shutterUpClip", $"{SoundFolder}/ShutterUp.wav");
        return audio;
    }

    private static Material CreateMaterial(string name, string shaderName, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Shader shader = Shader.Find(shaderName);

        if (shader == null)
            throw new InvalidOperationException($"Shader {shaderName} was not found.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;

        if (material.HasProperty("_Color"))
            material.color = color;

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static MeshRenderer SetMesh(GameObject target, Mesh mesh, params Material[] materials)
    {
        MeshFilter filter = target.GetComponent<MeshFilter>();

        if (filter == null)
            filter = target.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
            meshRenderer = target.AddComponent<MeshRenderer>();

        filter.sharedMesh = mesh;
        meshRenderer.sharedMaterials = materials;
        return meshRenderer;
    }

    private static void ResetTransform(Transform target, Vector3 localPosition)
    {
        target.localPosition = localPosition;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();

        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
    }

    private static Mesh LoadMesh(string modelPath, string meshName)
    {
        Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().FirstOrDefault(candidate => candidate.name == meshName);

        if (mesh == null)
            throw new InvalidOperationException($"{modelPath} has no mesh {meshName}.");

        return mesh;
    }

    private static T FindSingle<T>() where T : UnityEngine.Object
    {
        T[] found = UnityEngine.Object.FindObjectsOfType<T>(true);

        if (found.Length != 1)
            throw new InvalidOperationException($"Expected exactly one {typeof(T).Name} in the scene, found {found.Length}.");

        return found[0];
    }

    private static void AssignIfExists(UnityEngine.Object target, string field, string assetPath)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

        if (clip != null)
            Assign(target, field, clip);
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
