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

        // 語そのものはゲームの載せた不適切な語なので、ログには数だけを出す。
        public static string WordFilter(ChatWordFilter filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} word filter words={1} action={2} mask={3} wholeWordsOnly={4}",
                Tag,
                filter.Count,
                filter.Spec.Action,
                filter.Spec.Mask,
                filter.Spec.WholeWordsOnly ? "true" : "false");
        }

        public static string Masked(ChatLine line)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} masked sender={1} channel={2} at={3:0.00}",
                Tag,
                line.SenderId,
                line.Channel,
                line.SentAtSeconds);
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

        public static string Received(ChatLine line)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} received sender={1} channel={2} length={3} at={4:0.00}",
                Tag,
                line.SenderId,
                line.Channel,
                line.Text.Length,
                line.SentAtSeconds);
        }

        public static string IgnoredChannel(int senderId, int channel)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} ignored a line with an unknown channel sender={1} channel={2}",
                Tag,
                senderId,
                channel);
        }

        public static string DroppedBlocked(int senderId)
        {
            return Tag + " dropped a line from a blocked sender=" + senderId.ToString(CultureInfo.InvariantCulture);
        }

        public static string RemovedBlocked(int count)
        {
            return Tag + " removed " + count.ToString(CultureInfo.InvariantCulture) + " shown line(s) from blocked senders";
        }

        public static string Sent(ChatChannel channel, int length, bool sent)
        {
            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "channel must be a defined ChatChannel");
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                sent ? "{0} sent channel={1} length={2}" : "{0} could not send channel={1} length={2} (not connected)",
                Tag,
                channel,
                length);
        }

        public static string TextChatOff(ChatChannel channel, int length)
        {
            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "channel must be a defined ChatChannel");
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} could not send channel={1} length={2} (text chat is off)",
                Tag,
                channel,
                length);
        }

        public static string NotSent(ChatChannel channel, ChatRefusal reason)
        {
            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "channel must be a defined ChatChannel");
            }

            if (reason == ChatRefusal.None || !Enum.IsDefined(typeof(ChatRefusal), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined ChatRefusal other than None");
            }

            return Tag + " not sent channel=" + channel + " reason=" + reason;
        }
    }
}
