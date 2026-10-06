using System;
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

        for (int i = 0; i < index; i++)
        {
            if (!GameProgressRepository.IsLevelCompleted(_catalog.Levels[i].Number))
                return false;
        }

        return true;
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

        for (int i = 0; i < _catalog.Levels.Count; i++)
        {
            if (_catalog.Levels[i] == level)
                return i;
        }

        throw new InvalidOperationException();
    }

    private void HandleProgressChanged()
    {
        ProgressChanged?.Invoke();
    }
}
