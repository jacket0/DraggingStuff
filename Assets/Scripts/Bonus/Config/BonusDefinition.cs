using UnityEngine;

[CreateAssetMenu(fileName = "BonusDefinition", menuName = "Game/Bonuses/Bonus Definition")]
public class BonusDefinition : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private Sprite _icon;
    [SerializeField, Min(0)] private int _initialAmount = 1;
    [SerializeField, Min(1)] private int _maxAmount = 3;
    [SerializeField, Min(0)] private float _cooldownDuration;
    [SerializeField] private bool _hasLevelUseLimit = true;
    [SerializeField, Min(1)] private int _maxLevelUses = 2;

    public string Id => _id;
    public Sprite Icon => _icon;
    public int InitialAmount => _initialAmount;
    public int MaxAmount => _maxAmount;
    public float CooldownDuration => _cooldownDuration;
    public bool HasLevelUseLimit => _hasLevelUseLimit;
    public int MaxLevelUses => _maxLevelUses;

    private void OnValidate()
    {
        _initialAmount = Mathf.Clamp(_initialAmount, 0, _maxAmount);
    }
}
