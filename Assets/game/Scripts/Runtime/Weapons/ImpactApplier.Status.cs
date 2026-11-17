using Shooter.Core;

namespace Shooter.Runtime
{
    public static partial class ImpactApplier
    {
        private static void ApplyStatusEffect(ImpactOrder order, ShotRequest request, Damageable target)
        {
            // 同じ着弾のダメージで倒れた相手には、燃える見た目だけが残らないよう、かけない。
            if (target == null || target.IsDead)
            {
                return;
            }

            target.EnsureStatusEffects().Afflict(order.StatusEffect, request.Shooter, request.WeaponId);
        }
    }
}
