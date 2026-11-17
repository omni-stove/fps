using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // 撃った者が持つ定義を先に見るのは、カタログに無い武器 (試験や差し替えた武器) の見た目も引けるようにするため。
    public static class ShooterWeapons
    {
        public static WeaponDefinition Find(GameObject shooter, string weaponId)
        {
            if (weaponId == null)
            {
                return null;
            }

            if (shooter != null)
            {
                var controller = shooter.GetComponentInChildren<WeaponController>(true);
                var held = controller != null ? controller.Definition : null;
                if (held != null && string.Equals(held.Id, weaponId, StringComparison.Ordinal))
                {
                    return held;
                }

                var enemy = shooter.GetComponentInChildren<EnemyAgent>(true);
                var carried = enemy != null ? enemy.WeaponDefinition : null;
                if (carried != null && string.Equals(carried.Id, weaponId, StringComparison.Ordinal))
                {
                    return carried;
                }
            }

            var catalog = GameContent.Pick(c => c.WeaponCatalog);
            return catalog != null && catalog.TryFind(weaponId, out var definition) ? definition : null;
        }
    }
}
