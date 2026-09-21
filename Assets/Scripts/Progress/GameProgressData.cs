using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameProgressData
{
    public const int CurrentVersion = 2;

    public int Version = CurrentVersion;
    public bool LegacyDataImported;
    public List<LevelProgressData> Levels = new List<LevelProgressData>();
    public long EndlessBestScore;
    public List<BonusAmountData> Bonuses = new List<BonusAmountData>();
}

[Serializable]
public sealed class LevelProgressData
{
    public int LevelNumber;
    public long BestScore;
    public long BestCompletionTimeMilliseconds;
    public int Stars;
    public bool IsCompleted;
}
