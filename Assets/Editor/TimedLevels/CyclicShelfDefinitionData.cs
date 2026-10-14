using System.Collections.Generic;
using UnityEditor;

public sealed class CyclicShelfDefinitionData
{
    private readonly int _shelfIndex;
    private readonly bool _startsOpen;
    private readonly int _openMoves;
    private readonly int _closedMoves;
    private readonly int _movesToFirstSwitch;

    public CyclicShelfDefinitionData(int shelfIndex, bool startsOpen, int openMoves, int closedMoves, int movesToFirstSwitch)
    {
        _shelfIndex = shelfIndex;
        _startsOpen = startsOpen;
        _openMoves = openMoves;
        _closedMoves = closedMoves;
        _movesToFirstSwitch = movesToFirstSwitch;
    }

    public void Write(SerializedProperty property)
    {
        property.FindPropertyRelative("_shelfIndex").intValue = _shelfIndex;
        property.FindPropertyRelative("_startsOpen").boolValue = _startsOpen;
        property.FindPropertyRelative("_openMoves").intValue = _openMoves;
        property.FindPropertyRelative("_closedMoves").intValue = _closedMoves;
        property.FindPropertyRelative("_movesToFirstSwitch").intValue = _movesToFirstSwitch;
    }

    public static void WriteAll(SerializedProperty property, IReadOnlyList<CyclicShelfDefinitionData> cyclicShelves)
    {
        property.arraySize = cyclicShelves.Count;

        for (int index = 0; index < cyclicShelves.Count; index++)
            cyclicShelves[index].Write(property.GetArrayElementAtIndex(index));
    }
}
