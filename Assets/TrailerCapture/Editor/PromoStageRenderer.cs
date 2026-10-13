using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PromoStageRenderer
{
    private const string ItemsFolder = "Assets/Prefabs/Items/";
    private const string OutputFolderName = "LocalOutput/PromoStage";
    private const int SuperSample = 2;

    private static readonly Color BackgroundTop = new Color(1f, 0.78f, 0.28f);
    private static readonly Color BackgroundBottom = new Color(0.93f, 0.43f, 0.12f);
    private static readonly Color WoodColor = new Color(0.62f, 0.36f, 0.17f);

    [MenuItem("Tools/Promo/Render Stage Variants")]
    private static void RenderAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string returnScenePath = SceneManager.GetActiveScene().path;
        Directory.CreateDirectory(OutputFolder);

        RenderVariant("cover_a_shelf", 800, 470, BuildShelfStage);
        RenderVariant("cover_b_triple", 800, 470, BuildTripleStage);
        RenderVariant("cover_c_key", 800, 470, BuildKeyStage);
        RenderVariant("icon_a_cluster", 512, 512, BuildIconClusterStage);
        RenderVariant("icon_b_triple", 512, 512, BuildIconTripleStage);

        if (!string.IsNullOrEmpty(returnScenePath))
            EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);

        Debug.Log("PROMO_STAGE_PASS");
    }

    private static string OutputFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolderName);

    private static void RenderVariant(string name, int width, int height, System.Func<Camera, float, Transform> build)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("StageCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundBottom;
        camera.fieldOfView = 28f;
        camera.allowMSAA = true;

        CreateLight();
        CreateBackdrop();
        build(camera, (float)width / height);

        RenderTexture target = new RenderTexture(width * SuperSample, height * SuperSample, 24, RenderTextureFormat.ARGB32);
        target.antiAliasing = 8;
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = null;

        RenderTexture output = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(target, output);

        RenderTexture.active = output;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(output);

        File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        target.Release();
        Object.DestroyImmediate(target);
    }

    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("Sun");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.85f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.55f;
        lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.78f, 0.66f, 0.55f);
    }

    private static void CreateBackdrop()
    {
        Texture2D gradient = new Texture2D(128, 128, TextureFormat.RGB24, false);
        gradient.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < gradient.height; y++)
        {
            for (int x = 0; x < gradient.width; x++)
            {
                Vector2 offset = new Vector2((x + 0.5f) / gradient.width - 0.5f, (y + 0.5f) / gradient.height - 0.55f);
                float glow = Mathf.Clamp01(1f - offset.magnitude * 1.5f);
                gradient.SetPixel(x, y, Color.Lerp(BackgroundBottom, BackgroundTop, glow * glow));
            }
        }

        gradient.Apply();
        Material material = new Material(Shader.Find("Unlit/Texture"));
        material.mainTexture = gradient;

        GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = "Backdrop";
        Object.DestroyImmediate(backdrop.GetComponent<Collider>());
        backdrop.GetComponent<MeshRenderer>().sharedMaterial = material;
        backdrop.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        backdrop.GetComponent<MeshRenderer>().receiveShadows = false;
        backdrop.transform.position = new Vector3(0f, 0f, 6f);
        backdrop.transform.localScale = new Vector3(11f, 6.5f, 1f);
    }

    private static Transform BuildShelfStage(Camera camera, float aspect)
    {
        Transform root = new GameObject("Stage").transform;
        PlaceCamera(camera, new Vector3(0f, 0.3f, -3.5f), new Vector3(0f, 0.3f, 0f));

        CreatePlank(root, new Vector3(0f, -0.44f, 0f), new Vector3(3.6f, 0.07f, 0.55f));
        CreatePlank(root, new Vector3(0f, 0.1f, 0f), new Vector3(3.6f, 0.07f, 0.55f));

        PlaceRow(root, new[] { "Ball", "Ball", "Ball", "TeddyBear", "TeddyBear", "TeddyBear", "Plant", "Plant" }, -0.4f, 2.9f, 0.33f);
        PlaceRow(root, new[] { "Lamp", "Lamp", "Lamp", "MapBall", "MapBall", "MapBall", "TrainToy", "TrainToy" }, 0.14f, 2.9f, 0.33f);
        return root;
    }

    private static Transform BuildTripleStage(Camera camera, float aspect)
    {
        Transform root = new GameObject("Stage").transform;
        PlaceCamera(camera, new Vector3(0f, 0.3f, -3.4f), new Vector3(0f, 0.3f, 0f));

        CreatePlank(root, new Vector3(0f, -0.3f, 0f), new Vector3(4.4f, 0.09f, 0.8f));
        PlaceRow(root, new[] { "Ball", "Ball", "Ball" }, -0.25f, 1.3f, 0.62f);
        PlaceItem(root, ItemsFolder + "TeddyBear.prefab", new Vector3(-1.15f, -0.25f, 0f), 2.3f, 190f);
        PlaceItem(root, ItemsFolder + "Lamp.prefab", new Vector3(1.15f, -0.25f, 0f), 2.3f, 190f);
        return root;
    }

    private static Transform BuildKeyStage(Camera camera, float aspect)
    {
        Transform root = new GameObject("Stage").transform;
        PlaceCamera(camera, new Vector3(0f, 0.4f, -3.4f), new Vector3(0f, 0.4f, 0f));

        for (int index = -7; index <= 7; index++)
            CreateBar(root, new Vector3(index * 0.3f, 0.5f, 0.6f), 2.4f);

        CreateCrossBar(root, new Vector3(0f, 1.0f, 0.6f), 4.4f);
        CreateCrossBar(root, new Vector3(0f, -0.1f, 0.6f), 4.4f);

        CreatePlank(root, new Vector3(0f, -0.4f, 0.1f), new Vector3(4f, 0.08f, 0.7f));
        PlaceRow(root, new[] { "TeddyBear", "Lamp", "MapBall", "Ball", "Plant", "Toy", "TrainToy" }, -0.36f, 3.2f, 0.42f, 0.1f);

        GameObject key = PlaceItem(root, "Assets/Prefabs/Keys/ShelfKey.prefab", new Vector3(0f, -0.15f, -0.5f), 3.2f, 0f);
        key.transform.rotation = Quaternion.Euler(0f, -25f, 10f);
        key.transform.position += Vector3.down * 0.08f;

        GameObject glowObject = new GameObject("KeyGlow");
        glowObject.transform.SetParent(root, false);
        glowObject.transform.position = new Vector3(0f, 0.3f, -1.4f);
        Light glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.4f);
        glow.intensity = 1.8f;
        glow.range = 3f;
        return root;
    }

    private static Transform BuildIconClusterStage(Camera camera, float aspect)
    {
        Transform root = new GameObject("Stage").transform;
        PlaceCamera(camera, new Vector3(0f, 0.05f, -3f), new Vector3(0f, 0.05f, 0f));

        CreatePlank(root, new Vector3(0f, -0.42f, 0f), new Vector3(3f, 0.08f, 0.8f));
        PlaceItem(root, ItemsFolder + "TeddyBear.prefab", new Vector3(0f, -0.38f, 0f), 3.7f, 188f);
        PlaceItem(root, ItemsFolder + "Ball.prefab", new Vector3(-0.52f, -0.38f, -0.2f), 2.2f, 0f);
        PlaceItem(root, ItemsFolder + "Lamp.prefab", new Vector3(0.5f, -0.38f, -0.1f), 2.5f, 12f);
        return root;
    }

    private static Transform BuildIconTripleStage(Camera camera, float aspect)
    {
        Transform root = new GameObject("Stage").transform;
        PlaceCamera(camera, new Vector3(0f, 0.05f, -3f), new Vector3(0f, 0.05f, 0f));

        CreatePlank(root, new Vector3(0f, -0.42f, 0f), new Vector3(3f, 0.08f, 0.8f));
        PlaceItem(root, ItemsFolder + "Ball.prefab", new Vector3(-0.55f, -0.38f, -0.1f), 2.3f, 0f);
        PlaceItem(root, ItemsFolder + "Lamp.prefab", new Vector3(0.55f, -0.38f, -0.1f), 2.4f, 12f);
        GameObject key = PlaceItem(root, "Assets/Prefabs/Keys/ShelfKey.prefab", new Vector3(0f, -0.3f, -0.3f), 3.5f, 0f);
        key.transform.rotation = Quaternion.Euler(0f, -22f, 8f);
        key.transform.position += Vector3.down * 0.07f;

        GameObject glowObject = new GameObject("KeyGlow");
        glowObject.transform.SetParent(root, false);
        glowObject.transform.position = new Vector3(0f, 0.3f, -1.4f);
        Light glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.4f);
        glow.intensity = 1.6f;
        glow.range = 3f;
        return root;
    }

    private static void PlaceCamera(Camera camera, Vector3 position, Vector3 target)
    {
        camera.transform.position = position;
        camera.transform.LookAt(target);
    }

    private static void PlaceRow(Transform parent, IReadOnlyList<string> names, float baseY, float width, float scale, float z = 0f)
    {
        for (int index = 0; index < names.Count; index++)
        {
            float t = names.Count == 1 ? 0.5f : (float)index / (names.Count - 1);
            Vector3 position = new Vector3(Mathf.Lerp(-width * 0.5f, width * 0.5f, t), baseY, z);
            PlaceItem(parent, ItemsFolder + names[index] + ".prefab", position, scale * 5f, 180f + (index % 2 == 0 ? 1f : -1f) * 8f);
        }
    }

    private static GameObject PlaceItem(Transform parent, string path, Vector3 position, float scale, float rotationY)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);

        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(behaviour);

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        instance.transform.localScale = Vector3.one * scale;
        instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        float bottom = renderers[0].bounds.min.y;

        foreach (Renderer renderer in renderers)
            bottom = Mathf.Min(bottom, renderer.bounds.min.y);

        instance.transform.position = position + Vector3.up * (instance.transform.position.y - bottom);
        return instance;
    }

    private static void CreatePlank(Transform parent, Vector3 position, Vector3 size)
    {
        GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.name = "Plank";
        Object.DestroyImmediate(plank.GetComponent<Collider>());
        plank.transform.SetParent(parent, false);
        plank.transform.position = position;
        plank.transform.localScale = size;
        plank.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(WoodColor, 0.25f, 0f);
    }

    private static void CreateBar(Transform parent, Vector3 position, float height)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bar.name = "Bar";
        Object.DestroyImmediate(bar.GetComponent<Collider>());
        bar.transform.SetParent(parent, false);
        bar.transform.position = position;
        bar.transform.localScale = new Vector3(0.035f, height * 0.5f, 0.035f);
        bar.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.55f, 0.57f, 0.6f), 0.7f, 0.8f);
    }

    private static void CreateCrossBar(Transform parent, Vector3 position, float width)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bar.name = "CrossBar";
        Object.DestroyImmediate(bar.GetComponent<Collider>());
        bar.transform.SetParent(parent, false);
        bar.transform.position = position;
        bar.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        bar.transform.localScale = new Vector3(0.06f, width * 0.5f, 0.06f);
        bar.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.45f, 0.47f, 0.5f), 0.7f, 0.8f);
    }

    private static Material CreateMaterial(Color color, float smoothness, float metallic)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Glossiness", smoothness);
        material.SetFloat("_Metallic", metallic);
        return material;
    }
}
