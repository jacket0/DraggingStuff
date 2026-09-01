using System;
using System.Collections.Generic;
using UnityEngine;

public static class LegacyGameProgressLoader
{
    private const string LevelProgressKey = "level.progress.v2";
    private const string EndlessProgressKey = "endless.progress.v1";
    private const string BonusInventoryKey = "bonus.inventory.v1";

    public static GameProgressData Load()
    {
        GameProgressData progress = new GameProgressData
        {
            LegacyDataImported = true
        };

        LoadLevels(progress);
        LoadEndlessScore(progress);
        LoadBonuses(progress);

        return progress;
    }

    private static void LoadLevels(GameProgressData progress)
    {
        LegacyLevelProgress data = Read<LegacyLevelProgress>(LevelProgressKey);

        if (data?.Records == null)
            return;

        foreach (LegacyLevelRecord record in data.Records)
        {
            if (record == null)
                continue;

            progress.Levels.Add(new LevelProgressData
            {
                LevelNumber = record.LevelNumber,
                BestScore = record.BestScore,
                IsCompleted = record.IsCompleted
            });
        }
    }

    private static void LoadEndlessScore(GameProgressData progress)
    {
        LegacyEndlessProgress data = Read<LegacyEndlessProgress>(EndlessProgressKey);
        progress.EndlessBestScore = Math.Max(0, data?.BestScore ?? 0);
    }

    private static void LoadBonuses(GameProgressData progress)
    {
        BonusInventoryData data = Read<BonusInventoryData>(BonusInventoryKey);

        if (data?.Amounts == null)
            return;

        foreach (BonusAmountData amount in data.Amounts)
        {
            if (amount == null)
                continue;

            progress.Bonuses.Add(new BonusAmountData
            {
                Id = amount.Id,
                Amount = amount.Amount
            });
        }
    }

    private static T Read<T>(string key) where T : class
    {
        if (!PlayerPrefs.HasKey(key))
            return null;

        string json = PlayerPrefs.GetString(key);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"Не удалось перенести сохранение {key}: {exception.Message}");
            return null;
        }
    }

    [Serializable]
    private sealed class LegacyLevelProgress
    {
        public List<LegacyLevelRecord> Records = new List<LegacyLevelRecord>();
    }

    [Serializable]
    private sealed class LegacyLevelRecord
    {
        public int LevelNumber = 0;
        public long BestScore = 0;
        public bool IsCompleted = false;
    }

    [Serializable]
    private sealed class LegacyEndlessProgress
    {
        public long BestScore = 0;
    }
}
