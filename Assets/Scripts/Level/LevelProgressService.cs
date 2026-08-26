using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelProgressService : MonoBehaviour
{
    private const string ProgressStorageKey = "level.progress.v2";
    private const int FirstCatalogIndex = 0;

    [SerializeField] private LevelCatalog _catalog;

    private ProgressData _data;

    [Serializable]
    private class ProgressData
    {
        public List<LevelRecord> Records = new List<LevelRecord>();
    }

    [Serializable]
    private class LevelRecord
    {
        public int LevelNumber;
        public long BestScore;
        public bool IsCompleted;
    }

    private void Awake()
    {
        if (_catalog == null)
            throw new NullReferenceException(nameof(_catalog));

        _data = Load();
    }

    public bool IsUnlocked(LevelEntry level)
    {
        int index = FindCatalogIndex(level);

        if (index == FirstCatalogIndex)
            return true;

        LevelEntry previousLevel = _catalog.Levels[index - 1];
        LevelRecord previousRecord = FindRecord(previousLevel.Number);

        return previousRecord != null && previousRecord.IsCompleted;
    }

    public long GetBestScore(LevelEntry level)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        LevelRecord record = FindRecord(level.Number);
        return record?.BestScore ?? 0;
    }

    public void RegisterCompletion(LevelEntry level, long score)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));

        if (score < 0)
            throw new ArgumentOutOfRangeException(nameof(score));

        LevelRecord record = FindRecord(level.Number);

        if (record == null)
        {
            record = new LevelRecord
            {
                LevelNumber = level.Number
            };

            _data.Records.Add(record);
        }

        record.IsCompleted = true;
        record.BestScore = Math.Max(record.BestScore, score);

        Save();
    }

    private LevelRecord FindRecord(int number)
    {
        foreach (LevelRecord record in _data.Records)
        {
            if (record.LevelNumber == number)
                return record;
        }

        return null;
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

    private static ProgressData Load()
    {
        if (!PlayerPrefs.HasKey(ProgressStorageKey))
            return new ProgressData();

        string json = PlayerPrefs.GetString(ProgressStorageKey);

        if (string.IsNullOrWhiteSpace(json))
            return new ProgressData();

        try
        {
            return JsonUtility.FromJson<ProgressData>(json) ?? new ProgressData();
        }
        catch (ArgumentException ec)
        {
            Debug.LogWarning($"Ошибка чтения: {ec.Message}");
            return new ProgressData();
        }
    }

    private void Save()
    {
        string json = JsonUtility.ToJson(_data);

        PlayerPrefs.SetString(ProgressStorageKey, json);
        PlayerPrefs.Save();
    }
}
