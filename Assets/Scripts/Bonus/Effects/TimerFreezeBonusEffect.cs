using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class TimerFreezeBonusEffect : BonusEffect
{
    private const float FrozenTimerSpeedMultiplier = 0f;

    [FormerlySerializedAs("_comboSystem")]
    [SerializeField] private TimerSpeedModifierTarget _primaryTarget;
    [SerializeField] private List<TimerSpeedModifierTarget> _additionalTargets = new List<TimerSpeedModifierTarget>();
    [SerializeField, Min(0.1f)] private float _freezeDuration = 4f;

    private readonly List<IDisposable> _timerSpeedModifiers = new List<IDisposable>();

    private Coroutine _completionCoroutine;

    private void OnDisable()
    {
        StopCompletionCoroutine();
        ReleaseTimerSpeedModifier();
        CompleteEffect();
    }

    protected override bool CanActivateEffect()
    {
        if (_primaryTarget == null || !isActiveAndEnabled)
            return false;

        foreach (TimerSpeedModifierTarget target in _additionalTargets)
        {
            if (target == null || target == _primaryTarget)
                return false;
        }

        return true;
    }

    protected override void ActivateEffect()
    {
        _timerSpeedModifiers.Add(_primaryTarget.AddTimerSpeedMultiplier(FrozenTimerSpeedMultiplier));

        foreach (TimerSpeedModifierTarget target in _additionalTargets)
            _timerSpeedModifiers.Add(target.AddTimerSpeedMultiplier(FrozenTimerSpeedMultiplier));

        _completionCoroutine = StartCoroutine(CompleteAfterFreezeDuration());
    }

    private IEnumerator CompleteAfterFreezeDuration()
    {
        yield return new WaitForSeconds(_freezeDuration);

        _completionCoroutine = null;

        ReleaseTimerSpeedModifier();
        CompleteEffect();
    }

    private void ReleaseTimerSpeedModifier()
    {
        foreach (IDisposable modifier in _timerSpeedModifiers)
            modifier.Dispose();

        _timerSpeedModifiers.Clear();
    }

    private void StopCompletionCoroutine()
    {
        if (_completionCoroutine == null)
            return;

        StopCoroutine(_completionCoroutine);
        _completionCoroutine = null;
    }
}
