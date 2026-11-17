using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// チャットの文を受けてよいかを、参加者ごとに決める。
    /// 前後の空白と改行を落とした文が空か <see cref="MaxLength"/> を超えるか、同じ人が前に受けた文から
    /// <see cref="IntervalSeconds"/> が経っていなければ断る。
    /// </summary>
    public sealed class ChatGate
    {
        private static readonly char[] _lineBreaks = { '\r', '\n' };

        private readonly Dictionary<int, double> _lastAcceptedAt = new Dictionary<int, double>();

        /// <exception cref="ArgumentNullException">settings が null。</exception>
        public ChatGate(NetMatchSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            IntervalSeconds = settings.ChatIntervalSeconds;
            MaxLength = settings.MaxChatLength;
        }

        /// <summary>1 人が続けて送れる最短の間隔 (秒)。</summary>
        public float IntervalSeconds { get; }

        /// <summary>1 行の文字数の上限。</summary>
        public int MaxLength { get; }

        /// <summary>前に受けた時刻を覚えている参加者の数。</summary>
        public int Count => _lastAcceptedAt.Count;

        /// <summary>
        /// 文を受けてよいかを調べ、受けるならその時刻を参加者の前の送信として覚える。
        /// 断った文は覚えないので、断られた後に直して送り直しても間隔は延びない。
        /// </summary>
        /// <param name="senderId">送った参加者の Id。</param>
        /// <param name="channel">送った先のチャンネル。</param>
        /// <param name="text">送られてきた文。null は空として断る。</param>
        /// <param name="nowSeconds">今の時刻 (秒)。</param>
        /// <param name="line">受けたときの、前後の空白と改行を落とした 1 行。断ったら default。</param>
        /// <exception cref="ArgumentOutOfRangeException">channel が定義に無い値、nowSeconds が負か有限でない。</exception>
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

        /// <summary>抜けた参加者の前の送信を忘れる。覚えていたら true。</summary>
        public bool Remove(int senderId)
        {
            return _lastAcceptedAt.Remove(senderId);
        }

        /// <summary>試合を始め直して時刻が 0 に戻るときなどに、すべての参加者の前の送信を忘れる。</summary>
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
