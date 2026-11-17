using System;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary>チャット (<see cref="ChatGate"/>) の <c>[Chat]</c> 付きログの文言。本文はプレイヤーが書いたものなので出さず、長さだけを出す。</summary>
    public static class ChatLog
    {
        public const string Tag = "[Chat]";

        /// <summary>間隔と文字数の上限を決めた。</summary>
        /// <example><c>[Chat] gate interval=1.00 maxLength=120</c></example>
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

        /// <summary>文を受けた。</summary>
        /// <example><c>[Chat] accepted sender=1 channel=Team length=5 at=12.50</c></example>
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

        /// <summary>文を断った。</summary>
        /// <example><c>[Chat] refused sender=1 reason=TooSoon</c></example>
        /// <exception cref="ArgumentOutOfRangeException">reason が定義に無い値か None。</exception>
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
