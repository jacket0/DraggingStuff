using System;

public interface IBonusRuntimeStateSource
{
    public event Action<BonusRuntimeState> RuntimeStateChanged;

    public BonusRuntimeState GetState(BonusDefinition definition);
}