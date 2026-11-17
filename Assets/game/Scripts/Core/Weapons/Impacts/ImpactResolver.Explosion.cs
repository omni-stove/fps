using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public static partial class ImpactResolver
    {
        // 面の上で起爆すると、爆風を遮るかを調べる線がその面の中から始まり、面の向こうの相手にも遮られずに届いてしまう。
        public const float DetonationStandoff = 0.05f;

        public static IReadOnlyList<ImpactOrder> ResolveDetonation(ImpactSpec impact, DetonationTrigger cause, Vector3 point)
        {
            if (impact == null)
            {
                throw new ArgumentNullException(nameof(impact));
            }

            var orders = new List<ImpactOrder>(1);
            AddDetonation(impact, SpecGuard.Defined(cause, nameof(cause)), point, orders);
            return orders;
        }

        public static Vector3 DetonationPoint(Vector3 point, Vector3 normal)
        {
            var length = normal.Length();
            if (!(length > 0f) || float.IsInfinity(length))
            {
                return point;
            }

            return point + (normal * (DetonationStandoff / length));
        }

        private static void AddExplosion(ImpactSpec impact, ImpactHit hit, List<ImpactOrder> orders)
        {
            AddDetonation(impact, DetonationTrigger.OnImpact, hit.Point, orders);
        }

        private static void AddDetonation(ImpactSpec impact, DetonationTrigger cause, Vector3 point, List<ImpactOrder> orders)
        {
            var explosion = impact.Explosion;
            if (explosion != null && explosion.Detonation == cause)
            {
                orders.Add(ImpactOrder.Detonate(explosion, point));
            }
        }
    }
}
