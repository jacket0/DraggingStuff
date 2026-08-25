using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerPrefsBonusInventoryStorage", menuName = "Game/Bonuses/Storage/PlayerPrefs Inventory Storage")]
public class PlayerPrefsBonusInventoryStorage : BonusInventoryStorageAsset
{
    private const string StorageKey = "bonus.inventory.v1";

    public override BonusInventoryData Load()
    {
        if (!PlayerPrefs.HasKey(StorageKey))
            return new BonusInventoryData();

        string storedJson = PlayerPrefs.GetString(StorageKey);

        if (string.IsNullOrWhiteSpace(storedJson))
            return new BonusInventoryData();

        try
        {
            BonusInventoryData data = JsonUtility.FromJson<BonusInventoryData>(storedJson);

            if (data == null || data.Amounts == null)
                return new BonusInventoryData();

            return data;
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"Не удалось загрузить инвентарь бонусов: {exception.Message}");

            return new BonusInventoryData();
        }

    }

    public override void Save(BonusInventoryData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(StorageKey, json);
        PlayerPrefs.Save();
    }
}
