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
    [SerializeField] private Button _cardButton;

    private bool _isInitialized;

    private IBonusInventory Inventory => _inventoryService;

    private void Awake()
    {
        ValidateDependencies();
        _cardButton.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        _cardButton?.onClick.RemoveListener(HandleClicked);
    }

    private void OnEnable()
    {
        Inventory.AmountChanged += HandleAmountChanged;
        _rewardedAdService.AvailabilityChanged += Refresh;

        if (_isInitialized)
            Refresh();
    }

    private void OnDisable()
    {
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

    private void HandleClicked()
    {
        if (!_cardButton.interactable)
            return;

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
        _cardButton.interactable = _rewardedAdService.CanShow(_definition);
    }

    private void ApplyIcon()
    {
        if (_icon == null)
            return;

        _icon.sprite = _definition == null ? null : _definition.Icon;
        _icon.enabled = _icon.sprite != null;
    }

    private void ValidateDependencies()
    {
        if (_definition == null)
            throw new MissingReferenceException(nameof(_definition));

        if (_inventoryService == null)
            throw new MissingReferenceException(nameof(_inventoryService));

        if (_rewardedAdService == null)
            throw new MissingReferenceException(nameof(_rewardedAdService));

        if (_icon == null)
            throw new MissingReferenceException(nameof(_icon));

        if (_amountText == null)
            throw new MissingReferenceException(nameof(_amountText));

        if (_cardButton == null)
            throw new MissingReferenceException(nameof(_cardButton));
    }
}
