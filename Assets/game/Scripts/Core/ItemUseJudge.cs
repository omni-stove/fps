using System;

namespace Shooter.Core
{
    public static class ItemUseJudge
    {
        public static ItemUseOutcome Judge(ItemSpec item, PickupCollector user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (item == null)
            {
                return ItemUseOutcome.Rejected(ItemUseRejection.NoItem);
            }

            if (!item.IsUsable)
            {
                return ItemUseOutcome.Rejected(ItemUseRejection.NotUsable);
            }

            if (!user.IsAlive)
            {
                return ItemUseOutcome.Rejected(ItemUseRejection.UserDown);
            }

            var health = Math.Min(item.UseHealth, user.MaxHealth - user.Health);
            var armor = user.MaxArmor > 0 ? Math.Min(item.UseArmor, user.MaxArmor - user.Armor) : 0;
            return health > 0 || armor > 0 ? ItemUseOutcome.Used(health, armor) : ItemUseOutcome.Rejected(ItemUseRejection.Full);
        }
    }
}
