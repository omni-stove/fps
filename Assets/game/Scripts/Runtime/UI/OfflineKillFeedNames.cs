using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// オフラインのキルフィードの引き先。Id (<see cref="CombatEvents.OfflineId"/>) を GameObject に戻し、名前はその
    /// <see cref="Damageable.Id"/> (無ければ GameObject の名前) にする。チームは無く、<see cref="Damageable.IsLocalPlayer"/> の人を強調する。
    /// 武器の表示名は、倒した側が持っている武器 (プレイヤーは <see cref="WeaponController"/> の全武器、敵は <see cref="EnemyAgent.Weapon"/>) から引く。
    /// </summary>
    public sealed class OfflineKillFeedNames : IKillFeedNames
    {
        public KillFeedLine Describe(KillFeedEntry entry)
        {
            var killerObject = CombatEvents.OfflineObject(entry.KillerId);
            var killer = NameOf(entry.KillerId, killerObject);
            var victim = entry.IsSuicide ? killer : NameOf(entry.VictimId, CombatEvents.OfflineObject(entry.VictimId));
            return new KillFeedLine(entry, killer, victim, WeaponNameOf(killerObject, entry.WeaponId));
        }

        private static KillFeedName NameOf(int id, GameObject gameObject)
        {
            if (gameObject == null)
            {
                return new KillFeedName(KillFeedFormat.Unknown(id), null, false);
            }

            // 敵の撃ち手 (EnemyAgent) と当たり (Damageable) の付く階層がずれていても同じ人として引けるよう、子も探す。
            var damageable = gameObject.GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = gameObject.GetComponentInChildren<Damageable>(true);
            }

            return damageable != null
                ? new KillFeedName(damageable.Id, null, damageable.IsLocalPlayer)
                : new KillFeedName(gameObject.name, null, false);
        }

        private static string WeaponNameOf(GameObject killer, string weaponId)
        {
            if (killer == null || string.IsNullOrEmpty(weaponId))
            {
                return string.Empty;
            }

            // グレネードはロードアウトの武器ではないので、持っている武器からは名前を引けない。
            if (string.Equals(weaponId, GrenadeThrower.WeaponId, StringComparison.Ordinal))
            {
                return GrenadeThrower.DisplayName;
            }

            var controller = killer.GetComponentInChildren<WeaponController>(true);
            var loadout = controller != null ? controller.Loadout : null;
            if (loadout != null)
            {
                foreach (var weapon in loadout.Items)
                {
                    if (weapon != null && IsWeapon(weapon.Spec, weaponId))
                    {
                        return weapon.Spec.DisplayName;
                    }
                }
            }

            var enemy = killer.GetComponentInChildren<EnemyAgent>(true);
            var enemyWeapon = enemy != null ? enemy.Weapon : null;
            if (enemyWeapon != null && IsWeapon(enemyWeapon.Spec, weaponId))
            {
                return enemyWeapon.Spec.DisplayName;
            }

            return string.Empty;
        }

        private static bool IsWeapon(WeaponSpec spec, string weaponId)
        {
            return spec != null && string.Equals(spec.Id, weaponId, StringComparison.Ordinal);
        }
    }
}
