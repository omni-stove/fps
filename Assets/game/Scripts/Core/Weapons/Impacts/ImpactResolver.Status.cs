using System.Collections.Generic;

namespace Shooter.Core
{
    public static partial class ImpactResolver
    {
        private static void AddStatusEffect(ImpactSpec impact, ImpactHit hit, List<ImpactOrder> orders)
        {
            // 建造物は燃えても体力を持たないので、かけるのはキャラに当たったときだけにする。
            if (impact.StatusEffect != null && hit.IsCharacter)
            {
                orders.Add(ImpactOrder.Afflict(impact.StatusEffect, hit.Point));
            }
        }
    }
}
