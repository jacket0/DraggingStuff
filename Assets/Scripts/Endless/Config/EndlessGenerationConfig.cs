using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EndlessGenerationConfig", menuName = "Game/Endless/Generation Config")]
public sealed class EndlessGenerationConfig : ScriptableObject
{
    [SerializeField, Min(1)] private int _minimumEmptyColumns = 6;
    [SerializeField, Range(0f, 0.5f)] private float _emptyColumnRatio = 0.15f;
    [SerializeField, Min(1)] private int _refillThresholdItemCount = 24;
    [SerializeField, Range(0.01f, 1f)] private float _repeatedTypeWeight = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _earlySlotFillChance = 0.65f;
    [SerializeField, Range(0f, 1f)] private float _lateSlotFillChance = 0.8f;
    [SerializeField, Min(1)] private int _fullDifficultyMatchCount = 100;

    public int MinimumEmptyColumns => _minimumEmptyColumns;
    public float EmptyColumnRatio => _emptyColumnRatio;
    public int RefillThresholdItemCount => _refillThresholdItemCount;
    public float RepeatedTypeWeight => _repeatedTypeWeight;

    public void Validate()
    {
        if (_minimumEmptyColumns <= 0 || !IsInRange(_emptyColumnRatio, 0f, 0.5f))
            throw new InvalidOperationException(nameof(_minimumEmptyColumns));

        if (_refillThresholdItemCount <= 0)
            throw new InvalidOperationException(nameof(_refillThresholdItemCount));

        if (!IsInRange(_repeatedTypeWeight, 0.01f, 1f))
            throw new InvalidOperationException(nameof(_repeatedTypeWeight));

        if (!IsInRange(_earlySlotFillChance, 0f, 1f) || !IsInRange(_lateSlotFillChance, _earlySlotFillChance, 1f))
            throw new InvalidOperationException(nameof(_lateSlotFillChance));

        if (_fullDifficultyMatchCount <= 0)
            throw new InvalidOperationException(nameof(_fullDifficultyMatchCount));
    }

    public float GetColumnFillChance(int matchCount)
    {
        float difficulty = Mathf.Clamp01((float)matchCount / _fullDifficultyMatchCount);
        return Mathf.Lerp(_earlySlotFillChance, _lateSlotFillChance, difficulty);
    }

    private static bool IsInRange(float value, float minimum, float maximum)
    {
        return value >= minimum && value <= maximum;
    }
}
