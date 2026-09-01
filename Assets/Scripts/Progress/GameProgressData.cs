using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameProgressData
{
    public int Version = 1;
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
    public bool IsCompleted;
}
