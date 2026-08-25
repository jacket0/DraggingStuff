using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BonusMenuCardView : MonoBehaviour
{
    [SerializeField] private BonusDefinition _definition;
    [SerializeField] private BonusInventoryService _inventoryService;
    [SerializeField] private BonusRewardedAdService _rewardedAdService;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private Button _rewardedButton;

    private bool _isInitialized;

    private IBonusInventory Inventory => _inventoryService;

    private void OnEnable()
    {
        _rewardedButton.onClick.AddListener(HandleRewardedButtonClicked);
        Inventory.AmountChanged += HandleAmountChanged;
        _rewardedAdService.AvailabilityChanged += Refresh;

        if (_isInitialized)
            Refresh();
    }

    private void OnDisable()
    {
        _rewardedButton.onClick.RemoveListener(HandleRewardedButtonClicked);
        Inventory.AmountChanged -= HandleAmountChanged;
        _rewardedAdService.AvailabilityChanged -= Refresh;
    }

    private void Start()
    {
        ApplyIcon();
        _isInitialized = true;
        Refresh();
    }

    private void OnValidate()
    {
        ApplyIcon();
    }

    private void HandleRewardedButtonClicked()
    {
        if (!_rewardedAdService.TryShow(_definition))
            Refresh();
    }

    private void HandleAmountChanged(BonusDefinition definition, int amount)
    {
        if (!_isInitialized || definition != _definition)
            return;

        Render(amount);
    }

    private void Refresh()
    {
        if (!_isInitialized)
            return;

        Render(Inventory.GetAmount(_definition));
    }

    private void Render(int amount)
    {
        _amountText.SetText("x{0}", amount);
        _rewardedButton.interactable = _rewardedAdService.CanShow(_definition);
    }

    private void ApplyIcon()
    {
        if (_icon == null)
            return;

        _icon.sprite = _definition == null ? null : _definition.Icon;
        _icon.enabled = _icon.sprite != null;
    }
}
