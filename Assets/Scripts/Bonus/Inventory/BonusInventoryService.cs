using System;
using System.Collections.Generic;
using UnityEngine;

public class BonusInventoryService : MonoBehaviour
{
    private const string StorageKey = "bonus.inventory.v1";

    [SerializeField] private BonusCatalog _catalog;

    private readonly Dictionary<BonusId, int> _amounts = new Dictionary<BonusId, int>();

    public event Action<BonusId, int> AmountChanged;

    [Serializable]
    private class BonusInventoryData
    {
        public List<BonusAmountData> Amounts = new List<BonusAmountData>();
    }

    [Serializable]
    private class BonusAmountData
    {
        public BonusId Id;
        public int Amount;
    }

    private void Awake()
    {
        if (_catalog == null)
            throw new InvalidOperationException(nameof(_catalog));

        LoadAmounts();
    }

    public int GetAmount(BonusId bonusId)
    {
        if (!_amounts.TryGetValue(bonusId, out int amount))
            throw new InvalidOperationException(nameof(amount));

        return amount;
    }

    public bool TryConsume(BonusId bonusId)
    {
        int currentAmount = GetAmount(bonusId);

        if (currentAmount <= 0)
            return false;

        SetAmount(bonusId, currentAmount - 1);
        return true;
    }

    public bool TryGrant(BonusId bonusId)
    {
        BonusDefinition definition = GetDefinition(bonusId);
        int currentAmount = GetAmount(bonusId);

        if (currentAmount >= definition.MaxAmount)
            return false;

        SetAmount(bonusId, currentAmount + 1);
        return true;
    }

    private BonusDefinition GetDefinition(BonusId bonusId)
    {
        if (!_catalog.TryGetDefinition(bonusId, out BonusDefinition definition))
            throw new InvalidOperationException(nameof(definition));

        return definition;
    }

    private void SetAmount(BonusId bonusId, int amount)
    {
        _amounts[bonusId] = amount;

        Save();
        AmountChanged?.Invoke(bonusId, amount);
    }

    private void LoadAmounts()
    {
        BonusInventoryData data = LoadData(out bool shouldRewriteStorage);
        _amounts.Clear();

        foreach (var storedAmount in data.Amounts)
        {
            if (storedAmount == null)
            {
                shouldRewriteStorage = true;
                continue;
            }

            if (!_catalog.TryGetDefinition(storedAmount.Id, out var defininition))
            {
                shouldRewriteStorage = true;
                continue;
            }

            if (_amounts.ContainsKey(storedAmount.Id))
            {
                shouldRewriteStorage = true;
                continue;
            }

            int normalizedAmount = Mathf.Clamp(storedAmount.Amount, 0, defininition.MaxAmount);

            if (normalizedAmount != storedAmount.Amount)
                shouldRewriteStorage = true;

            _amounts.Add(storedAmount.Id, normalizedAmount);
        }

        foreach (var definition in _catalog.Definitions)
        {
            if (_amounts.ContainsKey(definition.Id))
                continue;

            _amounts.Add(definition.Id, definition.InitialAmount);
            shouldRewriteStorage = true;
        }

        if (shouldRewriteStorage)
            Save();
    }

    private BonusInventoryData LoadData(out bool shouldRewriteStorage)
    {
        shouldRewriteStorage = false;

        if (!PlayerPrefs.HasKey(StorageKey))
        {
            shouldRewriteStorage = true;
            return new BonusInventoryData();
        }

        string json = PlayerPrefs.GetString(StorageKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            shouldRewriteStorage = true;
            return new BonusInventoryData();
        }

        try
        {
            BonusInventoryData data = JsonUtility.FromJson<BonusInventoryData>(json);

            if (data == null || data.Amounts == null)
            {
                shouldRewriteStorage = true;
                return new BonusInventoryData();
            }

            return data;
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"Не удалось загрузить инвентарь: {exception.Message}");

            shouldRewriteStorage = true;
            return new BonusInventoryData();
        }
    }

    private void Save()
    {
        BonusInventoryData data = new BonusInventoryData();

        foreach (var definition in _catalog.Definitions)
        {
            data.Amounts.Add(new BonusAmountData
            {
                Id = definition.Id,
                Amount = _amounts[definition.Id]
            });
        }

        string json = JsonUtility.ToJson(data);

        PlayerPrefs.SetString(StorageKey, json);
        PlayerPrefs.Save();
    }
}
