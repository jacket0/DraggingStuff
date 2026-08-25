using System;

public interface IComboTimerModifierTarget
{
    public IDisposable AddTimerSpeedMultiplier(float multiplier);
}
