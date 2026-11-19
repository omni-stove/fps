using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class OfflineKillFeedNames : IKillFeedNames
    {
        private readonly OfflineParticipants _participants = new OfflineParticipants();

        public KillFeedLine Describe(KillFeedEntry entry)
        {
            var killerObject = CombatEvents.OfflineObject(entry.KillerId);
            var killer = NameOf(entry.KillerId);
            var victim = entry.IsSuicide ? killer : NameOf(entry.VictimId);
            return new KillFeedLine(
                entry, killer, victim, WeaponNameOf(killerObject, entry.WeaponId),
                ParticipantKillFeedNames.AnyLocal(_participants, entry.AssisterIds));
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
                return new LocalizedText(GrenadeThrower.NameKey);
            }

            // 地雷もロードアウトの武器ではなく、爆発は置いた物の種類 (能力の Id) で帰属する。
            if (MineDeployDefinition.TryFind(killer, weaponId, out var mine))
            {
                return DisplayNames.Of(mine);
            }

            // C4 も地雷と同じく、爆発は置いた物の種類 (能力の Id) で帰属する。
            if (RemoteChargeDeployDefinition.TryFind(killer, weaponId, out var charge))
            {
                return DisplayNames.Of(charge);
            }

            // 近接で倒した武器 (ナイフ) はロードアウトに入れないので、近接の武器の Id と照らして名前を引く。
            var melee = killer.GetComponentInChildren<MeleeController>(true);
            var meleeWeapon = melee != null ? melee.MeleeWeapon : null;
            if (meleeWeapon != null && string.Equals(meleeWeapon.Id, weaponId, StringComparison.Ordinal))
            {
                return DisplayNames.Of(meleeWeapon);
            }

            var thrown = melee != null ? melee.LastThrownWeapon : null;
            if (thrown != null && string.Equals(thrown.Id, weaponId, StringComparison.Ordinal))
            {
                return DisplayNames.Of(thrown);
            }

            var controller = killer.GetComponentInChildren<WeaponController>(true);
            var loadout = controller != null ? controller.Loadout : null;
            if (loadout != null)
            {
                foreach (var weapon in loadout.Items)
                {
                    if (weapon != null && IsWeapon(weapon.Spec, weaponId))
                    {
                        return DisplayNames.Of(weapon.Spec);
                    }
                }
            }

            var enemy = killer.GetComponentInChildren<EnemyAgent>(true);
            var enemyWeapon = enemy != null ? enemy.Weapon : null;
            if (enemyWeapon != null && IsWeapon(enemyWeapon.Spec, weaponId))
            {
                return DisplayNames.Of(enemyWeapon.Spec);
            }

            return default;
        }

        private static bool IsWeapon(WeaponSpec spec, string weaponId)
        {
            return spec != null && string.Equals(spec.Id, weaponId, StringComparison.Ordinal);
        }
    }
}
