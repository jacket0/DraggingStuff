using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SplitFlapAtlasBuilder
{
    public const int CellWidth = 104;
    public const int CellHeight = 128;

    private const int AtlasWidth = CellWidth * SplitFlapDisplay.AtlasColumns;
    private const int AtlasHeight = CellHeight * SplitFlapDisplay.AtlasRows;
    private const float PixelsPerUnit = 100f;
    private const float FontSize = 11f;
    private const int CardInset = 3;
    private const int CardCornerRadius = 9;
    private const int SplitLineHeight = 2;
    private const int IconSamples = 4;
    private const char DashGlyph = '-';
    private static readonly RectInt DashArea = new RectInt(30, 57, 44, 14);

    private static readonly Color CardTop = new Color(0.17f, 0.17f, 0.19f);
    private static readonly Color CardBottom = new Color(0.12f, 0.12f, 0.135f);
    private static readonly Color SplitLine = new Color(0.03f, 0.03f, 0.035f);
    private static readonly Color DigitColor = new Color(0.97f, 0.93f, 0.82f);
    private static readonly Color IconColor = new Color(0.98f, 0.97f, 0.93f);

    public static Texture2D Build(string path, TMP_FontAsset font)
    {
        if (font == null)
            throw new ArgumentNullException(nameof(font));

        Texture2D coverage = RenderGlyphCoverage(font);
        Texture2D atlas = new Texture2D(AtlasWidth, AtlasHeight, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[AtlasWidth * AtlasHeight];
        Color[] glyphCoverage = coverage.GetPixels();

        for (int cell = 0; cell < SplitFlapDisplay.Glyphs.Length; cell++)
            DrawCard(pixels, glyphCoverage, cell);

        DrawIcon(pixels, SplitFlapDisplay.LockCell, IsInsideLock);
        DrawIcon(pixels, SplitFlapDisplay.ArrowUpCell, IsInsideArrow);
        atlas.SetPixels(pixels);
        atlas.Apply();
        File.WriteAllBytes(path, atlas.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(atlas);
        UnityEngine.Object.DestroyImmediate(coverage);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D RenderGlyphCoverage(TMP_FontAsset font)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = RenderTexture.GetTemporary(AtlasWidth, AtlasHeight, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;

        try
        {
            GameObject cameraObject = new GameObject("AtlasCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = AtlasHeight / PixelsPerUnit * 0.5f;
            camera.aspect = (float)AtlasWidth / AtlasHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.targetTexture = target;

            foreach (int cell in Enumerable.Range(0, SplitFlapDisplay.Glyphs.Length).Where(cell => SplitFlapDisplay.Glyphs[cell] != DashGlyph))
            {
                GameObject textObject = new GameObject($"Glyph_{cell}");
                SceneManager.MoveGameObjectToScene(textObject, scene);
                TextMeshPro text = textObject.AddComponent<TextMeshPro>();
                text.font = font;
                text.fontSize = FontSize;
                text.fontStyle = FontStyles.Bold;
                text.enableWordWrapping = false;
                text.horizontalAlignment = HorizontalAlignmentOptions.Geometry;
                text.verticalAlignment = VerticalAlignmentOptions.Geometry;
                text.color = Color.white;
                text.text = SplitFlapDisplay.Glyphs[cell].ToString();
                text.rectTransform.sizeDelta = new Vector2(CellWidth, CellHeight) / PixelsPerUnit;
                Vector2Int origin = GetCellOrigin(cell);
                textObject.transform.position = new Vector3(
                    (origin.x + CellWidth * 0.5f - AtlasWidth * 0.5f) / PixelsPerUnit,
                    (origin.y + CellHeight * 0.5f - AtlasHeight * 0.5f) / PixelsPerUnit,
                    0f);
                text.ForceMeshUpdate();
            }

            camera.Render();
            RenderTexture.active = target;
            Texture2D coverage = new Texture2D(AtlasWidth, AtlasHeight, TextureFormat.RGBA32, false);
            coverage.ReadPixels(new Rect(0, 0, AtlasWidth, AtlasHeight), 0, 0);
            coverage.Apply();
            return coverage;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void DrawCard(Color[] pixels, Color[] glyphCoverage, int cell)
    {
        Vector2Int origin = GetCellOrigin(cell);
        int splitBottom = CellHeight / 2 - SplitLineHeight / 2;

        for (int y = 0; y < CellHeight; y++)
        {
            for (int x = 0; x < CellWidth; x++)
            {
                int index = (origin.y + y) * AtlasWidth + origin.x + x;

                if (!IsInsideRoundedRect(x, y, CardInset, CardInset, CellWidth - CardInset, CellHeight - CardInset, CardCornerRadius))
                {
                    pixels[index] = Color.clear;
                    continue;
                }

                Color card = y >= CellHeight / 2 ? CardTop : CardBottom;
                float shade = 1f - 0.25f * Mathf.Clamp01(1f - Mathf.Abs(y - CellHeight * 0.5f) / 10f);
                card = new Color(card.r * shade, card.g * shade, card.b * shade, 1f);
                float coverage = SplitFlapDisplay.Glyphs[cell] == DashGlyph ? (DashArea.Contains(new Vector2Int(x, y)) ? 1f : 0f) : glyphCoverage[index].r;
                Color color = Color.Lerp(card, DigitColor, coverage);

                if (y >= splitBottom && y < splitBottom + SplitLineHeight)
                    color = SplitLine;

                pixels[index] = color;
            }
        }
    }

    private static void DrawIcon(Color[] pixels, int cell, Func<float, float, bool> isInside)
    {
        Vector2Int origin = GetCellOrigin(cell);

        for (int y = 0; y < CellHeight; y++)
        {
            for (int x = 0; x < CellWidth; x++)
            {
                int covered = 0;

                for (int sampleY = 0; sampleY < IconSamples; sampleY++)
                {
                    for (int sampleX = 0; sampleX < IconSamples; sampleX++)
                    {
                        if (isInside(x + (sampleX + 0.5f) / IconSamples, y + (sampleY + 0.5f) / IconSamples))
                            covered++;
                    }
                }

                float alpha = covered / (float)(IconSamples * IconSamples);
                pixels[(origin.y + y) * AtlasWidth + origin.x + x] = new Color(IconColor.r, IconColor.g, IconColor.b, alpha);
            }
        }
    }

    private static bool IsInsideLock(float x, float y)
    {
        bool isKeyhole = Vector2.Distance(new Vector2(x, y), new Vector2(52f, 46f)) < 8f || (x > 48f && x < 56f && y > 24f && y < 46f);

        if (IsInsideRoundedRect(x, y, 18f, 10f, 86f, 72f, 9f))
            return !isKeyhole;

        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(52f, 72f));
        return y >= 60f && distance < 26f && distance > 15f;
    }

    private static bool IsInsideArrow(float x, float y)
    {
        if (y >= 64f && y <= 116f)
        {
            float halfWidth = 36f * (116f - y) / 52f;
            return Mathf.Abs(x - 52f) <= halfWidth;
        }

        return y >= 12f && y < 64f && Mathf.Abs(x - 52f) <= 12f;
    }

    private static bool IsInsideRoundedRect(float x, float y, float left, float bottom, float right, float top, float radius)
    {
        if (x < left || x >= right || y < bottom || y >= top)
            return false;

        float nearestX = Mathf.Clamp(x, left + radius, right - radius);
        float nearestY = Mathf.Clamp(y, bottom + radius, top - radius);
        return Vector2.Distance(new Vector2(x, y), new Vector2(nearestX, nearestY)) <= radius;
    }

    private static Vector2Int GetCellOrigin(int cell)
    {
        int column = cell % SplitFlapDisplay.AtlasColumns;
        int row = cell / SplitFlapDisplay.AtlasColumns;
        return new Vector2Int(column * CellWidth, AtlasHeight - (row + 1) * CellHeight);
    }

    private static void ConfigureImporter(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }
}
