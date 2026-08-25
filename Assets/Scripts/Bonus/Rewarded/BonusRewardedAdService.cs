using System;
using UnityEngine;

public sealed class BonusRewardedAdService : MonoBehaviour
{
    private const string RewardIdPrefix = "bonus.";

    [SerializeField] private BonusCatalog _catalog;
    [SerializeField] private BonusInventoryService _inventoryService;
    [SerializeField] private YandexRewardedAdService _rewardedAdService;

    public event Action AvailabilityChanged;

    private IBonusInventory Inventory => _inventoryService;
    private IRewardedAdService RewardedAdService => _rewardedAdService;

    private void OnEnable()
    {
        Inventory.AmountChanged += HandleAmountChanged;
        RewardedAdService.RewardGranted += HandleRewardGranted;
        RewardedAdService.ShowingStateChanged += HandleShowingStateChanged;
    }

    private void OnDisable()
    {
        Inventory.AmountChanged -= HandleAmountChanged;
        RewardedAdService.RewardGranted -= HandleRewardGranted;
        RewardedAdService.ShowingStateChanged -= HandleShowingStateChanged;
    }

    public bool CanShow(BonusDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        return RewardedAdService.IsSupported
            && !RewardedAdService.IsShowing
            && Inventory.GetAmount(definition) < definition.MaxAmount;
    }

    public bool TryShow(BonusDefinition definition)
    {
        if (!CanShow(definition))
            return false;

        return RewardedAdService.TryShow(CreateRewardId(definition));
    }

    private void HandleAmountChanged(BonusDefinition definition, int amount)
    {
        AvailabilityChanged?.Invoke();
    }

    private void HandleShowingStateChanged()
    {
        AvailabilityChanged?.Invoke();
    }

    private void HandleRewardGranted(string rewardId)
    {
        if (!TryGetDefinition(rewardId, out BonusDefinition definition))
            return;

        if (!Inventory.TryGrant(definition))
            Debug.LogWarning($"Бонус {definition.Id} не выдан: достигнут максимальный запас");
    }

    private bool TryGetDefinition(string rewardId, out BonusDefinition definition)
    {
        if (string.IsNullOrEmpty(rewardId) || !rewardId.StartsWith(RewardIdPrefix, StringComparison.Ordinal))
        {
            definition = null;
            return false;
        }

        string definitionId = rewardId.Substring(RewardIdPrefix.Length);
        return _catalog.TryGetDefinition(definitionId, out definition);
    }

    private static string CreateRewardId(BonusDefinition definition)
    {
        return RewardIdPrefix + definition.Id;
    }
}
