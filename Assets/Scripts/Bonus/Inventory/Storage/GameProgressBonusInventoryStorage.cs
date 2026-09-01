using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GameProgressBonusInventoryStorage", menuName = "Game/Bonuses/Storage/Game Progress Inventory Storage")]
public sealed class GameProgressBonusInventoryStorage : BonusInventoryStorageAsset
{
    public override BonusInventoryData Load()
    {
        return GameProgressRepository.LoadBonusInventory();
    }

    public override void Save(BonusInventoryData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        GameProgressRepository.SaveBonusInventory(data);
    }
}
