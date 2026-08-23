using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BonusRuntimeState
{
    public BonusId Id { get; }
    public BonusRuntimePhase Phase { get; }
    public int CurrentLevelUses { get; }
    public int MaxLevelUses { get; }
    public float RemainingCooldown { get; }
    public float CooldownRemainingRatio { get; }

    public int LevelRemainingUses => MaxLevelUses - CurrentLevelUses;

    public BonusRuntimeState(BonusId id, BonusRuntimePhase phase, int currentLevelUses, int maxLevelUses, float remainingCooldown, float cooldownRemainingRatio)
    {
        Id = id;
        Phase = phase;
        CurrentLevelUses = currentLevelUses;
        MaxLevelUses = maxLevelUses;
        RemainingCooldown = remainingCooldown;
        CooldownRemainingRatio = cooldownRemainingRatio;
    }
}
