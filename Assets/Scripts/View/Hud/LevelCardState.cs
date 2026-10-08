using System;
using UnityEngine;

public readonly struct LevelCardState
{
    public LevelCardState(
        LevelEntry level,
        bool isUnlocked,
        bool isCurrent,
        bool isNew,
        long bestScore,
        long bestTimeMilliseconds,
        int stars,
        Sprite mechanicIcon,
        int mechanicElementCount,
        Sprite secondaryMechanicIcon,
        int secondaryMechanicElementCount)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        IsUnlocked = isUnlocked;
        IsCurrent = isCurrent;
        IsNew = isNew;
        BestScore = bestScore;
        BestTimeMilliseconds = bestTimeMilliseconds;
        Stars = stars;
        MechanicIcon = mechanicIcon;
        MechanicElementCount = mechanicElementCount;
        SecondaryMechanicIcon = secondaryMechanicIcon;
        SecondaryMechanicElementCount = secondaryMechanicElementCount;
    }

    public LevelEntry Level { get; }
    public bool IsUnlocked { get; }
    public bool IsCurrent { get; }
    public bool IsNew { get; }
    public long BestScore { get; }
    public long BestTimeMilliseconds { get; }
    public int Stars { get; }
    public Sprite MechanicIcon { get; }
    public int MechanicElementCount { get; }
    public Sprite SecondaryMechanicIcon { get; }
    public int SecondaryMechanicElementCount { get; }
}
