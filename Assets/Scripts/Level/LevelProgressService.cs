using System;
using UnityEngine;

public class LevelProgressService : MonoBehaviour
{
    private const int FirstCatalogIndex = 0;

    [SerializeField] private LevelCatalog _catalog;


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

        if (index == FirstCatalogIndex)
            return true;

        LevelEntry previousLevel = _catalog.Levels[index - 1];
        return GameProgressRepository.IsLevelCompleted(previousLevel.Number);
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

    public bool AreAllLevelsCompleted()
    {
        foreach (LevelEntry level in _catalog.Levels)
        {
            if (level == null)
                throw new InvalidOperationException(nameof(_catalog.Levels));

            if (!GameProgressRepository.IsLevelCompleted(level.Number))
                return false;
        }

        return _catalog.Levels.Count > 0;
    }

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
