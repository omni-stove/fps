using System;
using System.Globalization;

namespace Fps.Core
{
    public static class MeleeLog
    {
        public const string LogTag = "[Melee]";

        public static string Swing(string attackerId)
        {
            return $"{LogTag} swing id={attackerId}";
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

        /// <exception cref="ArgumentOutOfRangeException">reason が None か、定義されていない値。</exception>
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
