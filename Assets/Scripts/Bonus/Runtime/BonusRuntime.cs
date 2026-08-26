using System;
using UnityEngine;

public class BonusRuntime
{
    public IBonusEffect Effect { get; }
    public BonusRuntimePhase Phase { get; private set; }
    public int CurrentLevelUses { get; private set; }
    public float RemainingCooldown { get; private set; }

    public BonusDefinition Definition => Effect.Definition;
    private bool HasReachedLevelUseLimit => Definition.HasLevelUseLimit && CurrentLevelUses >= Definition.MaxLevelUses;

    public BonusRuntime(IBonusEffect effect)
    {
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));

        if (effect.Definition == null)
            throw new InvalidOperationException(nameof(effect.Definition));

        Phase = BonusRuntimePhase.Ready;
    }

    public void Activate()
    {
        if (Phase != BonusRuntimePhase.Ready)
            throw new InvalidOperationException($"Бонус {Definition.Id} не может активироваться из {Phase}.");

        if (HasReachedLevelUseLimit)
            throw new InvalidOperationException($"Бонус {Definition.Id} исчерпал лимит применений на уровне.");

        CurrentLevelUses++;
        RemainingCooldown = 0f;
        Phase = BonusRuntimePhase.Active;
    }

    public void CompleteActivation()
    {
        if (Phase != BonusRuntimePhase.Active)
            throw new InvalidOperationException($"Бонус {Definition.Id} не может быть завершен во время {Phase}");

        RemainingCooldown = 0f;

        if (HasReachedLevelUseLimit)
        {
            Phase = BonusRuntimePhase.Limited;
            return;
        }

        if (Definition.CooldownDuration <= 0f)
        {
            Phase = BonusRuntimePhase.Ready;
            return;
        }

        RemainingCooldown = Definition.CooldownDuration;
        Phase = BonusRuntimePhase.Recharging;
    }

    public bool TickCooldown(float deltaTime)
    {
        if (Phase != BonusRuntimePhase.Recharging)
            return false;

        if (deltaTime <= 0f)
            return false;

        RemainingCooldown = Mathf.Max(0f, RemainingCooldown - deltaTime);

        if (RemainingCooldown <= 0f)
            Phase = BonusRuntimePhase.Ready;

        return true;
    }

    public BonusRuntimeState CreateState()
    {
        float cooldownRemainingRatio = 0f;

        if (Phase == BonusRuntimePhase.Recharging && Definition.CooldownDuration > 0f)
            cooldownRemainingRatio = Mathf.Clamp01(RemainingCooldown / Definition.CooldownDuration);

        return new BonusRuntimeState(Definition, Phase, CurrentLevelUses, Definition.HasLevelUseLimit, Definition.MaxLevelUses, RemainingCooldown, cooldownRemainingRatio);
    }
}
