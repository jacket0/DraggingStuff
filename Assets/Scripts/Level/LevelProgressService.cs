using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelProgressService : MonoBehaviour
{
    [SerializeField] private LevelCatalog _catalog;
    [SerializeField, Min(1)] private int _endlessUnlockLevelNumber = 5;

    public event Action ProgressChanged;

    private void Awake()
    {
        if (_catalog == null)
            throw new NullReferenceException(nameof(_catalog));
    }

    private void OnEnable()
    {
        GameProgressRepository.ProgressChanged += HandleProgressChanged;
    }

    private void OnDisable()
    {
        GameProgressRepository.ProgressChanged -= HandleProgressChanged;
    }

    public bool IsUnlocked(LevelEntry level)
    {
        int index = FindCatalogIndex(level);
        IReadOnlyList<LevelEntry> levels = _catalog.Levels;

        for (int i = 0; i < index; i++)
        {
            if (!IsCompleted(levels[i]))
                return false;
        }

        return true;
    }

    public bool IsCompleted(LevelEntry level)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        return GameProgressRepository.IsLevelCompleted(level.Number);
    }

    public long GetBestScore(LevelEntry level)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        return GameProgressRepository.GetLevelBestScore(level.Number);
    }

    public int GetStars(LevelEntry level)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        return GameProgressRepository.GetLevelStars(level.Number);
    }

    public long GetBestTime(LevelEntry level)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        return GameProgressRepository.GetLevelBestTime(level.Number);
    }

    public bool IsEndlessUnlocked() => GameProgressRepository.IsLevelCompleted(_endlessUnlockLevelNumber);

    public void RegisterLevelResult(LevelRunResult result)
    {
        GameProgressRepository.RegisterLevelResult(result);
    }

    private int FindCatalogIndex(LevelEntry level)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        int index = _catalog.IndexOf(level);

        if (index < 0)
            throw new InvalidOperationException($"{level.name} is not in {_catalog.name}.");

        return index;
    }

    private void HandleProgressChanged()
    {
        ProgressChanged?.Invoke();
    }
}
