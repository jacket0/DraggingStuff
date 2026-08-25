using System;
using System.Collections.Generic;
using UnityEngine;

public class BonusUseController : MonoBehaviour, IBonusUseService, IBonusRuntimeStateSource
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private BonusInventoryService _inventory;
    [SerializeField] private List<BonusEffect> _effects = new List<BonusEffect>();

    private readonly Dictionary<BonusDefinition, BonusRuntime> _runtimes = new Dictionary<BonusDefinition, BonusRuntime>();

    public event Action<BonusRuntimeState> RuntimeStateChanged;

    private void Awake()
    {
        RegisterEffects();
    }

    private void Update()
    {
        if (!_session.IsPlaying)
            return;

        foreach (var runtime in _runtimes.Values)
        {
            if (!runtime.TickCooldown(Time.deltaTime))
                continue;

            RuntimeStateChanged?.Invoke(runtime.CreateState());
        }
    }

    private void OnDestroy()
    {
        foreach (var runtime in _runtimes.Values)
        {
            if (runtime.Effect != null)
                runtime.Effect.Completed -= HandleEffectCompleted;
        }
    }

    public BonusUseResult TryUse(BonusDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (!_session.IsPlaying)
            return BonusUseResult.LevelUnavailable;

        if (!_runtimes.TryGetValue(definition, out var runtime))
            return BonusUseResult.NotRegistered;

        switch (runtime.Phase)
        {
            case BonusRuntimePhase.Active:
                return BonusUseResult.AlreadyActive;

            case BonusRuntimePhase.Recharging:
                return BonusUseResult.Recharging;

            case BonusRuntimePhase.Limited:
                return BonusUseResult.UsageLimit;
        }

        if (_inventory.GetAmount(definition) <= 0)
            return BonusUseResult.NotEnough;

        if (!runtime.Effect.CanActivate())
            return BonusUseResult.EffectUnavailable;

        if (!_inventory.TryConsume(definition))
            return BonusUseResult.NotEnough;

        runtime.Activate();
        RuntimeStateChanged?.Invoke(runtime.CreateState());
        runtime.Effect.Activate();

        return BonusUseResult.Applied;
    }

    public BonusRuntimeState GetState(BonusDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (!_runtimes.TryGetValue(definition, out BonusRuntime runtime))
            throw new InvalidOperationException($"Бонус {definition.Id} не существует");

        return runtime.CreateState();
    }

    private void RegisterEffects()
    {
        foreach (var effect in _effects)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            if (effect.Definition == null)
                throw new InvalidOperationException(nameof(effect.Definition));

            if (_runtimes.ContainsKey(effect.Definition))
                throw new InvalidOperationException($"Бонус {effect.Definition.Id} уже существует");

            BonusRuntime runtime = new BonusRuntime(effect);
            _runtimes.Add(effect.Definition, runtime);
            effect.Completed += HandleEffectCompleted;
        }
    }

    private void HandleEffectCompleted(IBonusEffect effect)
    {
        if (effect == null)
            throw new ArgumentNullException(nameof(effect));

        if (!_runtimes.TryGetValue(effect.Definition, out BonusRuntime runtime))
            throw new InvalidOperationException($"Бонус {effect.Definition.Id} не существует");

        runtime.CompleteActivation();
        RuntimeStateChanged?.Invoke(runtime.CreateState());
    }
}
