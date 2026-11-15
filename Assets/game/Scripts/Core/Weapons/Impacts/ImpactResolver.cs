using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public static partial class ImpactResolver
    {
        public static IReadOnlyList<ImpactOrder> Resolve(ImpactSpec impact, ImpactHit hit)
        {
            return Resolve(impact, hit, null, 0f);
        }

        public static IReadOnlyList<ImpactOrder> Resolve(ImpactSpec impact, ImpactHit hit, ChargeSpec charge, float chargeFraction)
        {
            return Resolve(impact, hit, charge, chargeFraction, 1f);
        }

        // damageMultiplier は貫通・跳弾のように、当たるまでに弾が弱った分。弾そのもののダメージにだけ掛け、爆発などには掛けない。
        public static IReadOnlyList<ImpactOrder> Resolve(
            ImpactSpec impact, ImpactHit hit, ChargeSpec charge, float chargeFraction, float damageMultiplier)
        {
            if (!(damageMultiplier >= 0f) || float.IsInfinity(damageMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(damageMultiplier), damageMultiplier, "damageMultiplier must be a non-negative finite number");
            }

            if (impact == null)
            {
                throw new ArgumentNullException(nameof(impact));
            }

            var orders = new List<ImpactOrder>();
            var baseDamage = ScaleDamage(impact.Damage, ChargeScaling.DamageMultiplier(charge, chargeFraction) * damageMultiplier);
            if (hit.IsCharacter)
            {
                orders.Add(ImpactOrder.Damage(DamageCalculator.Calculate(baseDamage, hit.Zone, hit.Distance, impact.Falloff, hit.Multipliers)));
            }
            else
            {
                // 建造物は部位も距離減衰も持たないので、オフラインとサーバーの今の射撃と同じく減衰の前の値を渡す。
                orders.Add(ImpactOrder.StructureDamage(baseDamage, impact.StructureRadius, hit.Point));
            }

            AddExplosion(impact, hit, orders);
            AddStatusEffect(impact, hit, orders);
            AddAreaEffect(impact, hit, orders);
            return orders;
        }

        private static int ScaleDamage(int damage, float multiplier)
        {
            if (!(multiplier >= 0f) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "multiplier must be a non-negative finite number");
            }

            var scaled = Math.Round((double)damage * multiplier, MidpointRounding.AwayFromZero);
            return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
        }
    }
}
