using System;
using System.Collections;
using UnityEngine;

public class ComboFreezeBonusEffect : BonusEffect
{
    private const float FrozenTimerSpeedMultiplier = 0f;

    [SerializeField] private ComboSystem _comboSystem;
    [SerializeField, Min(0.1f)] private float _freezeDuration = 4f;

    private IDisposable _timerSpeedModifier;
    private Coroutine _completionCoroutine;

    private IComboTimerModifierTarget TimerModifierTarget => _comboSystem;

    private void OnDisable()
    {
        StopCompletionCoroutine();
        ReleaseTimerSpeedModifier();
        CompleteEffect();
    }

    protected override bool CanActivateEffect()
    {
        return _comboSystem != null && isActiveAndEnabled;
    }

    protected override void ActivateEffect()
    {
        _timerSpeedModifier = TimerModifierTarget.AddTimerSpeedMultiplier(FrozenTimerSpeedMultiplier);
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
        if (_timerSpeedModifier == null)
            return;

        _timerSpeedModifier.Dispose();
        _timerSpeedModifier = null;
    }

    private void StopCompletionCoroutine()
    {
        if (_completionCoroutine == null)
            return;

        StopCoroutine(_completionCoroutine);
        _completionCoroutine = null;
    }
}
