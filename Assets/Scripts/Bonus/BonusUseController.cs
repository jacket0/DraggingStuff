using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BonusUseController : MonoBehaviour
{
    [SerializeField] private LevelSession _session;
    [SerializeField] private BonusInventoryService _inventory;
    [SerializeField] private List<BonusEffect> _effects = new List<BonusEffect>();

    private readonly Dictionary<BonusId, BonusRuntime> _runtimes = new Dictionary<BonusId, BonusRuntime>();

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
            if (runtime.Effect!=null)
                runtime.Effect.Completed -= HandleEffectCompleted;
        }
    }

    public BonusUseResult TryUse(BonusId id)
    {
        if (!_session.IsPlaying)
            return BonusUseResult.LevelUnavailable;

        if (!_runtimes.TryGetValue(id, out var runtime))
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

        if (_inventory.GetAmount(id) <= 0)
            return BonusUseResult.NotEnough;

        if (!runtime.Effect.CanActivate())
            return BonusUseResult.EffectUnavailable;

        if (!_inventory.TryConsume(id))
            return BonusUseResult.NotEnough;

        runtime.Activate();
        RuntimeStateChanged?.Invoke(runtime.CreateState());
        runtime.Effect.Activate();

        return BonusUseResult.Applied;
    }

    public BonusRuntimeState GetSate(BonusId id)
    {
        if (!_runtimes.TryGetValue(id, out BonusRuntime runtime))
            throw new InvalidOperationException($"Бонус {id} не существует");

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
            
            if (effect.Id == BonusId.None)
                throw new InvalidOperationException(nameof(effect.name));

            if (_runtimes.ContainsKey(effect.Id))
                throw new InvalidOperationException($"Бонус {effect.Id} уже существует");

            BonusRuntime runtime = new BonusRuntime(effect);
            _runtimes.Add(effect.Id, runtime);
            effect.Completed += HandleEffectCompleted;
        }
    }

    private void HandleEffectCompleted(BonusId id)
    {
        if (_runtimes.TryGetValue(id, out BonusRuntime runtime))
            throw new InvalidOperationException($"Бонус {id} не существует");

        runtime.CompleteActivation();
        RuntimeStateChanged?.Invoke(runtime.CreateState());
    }
}
