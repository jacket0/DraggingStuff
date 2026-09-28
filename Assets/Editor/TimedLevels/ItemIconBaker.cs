using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ItemIconBaker
{
    private const string CatalogPath = "Assets/Settings/Endless/ShelfItemCatalog.asset";
    private const string OutputFolder = "Assets/Sprites/UI/Items";
    private const int IconSize = 256;

    [MenuItem("Tools/Timed Levels/Bake Item Icons")]
    public static void Run()
    {
        ShelfItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ShelfItemCatalog>(CatalogPath);

        if (catalog == null)
            throw new InvalidOperationException($"Item catalog was not found at {CatalogPath}.");

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Sprites/UI", "Items");

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        SerializedProperty entriesProperty = serializedCatalog.FindProperty("_entries");

        int bakedIconCount = 0;

        for (int index = 0; index < catalog.Entries.Count; index++)
        {
            ShelfItemCatalog.Entry entry = catalog.Entries[index];

            if (entry == null || entry.Prefab == null)
                continue;

            string iconPath = BakeIcon(entry.Type, entry.Prefab);
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            entriesProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_icon").objectReferenceValue = sprite;
            bakedIconCount++;
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"ITEM_ICON_BAKE_PASS: {bakedIconCount}");
    }

    private static string BakeIcon(ItemType type, ShelfItem prefab)
    {
        GameObject cameraObject = new GameObject("ItemIconBakerCamera");
        GameObject instance = null;
        RenderTexture renderTexture = null;

        try
        {
            instance = UnityEngine.Object.Instantiate(prefab.gameObject, Vector3.zero, Quaternion.identity);
            instance.hideFlags = HideFlags.HideAndDontSave;

            Bounds bounds = ComputeBounds(instance);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = int.MaxValue;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.01f);
            camera.orthographicSize = extent * 1.15f;
            cameraObject.transform.position = bounds.center + new Vector3(0f, 0f, -extent * 4f);
            cameraObject.transform.LookAt(bounds.center);

            Light keyLight = cameraObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.1f;
            keyLight.color = new Color(1f, 0.97f, 0.92f);

            renderTexture = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
            texture.Apply();
            RenderTexture.active = previousActive;

            string path = $"{OutputFolder}/{type}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSpriteImport(path);
            return path;
        }
        finally
        {
            if (renderTexture != null)
            {
                Camera camera = cameraObject.GetComponent<Camera>();

                if (camera != null)
                    camera.targetTexture = null;

                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);

            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void ConfigureSpriteImport(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            throw new InvalidOperationException($"Texture importer was not found for {path}.");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static Bounds ComputeBounds(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return new Bounds(instance.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;

        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        return bounds;
    }
}
