using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// オフラインのキルフィードの引き先。名前・チーム・自分かどうかは <see cref="OfflineParticipants"/> から引く。
    /// 武器の名前 (<see cref="WeaponSpec.Name"/>) は、倒した側が持っている武器 (プレイヤーは <see cref="WeaponController"/> の全武器、敵は <see cref="EnemyAgent.Weapon"/>) から引く。
    /// </summary>
    public sealed class OfflineKillFeedNames : IKillFeedNames
    {
        private readonly OfflineParticipants _participants = new OfflineParticipants();

        public KillFeedLine Describe(KillFeedEntry entry)
        {
            var killerObject = CombatEvents.OfflineObject(entry.KillerId);
            var killer = NameOf(entry.KillerId);
            var victim = entry.IsSuicide ? killer : NameOf(entry.VictimId);
            return new KillFeedLine(entry, killer, victim, WeaponNameOf(killerObject, entry.WeaponId));
        }

        private KillFeedName NameOf(int id)
        {
            return _participants.TryGet(id, out var participant)
                ? new KillFeedName(participant)
                : new KillFeedName(KillFeedText.Unknown(id), null, false);
        }

        private static LocalizedText WeaponNameOf(GameObject killer, string weaponId)
        {
            if (killer == null || string.IsNullOrEmpty(weaponId))
            {
                return default;
            }

            // グレネードはロードアウトの武器ではないので、持っている武器からは名前を引けない。
            if (string.Equals(weaponId, GrenadeThrower.WeaponId, StringComparison.Ordinal))
            {
                return GrenadeThrower.Name;
            }

            // 地雷もロードアウトの武器ではなく、爆発は置いた物の種類 (能力の Id) で帰属する。
            if (MineDeployDefinition.TryFindName(killer, weaponId, out var mineName))
            {
                return mineName;
            }

            // C4 も地雷と同じく、爆発は置いた物の種類 (能力の Id) で帰属する。
            if (RemoteChargeDeployDefinition.TryFindName(killer, weaponId, out var chargeName))
            {
                return chargeName;
            }

            // 近接で倒した武器 (ナイフ) はロードアウトに入れないので、近接の武器の Id と照らして名前を引く。
            var melee = killer.GetComponentInChildren<MeleeController>(true);
            var meleeWeapon = melee != null ? melee.MeleeWeapon : null;
            if (meleeWeapon != null && string.Equals(meleeWeapon.Id, weaponId, StringComparison.Ordinal))
            {
                return meleeWeapon.ToSpec().Name;
            }

            var controller = killer.GetComponentInChildren<WeaponController>(true);
            var loadout = controller != null ? controller.Loadout : null;
            if (loadout != null)
            {
                foreach (var weapon in loadout.Items)
                {
                    if (weapon != null && IsWeapon(weapon.Spec, weaponId))
                    {
                        return weapon.Spec.Name;
                    }
                }
            }

            var enemy = killer.GetComponentInChildren<EnemyAgent>(true);
            var enemyWeapon = enemy != null ? enemy.Weapon : null;
            if (enemyWeapon != null && IsWeapon(enemyWeapon.Spec, weaponId))
            {
                return enemyWeapon.Spec.Name;
            }

            return default;
        }

        private static bool IsWeapon(WeaponSpec spec, string weaponId)
        {
            return spec != null && string.Equals(spec.Id, weaponId, StringComparison.Ordinal);
        }
    }
}
