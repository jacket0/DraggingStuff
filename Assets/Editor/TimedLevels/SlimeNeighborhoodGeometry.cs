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
    public const int GrowthColumnsPerShelf = 2;

    [MenuItem("Tools/Slime/Build Neighborhood From Scene")]
    public static void BuildFromActiveScene()
    {
        ShelfBoard board = FindBoard(SceneManager.GetActiveScene());
        IReadOnlyList<SlimeColumnLink> links = CreateLinks(board);
        IReadOnlyList<ColumnPosition> perimeter = CreateGrowthPath(board, GrowthColumnsPerShelf);
        SlimeNeighborhood neighborhood = AssetDatabase.LoadAssetAtPath<SlimeNeighborhood>(NeighborhoodPath);

        if (neighborhood == null)
        {
            if (!AssetDatabase.IsValidFolder(NeighborhoodFolder))
                AssetDatabase.CreateFolder("Assets/Levels/Timed", "Slime");

            neighborhood = ScriptableObject.CreateInstance<SlimeNeighborhood>();
            AssetDatabase.CreateAsset(neighborhood, NeighborhoodPath);
        }

        WriteNeighborhood(neighborhood, links, perimeter);
        AssetDatabase.SaveAssets();
        Validate(board, neighborhood);
        Debug.Log(
            $"SLIME_NEIGHBORHOOD_BUILT: {links.Count} links: {string.Join(", ", links.Select(Format))}; " +
            $"growth path of {perimeter.Count} columns: {string.Join(", ", perimeter.Select(Format))}.");
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

    public static IReadOnlyList<ColumnPosition> CreateGrowthPath(ShelfBoard board, int columnsPerShelf)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        if (columnsPerShelf < 1)
            throw new ArgumentOutOfRangeException(nameof(columnsPerShelf));

        List<(ColumnPosition Column, int Depth, int Row, int Side)> entries = new List<(ColumnPosition, int, int, int)>();
        List<List<int>> rows = GroupRows(board);

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            int[] shelves = rows[rowIndex].OrderByDescending(shelfIndex => GetAnchor(board, new ColumnPosition(shelfIndex, 0)).x).ToArray();
            int leftCount = (shelves.Length + 1) / 2;

            for (int position = 0; position < shelves.Length; position++)
            {
                bool isLeft = position < leftCount;
                int shelfIndex = shelves[position];
                int depthInRow = isLeft ? position : shelves.Length - 1 - position;
                ColumnPosition[] columns = Enumerable.Range(0, board.Shelves[shelfIndex].Capacity)
                    .Select(columnIndex => new ColumnPosition(shelfIndex, columnIndex))
                    .OrderBy(column => isLeft ? -GetAnchor(board, column).x : GetAnchor(board, column).x)
                    .Take(columnsPerShelf)
                    .ToArray();

                for (int columnOrder = 0; columnOrder < columns.Length; columnOrder++)
                    entries.Add((columns[columnOrder], depthInRow * board.Shelves[shelfIndex].Capacity + columnOrder, rowIndex, isLeft ? 0 : 1));
            }
        }

        return entries.OrderBy(entry => entry.Depth).ThenBy(entry => entry.Row).ThenBy(entry => entry.Side).Select(entry => entry.Column).ToArray();
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

        IReadOnlyList<ColumnPosition> expectedPerimeter = CreateGrowthPath(board, GrowthColumnsPerShelf);
        IReadOnlyList<ColumnPosition> storedPerimeter = neighborhood.CreatePerimeter();

        if (!storedPerimeter.SequenceEqual(expectedPerimeter))
        {
            throw new InvalidOperationException(
                $"{neighborhood.name}: slime perimeter {string.Join(", ", storedPerimeter.Select(Format))} differs from the growth path of the scene {string.Join(", ", expectedPerimeter.Select(Format))}.");
        }
    }

    private static List<List<int>> GroupRows(ShelfBoard board)
    {
        List<List<int>> rows = new List<List<int>>();

        foreach (int shelfIndex in Enumerable.Range(0, board.Shelves.Count).OrderBy(index => board.Shelves[index].transform.position.y))
        {
            float height = board.Shelves[shelfIndex].transform.position.y;
            List<int> row = rows.FirstOrDefault(candidate => Mathf.Abs(board.Shelves[candidate[0]].transform.position.y - height) <= MaximumRowHeightDifference);

            if (row == null)
                rows.Add(new List<int> { shelfIndex });
            else
                row.Add(shelfIndex);
        }

        return rows;
    }

    private static Vector3 GetAnchor(ShelfBoard board, ColumnPosition column)
    {
        return board.Shelves[column.ShelfIndex].ColumnViews[column.ColumnIndex].ItemAnchor.position;
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

    private static void WriteNeighborhood(SlimeNeighborhood neighborhood, IReadOnlyList<SlimeColumnLink> links, IReadOnlyList<ColumnPosition> perimeter)
    {
        SerializedObject serializedNeighborhood = new SerializedObject(neighborhood);
        SerializedProperty linksProperty = serializedNeighborhood.FindProperty("_links");
        linksProperty.arraySize = links.Count;

        for (int index = 0; index < links.Count; index++)
        {
            WriteColumn(linksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_first"), links[index].First);
            WriteColumn(linksProperty.GetArrayElementAtIndex(index).FindPropertyRelative("_second"), links[index].Second);
        }

        SerializedProperty perimeterProperty = serializedNeighborhood.FindProperty("_perimeterColumns");
        perimeterProperty.arraySize = perimeter.Count;

        for (int index = 0; index < perimeter.Count; index++)
            WriteColumn(perimeterProperty.GetArrayElementAtIndex(index), perimeter[index]);

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

    private static string Format(ColumnPosition column) => $"{column.ShelfIndex}.{column.ColumnIndex}";

    private static string Format(SlimeColumnLink link)
    {
        return $"{link.First.ShelfIndex}.{link.First.ColumnIndex}-{link.Second.ShelfIndex}.{link.Second.ColumnIndex}";
    }
}
