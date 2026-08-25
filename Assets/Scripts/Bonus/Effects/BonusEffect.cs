using System;
using UnityEngine;

public abstract class BonusEffect : MonoBehaviour, IBonusEffect
{
    [SerializeField] private BonusDefinition _definition;

    public BonusDefinition Definition => _definition;
    public bool IsActive { get; private set; }

    public event Action<IBonusEffect> Completed;

    public bool CanActivate()
    {
        return _definition != null && !IsActive && CanActivateEffect();
    }

    public void Activate()
    {
        if (_definition == null)
            throw new InvalidOperationException(nameof(_definition));

        if (IsActive)
            throw new InvalidOperationException($"Бонус {_definition.Id} уже активен.");

        IsActive = true;
        ActivateEffect();
    }

    protected void CompleteEffect()
    {
        if (!IsActive)
            return;

        IsActive = false;
        Completed?.Invoke(this);
    }

    protected abstract void ActivateEffect();
    protected abstract bool CanActivateEffect();
}
