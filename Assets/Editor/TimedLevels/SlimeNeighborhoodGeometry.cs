using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SlimeNeighborhoodGeometry
{
    public const string NeighborhoodPath = "Assets/Levels/Timed/Slime/GreenhouseNeighborhood.asset";
    private const string NeighborhoodFolder = "Assets/Levels/Timed/Slime";
    private const float MaximumSeamColumnDistance = 0.6f;
    private const float MaximumRowHeightDifference = 0.05f;

    [MenuItem("Tools/Slime/Build Neighborhood From Scene")]
    public static void BuildFromActiveScene()
    {
        ShelfBoard board = FindBoard(SceneManager.GetActiveScene());
        IReadOnlyList<SlimeColumnLink> links = CreateLinks(board);
        SlimeNeighborhood neighborhood = AssetDatabase.LoadAssetAtPath<SlimeNeighborhood>(NeighborhoodPath);

        if (neighborhood == null)
        {
            if (!AssetDatabase.IsValidFolder(NeighborhoodFolder))
                AssetDatabase.CreateFolder("Assets/Levels/Timed", "Slime");

            neighborhood = ScriptableObject.CreateInstance<SlimeNeighborhood>();
            AssetDatabase.CreateAsset(neighborhood, NeighborhoodPath);
        }

        WriteLinks(neighborhood, links);
        AssetDatabase.SaveAssets();
        Validate(board, neighborhood);
        Debug.Log($"SLIME_NEIGHBORHOOD_BUILT: {links.Count} links: {string.Join(", ", links.Select(Format))}.");
    }

    public static IReadOnlyList<SlimeColumnLink> CreateLinks(ShelfBoard board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        List<SlimeColumnLink> links = new List<SlimeColumnLink>();

        for (int firstShelfIndex = 0; firstShelfIndex < board.Shelves.Count; firstShelfIndex++)
        {
            for (int secondShelfIndex = firstShelfIndex + 1; secondShelfIndex < board.Shelves.Count; secondShelfIndex++)
            {
                if (TryFindSeam(board, firstShelfIndex, secondShelfIndex, out SlimeColumnLink link))
                    links.Add(link);
            }
        }

        return links;
    }

    public static void Validate(ShelfBoard board, SlimeNeighborhood neighborhood)
    {
        if (neighborhood == null)
            throw new ArgumentNullException(nameof(neighborhood));

        HashSet<SlimeColumnLink> expectedLinks = new HashSet<SlimeColumnLink>(CreateLinks(board));
        IReadOnlyList<SlimeColumnLink> storedLinks = neighborhood.CreateLinks();

        if (storedLinks.Count != expectedLinks.Count || storedLinks.Any(link => !expectedLinks.Contains(link)))
        {
            throw new InvalidOperationException(
                $"{neighborhood.name}: slime links {string.Join(", ", storedLinks.Select(Format))} differ from the touching shelves of the scene {string.Join(", ", expectedLinks.Select(Format))}.");
        }
    }

    private static bool TryFindSeam(ShelfBoard board, int firstShelfIndex, int secondShelfIndex, out SlimeColumnLink link)
    {
        link = default;
        Shelf firstShelf = board.Shelves[firstShelfIndex];
        Shelf secondShelf = board.Shelves[secondShelfIndex];

        if (Mathf.Abs(firstShelf.transform.position.y - secondShelf.transform.position.y) > MaximumRowHeightDifference)
            return false;

        float nearestDistance = float.MaxValue;

        for (int firstColumnIndex = 0; firstColumnIndex < firstShelf.Capacity; firstColumnIndex++)
        {
            for (int secondColumnIndex = 0; secondColumnIndex < secondShelf.Capacity; secondColumnIndex++)
            {
                float distance = Vector3.Distance(firstShelf.ColumnViews[firstColumnIndex].ItemAnchor.position, secondShelf.ColumnViews[secondColumnIndex].ItemAnchor.position);

                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                link = new SlimeColumnLink(new ColumnPosition(firstShelfIndex, firstColumnIndex), new ColumnPosition(secondShelfIndex, secondColumnIndex));
            }
        }

        return nearestDistance <= MaximumSeamColumnDistance;
    }

    private static void WriteLinks(SlimeNeighborhood neighborhood, IReadOnlyList<SlimeColumnLink> links)
    {
        SerializedObject serializedNeighborhood = new SerializedObject(neighborhood);
        SerializedProperty linksProperty = serializedNeighborhood.FindProperty("_links");
        linksProperty.arraySize = links.Count;

        for (int index = 0; index < links.Count; index++)
        {
            WriteColumn(linksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_first"), links[index].First);
            WriteColumn(linksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_second"), links[index].Second);
        }

        serializedNeighborhood.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(neighborhood);
    }

    private static void WriteColumn(SerializedProperty property, ColumnPosition position)
    {
        property.FindPropertyRelative("_shelfIndex").intValue = position.ShelfIndex;
        property.FindPropertyRelative("_columnIndex").intValue = position.ColumnIndex;
    }

    private static ShelfBoard FindBoard(Scene scene)
    {
        ShelfBoard[] boards = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ShelfBoard>(true)).ToArray();

        if (boards.Length != 1)
            throw new InvalidOperationException($"{scene.name}: expected one ShelfBoard, found {boards.Length}.");

        boards[0].Initialize();
        return boards[0];
    }

    private static string Format(SlimeColumnLink link)
    {
        return $"{link.First.ShelfIndex}.{link.First.ColumnIndex}-{link.Second.ShelfIndex}.{link.Second.ColumnIndex}";
    }
}
