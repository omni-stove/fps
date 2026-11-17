using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ChatGate
    {
        private static readonly char[] _lineBreaks = { '\r', '\n' };

        private readonly Dictionary<int, double> _lastAcceptedAt = new Dictionary<int, double>();

        public ChatGate(NetMatchSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            IntervalSeconds = settings.ChatIntervalSeconds;
            MaxLength = settings.MaxChatLength;
        }

        public float IntervalSeconds { get; }

        public int MaxLength { get; }

        public int Count => _lastAcceptedAt.Count;

        // 断った文の時刻を覚えないのは、断られた後に直して送り直したときに間隔を延ばさないため。
        public ChatRefusal TryAccept(int senderId, ChatChannel channel, string text, double nowSeconds, out ChatLine line)
        {
            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "channel must be a defined ChatChannel");
            }

            if (!(nowSeconds >= 0d) || double.IsInfinity(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), nowSeconds, "nowSeconds must be finite and not negative");
            }

            line = default;
            var trimmed = Normalize(text);
            if (trimmed.Length == 0)
            {
                return ChatRefusal.Empty;
            }

            if (trimmed.Length > MaxLength)
            {
                return ChatRefusal.TooLong;
            }

            if (_lastAcceptedAt.TryGetValue(senderId, out var last) && nowSeconds - last < IntervalSeconds)
            {
                return ChatRefusal.TooSoon;
            }

            _lastAcceptedAt[senderId] = nowSeconds;
            line = new ChatLine(senderId, channel, trimmed, nowSeconds);
            return ChatRefusal.None;
        }

        public bool Remove(int senderId)
        {
            return _lastAcceptedAt.Remove(senderId);
        }

        public void Clear()
        {
            _lastAcceptedAt.Clear();
        }

        // 1 行として出すので、文の中の改行は空白 1 つに寄せる (\r\n を 2 つの空白にしない)。
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var trimmed = text.Trim();
            if (trimmed.IndexOfAny(_lineBreaks) < 0)
            {
                return trimmed;
            }

            return trimmed.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
