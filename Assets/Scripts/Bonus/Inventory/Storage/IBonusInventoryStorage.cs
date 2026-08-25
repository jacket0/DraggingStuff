public interface IBonusInventoryStorage
{
    public BonusInventoryData Load();
    public void Save(BonusInventoryData data);
}