using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class MeleeLog
    {
        public const string LogTag = "[Melee]";

        public static string Swing(string attackerId)
        {
            return $"{LogTag} swing id={attackerId}";
        }

        // 当たりと外れの行は普通の振りと同じにし、盾で殴ったかは振り始めの行で見分ける。
        public static string ShieldBash(string attackerId, string weaponId)
        {
            return $"{LogTag} shield-bash id={attackerId} weapon={weaponId}";
        }

        // 溜めた割合を書くのは、ふつうの振りと溜めた振りをログだけで見分けられるようにするため。
        public static string WeaponSwing(string attackerId, string weaponId, float chargeFraction)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} weapon-swing id={1} weapon={2} charge={3:0.00}", LogTag, attackerId, weaponId, chargeFraction);
        }

        public static string WorldHit(string attackerId, string materialId)
        {
            return $"{LogTag} world-hit id={attackerId} material={materialId}";
        }

        public static string Thrown(string attackerId, string weaponId)
        {
            return $"{LogTag} thrown id={attackerId} weapon={weaponId}";
        }

        public static string ThrowHit(string attackerId, string weaponId, string targetId, int damage)
        {
            return $"{LogTag} throw-hit id={attackerId} weapon={weaponId} target={targetId} dmg={damage} kind={DamageKind.Melee}";
        }

        public static string ThrowWorldHit(string attackerId, string weaponId, string materialId)
        {
            return $"{LogTag} throw-world-hit id={attackerId} weapon={weaponId} material={materialId}";
        }

        public static string ThrowLanded(string attackerId, string weaponId)
        {
            return $"{LogTag} throw-landed id={attackerId} weapon={weaponId}";
        }

        public static string Hit(string attackerId, string targetId, int damage, float distance)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} hit id={1} target={2} dmg={3} distance={4:0.00} kind={5}",
                LogTag,
                attackerId,
                targetId,
                damage,
                distance,
                DamageKind.Melee);
        }

        public static string Miss(string attackerId)
        {
            return $"{LogTag} miss id={attackerId}";
        }

        public static string Refused(string attackerId, MeleeRefusal reason)
        {
            if (reason == MeleeRefusal.None || !Enum.IsDefined(typeof(MeleeRefusal), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined refusal other than None");
            }

            return $"{LogTag} refused id={attackerId} reason={reason}";
        }

        // 「hit id=X」で始まる行を待つ smoke が、ほかの相手への当たりと取り違えないよう、id と target までを頭として返す。
        public static string HitPrefix(string attackerId, string targetId)
        {
            return $"{LogTag} hit id={attackerId} target={targetId} ";
        }
    }
}
