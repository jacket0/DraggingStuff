public readonly struct BonusRuntimeState
{
    public BonusDefinition Definition { get; }
    public BonusRuntimePhase Phase { get; }
    public int CurrentLevelUses { get; }
    public bool HasLevelUseLimit { get; }
    public int MaxLevelUses { get; }
    public float RemainingCooldown { get; }
    public float CooldownRemainingRatio { get; }

    public int? LevelRemainingUses => HasLevelUseLimit ? MaxLevelUses - CurrentLevelUses : null;
    public BonusRuntimeState(BonusDefinition definition, BonusRuntimePhase phase, int currentLevelUses, bool hasLevelUseLimit, int maxLevelUses, float remainingCooldown, float cooldownRemainingRatio)
    {
        Definition = definition;
        Phase = phase;
        CurrentLevelUses = currentLevelUses;
        HasLevelUseLimit = hasLevelUseLimit;
        MaxLevelUses = maxLevelUses;
        RemainingCooldown = remainingCooldown;
        CooldownRemainingRatio = cooldownRemainingRatio;
    }
}
