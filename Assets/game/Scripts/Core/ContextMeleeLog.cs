using System;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    public static class ContextMeleeLog
    {
        public const string LogTag = "[ContextMelee]";

        public static string Started(string attackerId, string targetId, string moveId)
        {
            return $"{LogTag} started id={attackerId} target={targetId} move={moveId}";
        }

        // 技が選ばれずふつうの近接になったとき。何が足りなかったかを追えるよう、見た状態を並べる。
        public static string NoMove(string attackerId, string targetId, ContextMeleeConditions conditions)
        {
            return $"{LogTag} none id={attackerId} target={targetId} conditions={FormatConditions(conditions)}";
        }

        public static string Resolved(string attackerId, string targetId, string moveId, ContextMeleeOutcome outcome, int damage, bool killed)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} resolved id={1} target={2} move={3} outcome={4} dmg={5} killed={6}",
                LogTag,
                attackerId,
                targetId,
                moveId,
                outcome,
                damage,
                killed ? "true" : "false");
        }

        // 実際に増えた量を出す。満タンや上限で増えなかった分は数えない。
        public static string Reward(string attackerId, string moveId, int health, int ammo)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} reward id={1} move={2} health={3} ammo={4}",
                LogTag,
                attackerId,
                moveId,
                health,
                ammo);
        }

        public static string Interrupted(string attackerId, string targetId, string moveId, ContextMeleeInterruption reason)
        {
            if (!Enum.IsDefined(typeof(ContextMeleeInterruption), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason is not a defined ContextMeleeInterruption");
            }

            return $"{LogTag} interrupted id={attackerId} target={targetId} move={moveId} reason={reason}";
        }

        // 「resolved id=X target=Y move=Z」で始まる行を待つ smoke が、ほかの相手や技の行と取り違えないよう、ここまでを頭として返す。
        public static string ResolvedPrefix(string attackerId, string targetId, string moveId)
        {
            return $"{LogTag} resolved id={attackerId} target={targetId} move={moveId} ";
        }

        public static string RewardPrefix(string attackerId, string moveId)
        {
            return $"{LogTag} reward id={attackerId} move={moveId} ";
        }

        // Flags の ToString は「A, B」と空白を挟むので、key=value のログで値が途切れないよう「A|B」にする。
        public static string FormatConditions(ContextMeleeConditions conditions)
        {
            if (conditions == ContextMeleeConditions.None)
            {
                return nameof(ContextMeleeConditions.None);
            }

            var builder = new StringBuilder();
            foreach (ContextMeleeConditions flag in Enum.GetValues(typeof(ContextMeleeConditions)))
            {
                if (flag == ContextMeleeConditions.None || (conditions & flag) != flag)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('|');
                }

                builder.Append(flag.ToString());
                conditions &= ~flag;
            }

            // 名前の無いビットが残っていれば数で出す。黙って捨てると、何を見て選ばなかったかが追えなくなる。
            if (conditions != ContextMeleeConditions.None)
            {
                if (builder.Length > 0)
                {
                    builder.Append('|');
                }

                builder.Append(((int)conditions).ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
