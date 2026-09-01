using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "EndlessGenerationConfig", menuName = "Game/Endless/Generation Config")]
public sealed class EndlessGenerationConfig : ScriptableObject
{
    [SerializeField, Min(1)] private int _minimumInitialEmptySlots = 6;
    [SerializeField, Range(0f, 0.5f)] private float _initialEmptySlotRatio = 0.15f;
    [FormerlySerializedAs("_minimumBatchTripleCount")]
    [SerializeField, Min(1)] private int _minimumBatchGroupCount = 10;
    [FormerlySerializedAs("_maximumBatchTripleCount")]
    [SerializeField, Min(1)] private int _maximumBatchGroupCount = 14;
    [FormerlySerializedAs("_refillThresholdTripleCount")]
    [SerializeField, Min(1)] private int _refillThresholdItemCount = 24;
    [SerializeField, Range(0f, 1f)] private float _threeItemGroupWeight = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _fourItemGroupWeight = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _fiveItemGroupWeight = 0.1f;
    [SerializeField, Range(0f, 1f)] private float _fullTwoSlotLayerChance = 0.7f;
    [SerializeField, Min(1)] private int _candidateCount = 10;
    [SerializeField, Min(1)] private int _solverCandidateCount = 3;
    [SerializeField, Min(1)] private int _solverMatchGoal = 3;
    [SerializeField, Min(1)] private int _solverMaximumMoveCount = 7;
    [SerializeField, Min(1)] private int _solverNodeLimit = 400;
    [SerializeField, Min(1)] private int _solverMaximumMovesPerState = 32;
    [SerializeField, Min(1)] private int _solverTimeLimitMilliseconds = 8;
    [SerializeField, Min(1)] private int _generationTimeLimitMilliseconds = 30;
    [SerializeField, Range(0f, 1f)] private float _earlyThreeShelfChance = 0.7f;
    [SerializeField, Range(0f, 1f)] private float _lateThreeShelfChance = 0.9f;
    [SerializeField, Range(0f, 1f)] private float _earlySameLayerPairChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _lateSameLayerPairChance = 0.1f;
    [SerializeField, Min(1)] private int _fullDifficultyMatchCount = 100;

    public int MinimumInitialEmptySlots => _minimumInitialEmptySlots;
    public float InitialEmptySlotRatio => _initialEmptySlotRatio;
    public int MinimumBatchGroupCount => _minimumBatchGroupCount;
    public int MaximumBatchGroupCount => _maximumBatchGroupCount;
    public int RefillThresholdItemCount => _refillThresholdItemCount;
    public float ThreeItemGroupWeight => _threeItemGroupWeight;
    public float FourItemGroupWeight => _fourItemGroupWeight;
    public float FiveItemGroupWeight => _fiveItemGroupWeight;
    public float FullTwoSlotLayerChance => _fullTwoSlotLayerChance;
    public int CandidateCount => _candidateCount;
    public int SolverCandidateCount => _solverCandidateCount;
    public int SolverMatchGoal => _solverMatchGoal;
    public int SolverMaximumMoveCount => _solverMaximumMoveCount;
    public int SolverNodeLimit => _solverNodeLimit;
    public int SolverMaximumMovesPerState => _solverMaximumMovesPerState;
    public int SolverTimeLimitMilliseconds => _solverTimeLimitMilliseconds;
    public int GenerationTimeLimitMilliseconds => _generationTimeLimitMilliseconds;

    public void Validate()
    {
        if (_minimumBatchGroupCount <= 0 || _maximumBatchGroupCount < _minimumBatchGroupCount)
            throw new InvalidOperationException(nameof(_maximumBatchGroupCount));

        if (_refillThresholdItemCount <= 0)
            throw new InvalidOperationException(nameof(_refillThresholdItemCount));

        if (_threeItemGroupWeight + _fourItemGroupWeight + _fiveItemGroupWeight <= 0f)
            throw new InvalidOperationException(nameof(_threeItemGroupWeight));

        if (_candidateCount <= 0 ||
            _solverCandidateCount <= 0 ||
            _solverMatchGoal <= 0 ||
            _solverMaximumMoveCount <= 0 ||
            _solverNodeLimit <= 0 ||
            _solverMaximumMovesPerState <= 0 ||
            _solverTimeLimitMilliseconds <= 0 ||
            _generationTimeLimitMilliseconds <= 0)
            throw new InvalidOperationException(nameof(_candidateCount));
    }

    public float GetThreeShelfChance(int matchCount)
    {
        return Mathf.Lerp(_earlyThreeShelfChance, _lateThreeShelfChance, GetDifficulty(matchCount));
    }

    public float GetSameLayerPairChance(int matchCount)
    {
        return Mathf.Lerp(_earlySameLayerPairChance, _lateSameLayerPairChance, GetDifficulty(matchCount));
    }

    public float GetDenseLayerChance(int matchCount)
    {
        return Mathf.Lerp(0.25f, 0.5f, GetDifficulty(matchCount));
    }

    private float GetDifficulty(int matchCount)
    {
        return Mathf.Clamp01((float)matchCount / _fullDifficultyMatchCount);
    }
}
