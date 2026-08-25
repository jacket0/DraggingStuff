using System;

public interface IBonusEffect
{
    public BonusDefinition Definition { get; }
    public bool IsActive { get; }

    public event Action<IBonusEffect> Completed;

    public bool CanActivate();
    public void Activate();
}