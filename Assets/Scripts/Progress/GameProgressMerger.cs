using System;
using System.Collections.Generic;

public static class GameProgressMerger
{
    public static GameProgressData Merge(GameProgressData first, GameProgressData second)
    {
        GameProgressData result = Clone(first);
        GameProgressData other = Clone(second);

        result.Version = Math.Max(result.Version, other.Version);
        result.LegacyDataImported |= other.LegacyDataImported;
        result.EndlessBestScore = Math.Max(result.EndlessBestScore, other.EndlessBestScore);

        foreach (LevelProgressData level in other.Levels)
            MergeLevel(result.Levels, level);

        foreach (BonusAmountData bonus in other.Bonuses)
            MergeBonus(result.Bonuses, bonus);

        return Normalize(result);
    }

    public static GameProgressData Clone(GameProgressData source)
    {
        GameProgressData normalizedSource = source ?? new GameProgressData();
        GameProgressData clone = new GameProgressData
        {
            Version = GameProgressData.CurrentVersion,
            LegacyDataImported = normalizedSource.LegacyDataImported,
            EndlessBestScore = Math.Max(0, normalizedSource.EndlessBestScore)
        };

        if (normalizedSource.Version >= GameProgressData.CurrentVersion && normalizedSource.Levels != null)
        {
            foreach (LevelProgressData level in normalizedSource.Levels)
            {
                if (level == null)
                    continue;

                clone.Levels.Add(new LevelProgressData
                {
                    LevelNumber = level.LevelNumber,
                    BestScore = level.BestScore,
                    BestCompletionTimeMilliseconds = level.BestCompletionTimeMilliseconds,
                    Stars = level.Stars,
                    IsCompleted = level.IsCompleted
                });
            }
        }

        if (normalizedSource.Bonuses != null)
        {
            foreach (BonusAmountData bonus in normalizedSource.Bonuses)
            {
                if (bonus == null)
                    continue;

                clone.Bonuses.Add(new BonusAmountData
                {
                    Id = bonus.Id,
                    Amount = bonus.Amount
                });
            }
        }

        return Normalize(clone);
    }

    public static bool AreEqual(GameProgressData first, GameProgressData second)
    {
        GameProgressData normalizedFirst = Normalize(Clone(first));
        GameProgressData normalizedSecond = Normalize(Clone(second));

        if (normalizedFirst.Version != normalizedSecond.Version ||
            normalizedFirst.LegacyDataImported != normalizedSecond.LegacyDataImported ||
            normalizedFirst.EndlessBestScore != normalizedSecond.EndlessBestScore ||
            normalizedFirst.Levels.Count != normalizedSecond.Levels.Count ||
            normalizedFirst.Bonuses.Count != normalizedSecond.Bonuses.Count)
        {
            return false;
        }

        for (int i = 0; i < normalizedFirst.Levels.Count; i++)
        {
            LevelProgressData firstLevel = normalizedFirst.Levels[i];
            LevelProgressData secondLevel = normalizedSecond.Levels[i];

            if (firstLevel.LevelNumber != secondLevel.LevelNumber ||
                firstLevel.BestScore != secondLevel.BestScore ||
                firstLevel.BestCompletionTimeMilliseconds != secondLevel.BestCompletionTimeMilliseconds ||
                firstLevel.Stars != secondLevel.Stars ||
                firstLevel.IsCompleted != secondLevel.IsCompleted)
            {
                return false;
            }
        }

        for (int i = 0; i < normalizedFirst.Bonuses.Count; i++)
        {
            BonusAmountData firstBonus = normalizedFirst.Bonuses[i];
            BonusAmountData secondBonus = normalizedSecond.Bonuses[i];

            if (!string.Equals(firstBonus.Id, secondBonus.Id, StringComparison.Ordinal) ||
                firstBonus.Amount != secondBonus.Amount)
            {
                return false;
            }
        }

        return true;
    }

    private static GameProgressData Normalize(GameProgressData source)
    {
        GameProgressData data = source ?? new GameProgressData();
        data.Version = GameProgressData.CurrentVersion;
        data.EndlessBestScore = Math.Max(0, data.EndlessBestScore);
        data.Levels = NormalizeLevels(data.Levels);
        data.Bonuses = NormalizeBonuses(data.Bonuses);
        return data;
    }

    private static List<LevelProgressData> NormalizeLevels(List<LevelProgressData> levels)
    {
        List<LevelProgressData> result = new List<LevelProgressData>();

        if (levels != null)
        {
            foreach (LevelProgressData level in levels)
            {
                if (level == null || level.LevelNumber <= 0)
                    continue;

                MergeLevel(result, level);
            }
        }

        result.Sort((first, second) => first.LevelNumber.CompareTo(second.LevelNumber));
        return result;
    }

    private static List<BonusAmountData> NormalizeBonuses(List<BonusAmountData> bonuses)
    {
        List<BonusAmountData> result = new List<BonusAmountData>();

        if (bonuses != null)
        {
            foreach (BonusAmountData bonus in bonuses)
            {
                if (bonus == null || string.IsNullOrWhiteSpace(bonus.Id))
                    continue;

                MergeBonus(result, bonus);
            }
        }

        result.Sort((first, second) => string.CompareOrdinal(first.Id, second.Id));
        return result;
    }

    private static void MergeLevel(List<LevelProgressData> levels, LevelProgressData incoming)
    {
        LevelProgressData existing = levels.Find(level => level.LevelNumber == incoming.LevelNumber);

        if (existing == null)
        {
            levels.Add(new LevelProgressData
            {
                LevelNumber = incoming.LevelNumber,
                BestScore = Math.Max(0, incoming.BestScore),
                BestCompletionTimeMilliseconds = Math.Max(0, incoming.BestCompletionTimeMilliseconds),
                Stars = Math.Max(0, Math.Min(3, incoming.Stars)),
                IsCompleted = incoming.IsCompleted
            });
            return;
        }

        existing.BestScore = Math.Max(existing.BestScore, incoming.BestScore);
        existing.BestCompletionTimeMilliseconds = SelectBestTime(
            existing.BestCompletionTimeMilliseconds,
            incoming.BestCompletionTimeMilliseconds);
        existing.Stars = Math.Max(existing.Stars, Math.Max(0, Math.Min(3, incoming.Stars)));
        existing.IsCompleted |= incoming.IsCompleted;
    }

    private static long SelectBestTime(long first, long second)
    {
        if (first <= 0)
            return Math.Max(0, second);

        if (second <= 0)
            return first;

        return Math.Min(first, second);
    }

    private static void MergeBonus(List<BonusAmountData> bonuses, BonusAmountData incoming)
    {
        BonusAmountData existing = bonuses.Find(bonus => string.Equals(bonus.Id, incoming.Id, StringComparison.Ordinal));

        if (existing == null)
        {
            bonuses.Add(new BonusAmountData
            {
                Id = incoming.Id,
                Amount = Math.Max(0, incoming.Amount)
            });
            return;
        }

        existing.Amount = Math.Max(existing.Amount, incoming.Amount);
    }
}
