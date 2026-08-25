using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BonusButtonView : MonoBehaviour
{
    [SerializeField] private BonusDefinition _definition;
    [SerializeField] private BonusUseController _useController;
    [SerializeField] private BonusInventoryService _inventoryService;

    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _amountText;

    [SerializeField] private GameObject _activeIndicator;
    [SerializeField] private GameObject _cooldownRoot;
    [SerializeField] private Image _cooldownFill;
    [SerializeField] private TMP_Text _cooldownText;
    [SerializeField] private GameObject _unavailableShade;

    private bool _isInitialized;
    private int _currentAmount;
    private BonusRuntimeState _runtimeState;

    private IBonusUseService UseService => _useController;
    private IBonusRuntimeStateSource RuntimeStateSource => _useController;
    private IBonusInventory Inventory => _inventoryService;

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleUseButtonClicked);
        Inventory.AmountChanged += HandleAmountChanged;
        RuntimeStateSource.RuntimeStateChanged += HandleRuntimeStateChanged;

        if (_isInitialized)
            Refresh();
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(HandleUseButtonClicked);
        Inventory.AmountChanged -= HandleAmountChanged;
        RuntimeStateSource.RuntimeStateChanged -= HandleRuntimeStateChanged;
    }

    private void Start()
    {
        _icon.sprite = _definition.Icon;
        _isInitialized = true;
        Refresh();
    }

    private void Refresh()
    {
        _currentAmount = Inventory.GetAmount(_definition);
        _runtimeState = RuntimeStateSource.GetState(_definition);
        Render();
    }

    private void HandleUseButtonClicked()
    {
        BonusUseResult useResult = UseService.TryUse(_definition);

        if (useResult != BonusUseResult.Applied)
            Refresh();
    }

    private void HandleAmountChanged(BonusDefinition definition, int amount)
    {
        if (!_isInitialized || definition != _definition)
            return;

        _currentAmount = amount;
        Render();
    }

    private void HandleRuntimeStateChanged(BonusRuntimeState state)
    {
        if (!_isInitialized || state.Definition != _definition)
            return;

        _runtimeState = state;
        Render();
    }

    private void Render()
    {
        _amountText.SetText("x{0}", _currentAmount);

        bool isReady = _runtimeState.Phase == BonusRuntimePhase.Ready;
        bool isActive = _runtimeState.Phase == BonusRuntimePhase.Active;
        bool isRecharging = _runtimeState.Phase == BonusRuntimePhase.Recharging;
        bool isLimited = _runtimeState.Phase == BonusRuntimePhase.Limited;

        _button.interactable = isReady && _currentAmount > 0;

        _activeIndicator.SetActive(isActive);
        _cooldownRoot.SetActive(isRecharging);
        _unavailableShade.SetActive(isLimited);

        _cooldownFill.fillAmount = isRecharging ? _runtimeState.CooldownRemainingRatio : 0f;

        if (isRecharging)
            _cooldownText.SetText("{0}", Mathf.CeilToInt(_runtimeState.RemainingCooldown));
        else
            _cooldownText.SetText(string.Empty);
    }
}
