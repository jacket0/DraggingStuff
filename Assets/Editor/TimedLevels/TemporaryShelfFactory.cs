using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TemporaryShelfFactory
{
    private const int ShelfCapacity = 3;

    public static ShelfItem CreateItem(ItemType type, List<GameObject> temporaryObjects)
    {
        GameObject itemObject = EditorUtility.CreateGameObjectWithHideFlags($"ValidationItem_{type}", HideFlags.HideAndDontSave);
        temporaryObjects.Add(itemObject);
        ShelfItem item = itemObject.AddComponent<ShelfItem>();
        SerializedObject serializedItem = new SerializedObject(item);
        serializedItem.FindProperty("_type").enumValueIndex = (int)type;
        serializedItem.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    public static Shelf CreateShelf(string name, List<GameObject> temporaryObjects)
    {
        GameObject shelfObject = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
        temporaryObjects.Add(shelfObject);
        Shelf shelf = shelfObject.AddComponent<Shelf>();
        ShelfView view = shelfObject.AddComponent<ShelfView>();
        ShelfColumnView[] columnViews = new ShelfColumnView[ShelfCapacity];

        for (int index = 0; index < columnViews.Length; index++)
        {
            GameObject columnObject = EditorUtility.CreateGameObjectWithHideFlags($"{name}_Column_{index}", HideFlags.HideAndDontSave);
            temporaryObjects.Add(columnObject);
            BoxCollider dropCollider = columnObject.AddComponent<BoxCollider>();
            columnViews[index] = columnObject.AddComponent<ShelfColumnView>();
            SerializedObject serializedColumn = new SerializedObject(columnViews[index]);
            serializedColumn.FindProperty("_itemAnchor").objectReferenceValue = columnObject.transform;
            serializedColumn.FindProperty("_dropCollider").objectReferenceValue = dropCollider;
            serializedColumn.ApplyModifiedPropertiesWithoutUndo();
        }

        AssignReferences(new SerializedObject(shelf), "_columnViews", columnViews);
        AssignReferences(new SerializedObject(view), "_columns", columnViews);
        SerializedObject serializedShelf = new SerializedObject(shelf);
        serializedShelf.FindProperty("_view").objectReferenceValue = view;
        serializedShelf.ApplyModifiedPropertiesWithoutUndo();
        return shelf;
    }

    public static ShelfBoard CreateBoard(List<GameObject> temporaryObjects, params Shelf[] shelves)
    {
        GameObject boardObject = EditorUtility.CreateGameObjectWithHideFlags("ValidationBoard", HideFlags.HideAndDontSave);
        temporaryObjects.Add(boardObject);
        ShelfBoard board = boardObject.AddComponent<ShelfBoard>();
        AssignReferences(new SerializedObject(board), "_shelves", shelves);
        return board;
    }

    public static void Destroy(List<GameObject> temporaryObjects)
    {
        foreach (GameObject temporaryObject in temporaryObjects)
            Object.DestroyImmediate(temporaryObject);
    }

    private static void AssignReferences(SerializedObject serializedObject, string propertyName, IReadOnlyList<Object> references)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.arraySize = references.Count;

        for (int index = 0; index < references.Count; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = references[index];

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
