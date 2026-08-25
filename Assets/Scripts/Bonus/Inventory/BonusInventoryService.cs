using System;
using System.Collections.Generic;
using UnityEngine;

public class BonusInventoryService : MonoBehaviour, IBonusInventory
{
    [SerializeField] private BonusCatalog _catalog;
    [SerializeField] private BonusInventoryStorageAsset _storageAsset;

    private readonly Dictionary<BonusDefinition, int> _amounts = new Dictionary<BonusDefinition, int>();

    public event Action<BonusDefinition, int> AmountChanged;

    private IBonusInventoryStorage InventoryStorage => _storageAsset;

    private void Awake()
    {
        if (_catalog == null)
            throw new InvalidOperationException(nameof(_catalog));

        LoadAmounts();
    }

    public int GetAmount(BonusDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (!_amounts.TryGetValue(definition, out int amount))
            throw new InvalidOperationException(nameof(amount));

        return amount;
    }

    public bool TryConsume(BonusDefinition definition)
    {
        int currentAmount = GetAmount(definition);

        if (currentAmount <= 0)
            return false;

        SetAmount(definition, currentAmount - 1);
        return true;
    }

    public bool TryGrant(BonusDefinition definition)
    {
        int currentAmount = GetAmount(definition);

        if (currentAmount >= definition.MaxAmount)
            return false;

        SetAmount(definition, currentAmount + 1);
        return true;
    }

    private void SetAmount(BonusDefinition definition, int amount)
    {
        _amounts[definition] = amount;

        SaveAmounts();
        AmountChanged?.Invoke(definition, amount);
    }

    private void LoadAmounts()
    {
        BonusInventoryData data = InventoryStorage.Load();
        bool isStorageRequiresUpdate = false;
        _amounts.Clear();

        foreach (var storedAmount in data.Amounts)
        {
            if (storedAmount == null)
            {
                isStorageRequiresUpdate = true;
                continue;
            }

            if (!_catalog.TryGetDefinition(storedAmount.Id, out BonusDefinition definition))
            {
                isStorageRequiresUpdate = true;
                continue;
            }

            if (_amounts.ContainsKey(definition))
            {
                isStorageRequiresUpdate = true;
                continue;
            }

            int normalizedAmount = Mathf.Clamp(storedAmount.Amount, 0, definition.MaxAmount);

            if (normalizedAmount != storedAmount.Amount)
                isStorageRequiresUpdate = true;

            _amounts.Add(definition, normalizedAmount);
        }

        foreach (var definition in _catalog.Definitions)
        {
            if (_amounts.ContainsKey(definition))
                continue;

            _amounts.Add(definition, definition.InitialAmount);
            isStorageRequiresUpdate = true;
        }

        if (isStorageRequiresUpdate)
            SaveAmounts();
    }

    private void SaveAmounts()
    {
        BonusInventoryData data = new BonusInventoryData();

        foreach (var definition in _catalog.Definitions)
        {
            data.Amounts.Add(new BonusAmountData
            {
                Id = definition.Id,
                Amount = _amounts[definition]
            });
        }
    
        InventoryStorage.Save(data);
    }
}
