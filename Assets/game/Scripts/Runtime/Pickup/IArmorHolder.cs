namespace Shooter.Runtime
{
    public interface IArmorHolder
    {
        int Armor { get; }

        int MaxArmor { get; }

        void AddArmor(int amount);
    }
}
