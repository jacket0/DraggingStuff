using System;
using UnityEngine;

public abstract class BonusEffect : MonoBehaviour
{
    [SerializeField] private BonusDefinition _definition;

    public BonusDefinition Definition => _definition;
    public BonusId Id => _definition != null ? _definition.Id : BonusId.None;
    public bool IsActive { get; private set; }

    public event Action<BonusId> Completed;

    public bool CanActivate()
    {
        return _definition != null && !IsActive && CanActivateEffect();
    }

    public void Activate()
    {
        if (_definition == null)
            throw new InvalidOperationException($"{name} has no bonus definition.");

        if (IsActive)
            throw new InvalidOperationException($"Bonus {Id} is already active.");

        IsActive = true;
        ActivateEffect();
    }

    protected void CompleteEffect()
    {
        if (!IsActive)
            return;

        IsActive = false;
        Completed?.Invoke(Id);
    }

    protected abstract void ActivateEffect();
    protected abstract bool CanActivateEffect();
}
