using System;
using System.Globalization;

namespace Shooter.Core
{
    // 本文はプレイヤーが書いたものなので、ログには出さず長さだけを出す。
    public static class ChatLog
    {
        public const string Tag = "[Chat]";

        public static string Gate(ChatGate gate)
        {
            if (gate == null)
            {
                throw new ArgumentNullException(nameof(gate));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} gate interval={1:0.00} maxLength={2}",
                Tag,
                gate.IntervalSeconds,
                gate.MaxLength);
        }

        public static string Accepted(ChatLine line)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} accepted sender={1} channel={2} length={3} at={4:0.00}",
                Tag,
                line.SenderId,
                line.Channel,
                line.Text.Length,
                line.SentAtSeconds);
        }

        public static string Refused(int senderId, ChatRefusal reason)
        {
            if (reason == ChatRefusal.None || !Enum.IsDefined(typeof(ChatRefusal), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined ChatRefusal other than None");
            }

            return Tag + " refused sender=" + senderId.ToString(CultureInfo.InvariantCulture) + " reason=" + reason;
        }
    }
}
