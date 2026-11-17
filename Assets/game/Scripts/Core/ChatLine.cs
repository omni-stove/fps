using System;
using System.Globalization;

namespace Shooter.Core
{
    // 送った人を名前でなく参加者の Id で持つのは、名前に引くのを UI (IParticipants) に任せるため。
    public readonly struct ChatLine : IEquatable<ChatLine>
    {
        // default ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _text;

        public ChatLine(int senderId, ChatChannel channel, string text, double sentAtSeconds)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "channel must be a defined ChatChannel");
            }

            if (!(sentAtSeconds >= 0d) || double.IsInfinity(sentAtSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(sentAtSeconds), sentAtSeconds, "sentAtSeconds must be finite and not negative");
            }

            SenderId = senderId;
            Channel = channel;
            _text = text;
            SentAtSeconds = sentAtSeconds;
        }

        public int SenderId { get; }

        public ChatChannel Channel { get; }

        public string Text => _text ?? string.Empty;

        public double SentAtSeconds { get; }

        public bool Equals(ChatLine other)
        {
            return SenderId == other.SenderId
                && Channel == other.Channel
                && string.Equals(Text, other.Text, StringComparison.Ordinal)
                && SentAtSeconds.Equals(other.SentAtSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is ChatLine other && Equals(other);
        }

        public static bool operator ==(ChatLine left, ChatLine right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChatLine left, ChatLine right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SenderId, (int)Channel, StringComparer.Ordinal.GetHashCode(Text), SentAtSeconds);
        }

        // 本文はプレイヤーが書いたものなので、ログに残さず長さだけを出す。
        public override string ToString()
        {
            return "sender=" + SenderId.ToString(CultureInfo.InvariantCulture)
                + " channel=" + Channel
                + " length=" + Text.Length.ToString(CultureInfo.InvariantCulture)
                + " at=" + SentAtSeconds.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
