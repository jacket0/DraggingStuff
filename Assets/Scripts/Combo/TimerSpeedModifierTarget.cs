using System;
using UnityEngine;

public abstract class TimerSpeedModifierTarget : MonoBehaviour
{
    private readonly MultiplierModifierCollection _timerSpeedModifiers = new MultiplierModifierCollection();

    protected float TimerSpeedMultiplier => _timerSpeedModifiers.CombinedMultiplier;

    public IDisposable AddTimerSpeedMultiplier(float multiplier)
    {
        return _timerSpeedModifiers.AddMultiplier(multiplier);
    }
}
