namespace Shooter.Core
{
    public readonly struct ItemUseOutcome
    {
        private ItemUseOutcome(ItemUseRejection rejection, int health, int armor)
        {
            Rejection = rejection;
            Health = health;
            Armor = armor;
        }

        public ItemUseRejection Rejection { get; }

        public bool IsUsed => Rejection == ItemUseRejection.None;

        public int Health { get; }

        public int Armor { get; }

        public static ItemUseOutcome Rejected(ItemUseRejection rejection)
        {
            if (rejection == ItemUseRejection.None)
            {
                throw new System.ArgumentException("rejection must not be None", nameof(rejection));
            }

            return new ItemUseOutcome(rejection, 0, 0);
        }

        public static ItemUseOutcome Used(int health, int armor)
        {
            return new ItemUseOutcome(ItemUseRejection.None, health, armor);
        }
    }
}
