using UnityEngine;

public abstract class BonusInventoryStorageAsset : ScriptableObject, IBonusInventoryStorage
{
    public abstract BonusInventoryData Load();

    public abstract void Save(BonusInventoryData data);
}
