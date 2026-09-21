using System;
using UnityEngine;
using YG;

public static class GameProgressRepository
{
    private static GameProgressData _current;
    private static bool _isSynchronized;
    private static bool _savePending;
    private static bool _wasAuthorized;

    public static event Action ProgressChanged;

    public static long EndlessBestScore
    {
        get
        {
            EnsureInitialized();
            return _current.EndlessBestScore;
        }
    }

    public static bool IsLevelCompleted(int levelNumber)
    {
        LevelProgressData record = FindLevel(levelNumber);
        return record != null && record.IsCompleted;
    }

    public static long GetLevelBestScore(int levelNumber)
    {
        LevelProgressData record = FindLevel(levelNumber);
        return record?.BestScore ?? 0;
    }

    public static int GetLevelStars(int levelNumber)
    {
        LevelProgressData record = FindLevel(levelNumber);
        return record?.Stars ?? 0;
    }

    public static long GetLevelBestTime(int levelNumber)
    {
        LevelProgressData record = FindLevel(levelNumber);
        return record?.BestCompletionTimeMilliseconds ?? 0;
    }

    public static void RegisterLevelResult(LevelRunResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        if (!result.Won)
            return;

        EnsureInitialized();
        LevelProgressData record = FindLevel(result.LevelNumber);

        if (record == null)
        {
            record = new LevelProgressData
            {
                LevelNumber = result.LevelNumber
            };
            _current.Levels.Add(record);
        }

        long bestTime = SelectBestTime(record.BestCompletionTimeMilliseconds, result.ActiveTimeMilliseconds);
        bool changed = !record.IsCompleted
            || result.Score > record.BestScore
            || result.Stars > record.Stars
            || bestTime != record.BestCompletionTimeMilliseconds;

        if (!changed)
            return;

        record.IsCompleted = true;
        record.BestScore = Math.Max(record.BestScore, result.Score);
        record.BestCompletionTimeMilliseconds = bestTime;
        record.Stars = Math.Max(record.Stars, result.Stars);
        Commit();
    }

    public static bool RegisterEndlessResult(long score)
    {
        if (score < 0)
            throw new ArgumentOutOfRangeException(nameof(score));

        EnsureInitialized();

        if (score <= _current.EndlessBestScore)
            return false;

        _current.EndlessBestScore = score;
        Commit();
        return true;
    }

    public static BonusInventoryData LoadBonusInventory()
    {
        EnsureInitialized();
        BonusInventoryData result = new BonusInventoryData();

        foreach (BonusAmountData amount in _current.Bonuses)
        {
            result.Amounts.Add(new BonusAmountData
            {
                Id = amount.Id,
                Amount = amount.Amount
            });
        }

        return result;
    }

    public static void SaveBonusInventory(BonusInventoryData inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));

        EnsureInitialized();
        GameProgressData updated = GameProgressMerger.Clone(_current);
        updated.Bonuses.Clear();

        if (inventory.Amounts != null)
        {
            foreach (BonusAmountData amount in inventory.Amounts)
            {
                if (amount == null || string.IsNullOrWhiteSpace(amount.Id))
                    continue;

                updated.Bonuses.Add(new BonusAmountData
                {
                    Id = amount.Id,
                    Amount = Math.Max(0, amount.Amount)
                });
            }
        }

        if (GameProgressMerger.AreEqual(_current, updated))
            return;

        _current = GameProgressMerger.Clone(updated);
        Commit();
    }

    public static void Synchronize(GameProgressData platformProgress)
    {
        EnsureInitialized();
        GameProgressData remote = GameProgressMerger.Clone(platformProgress);
        GameProgressData merged;
        bool accountJustAuthorized = YG2.player.auth && !_wasAuthorized;

        if (!_isSynchronized && remote.LegacyDataImported)
            merged = remote;
        else
            merged = GameProgressMerger.Merge(_current, remote);

        merged.LegacyDataImported = true;

        bool localChanged = !GameProgressMerger.AreEqual(_current, merged);
        bool remoteChanged = !GameProgressMerger.AreEqual(remote, merged);

        _current = GameProgressMerger.Clone(merged);
        _isSynchronized = true;
        _wasAuthorized = YG2.player.auth;
        YG2.saves.GameProgress = GameProgressMerger.Clone(_current);

        if (remoteChanged || _savePending || accountJustAuthorized)
            SaveToPlatform();

        if (localChanged)
            ProgressChanged?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _current = null;
        _isSynchronized = false;
        _savePending = false;
        _wasAuthorized = false;
        ProgressChanged = null;
    }

    private static LevelProgressData FindLevel(int levelNumber)
    {
        if (levelNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelNumber));

        EnsureInitialized();
        return _current.Levels.Find(level => level.LevelNumber == levelNumber);
    }

    private static void EnsureInitialized()
    {
        if (_current != null)
            return;

        _current = GameProgressMerger.Clone(LegacyGameProgressLoader.Load());

        if (YG2.isSDKEnabled)
            Synchronize(YG2.saves.GameProgress);
    }

    private static void Commit()
    {
        _current = GameProgressMerger.Clone(_current);
        YG2.saves.GameProgress = GameProgressMerger.Clone(_current);
        SaveToPlatform();
        ProgressChanged?.Invoke();
    }

    private static void SaveToPlatform()
    {
        if (!YG2.isSDKEnabled)
        {
            _savePending = true;
            return;
        }

        _savePending = false;
        YG2.SaveProgress();
    }

    private static long SelectBestTime(long currentTime, long newTime)
    {
        if (currentTime <= 0)
            return Math.Max(0, newTime);

        if (newTime <= 0)
            return currentTime;

        return Math.Min(currentTime, newTime);
    }
}
