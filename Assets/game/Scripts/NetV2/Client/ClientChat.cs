using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;
using CoreChatChannel = Shooter.Core.ChatChannel;
using WireChatChannel = Shooter.Protocol.ChatChannel;

namespace Shooter.NetV2
{
    // 届ける相手を絞るのと文の長さ・間隔を確かめるのはサーバーの ChatGate なので、ここでは送れない文 (空・プロトコルに収まらない) だけを止める。
    public sealed class ClientChat
    {
        public const int DefaultCapacity = 50;

        private readonly Func<IMessage, bool> _send;
        private readonly Action<string> _log;
        private readonly List<ChatLine> _lines = new List<ChatLine>();
        private readonly ChatRequestMessage _request = new ChatRequestMessage();
        private readonly ClientBlocks _blocks;

        public ClientChat(int capacity, Func<IMessage, bool> send, Action<string> log, ClientBlocks blocks = null)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1");
            }

            Capacity = capacity;
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _blocks = blocks;
            Lines = new ReadOnlyCollection<ChatLine>(_lines);
        }

        public int Capacity { get; }

        public IReadOnlyList<ChatLine> Lines { get; }

        public bool Handle(IMessage message, double nowSeconds)
        {
            if (!(message is ChatMessage chat))
            {
                return false;
            }

            if (!(nowSeconds >= 0d) || double.IsInfinity(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), nowSeconds, "nowSeconds must be finite and not negative");
            }

            var channel = (CoreChatChannel)(int)chat.Channel;
            if (!Enum.IsDefined(typeof(CoreChatChannel), channel))
            {
                _log(ChatLog.IgnoredChannel(chat.SenderId, (int)chat.Channel));
                return true;
            }

            if (_blocks != null && _blocks.IsBlocked(chat.SenderId))
            {
                _log(ChatLog.DroppedBlocked(chat.SenderId));
                return true;
            }

            var line = new ChatLine(chat.SenderId, channel, chat.Text ?? string.Empty, nowSeconds);
            if (_lines.Count >= Capacity)
            {
                _lines.RemoveRange(0, _lines.Count - Capacity + 1);
            }

            _lines.Add(line);
            _log(ChatLog.Received(line));
            return true;
        }

        public bool Send(CoreChatChannel channel, string text, out ChatRefusal refusal)
        {
            if (!Enum.IsDefined(typeof(CoreChatChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "channel must be a defined ChatChannel");
            }

            var trimmed = Sanitize(text);
            refusal = trimmed.Length == 0 ? ChatRefusal.Empty
                : Encoding.UTF8.GetByteCount(trimmed) > MatchProtocol.MaxChatBytes ? ChatRefusal.TooLong
                : ChatRefusal.None;
            if (refusal != ChatRefusal.None)
            {
                _log(ChatLog.NotSent(channel, refusal));
                return false;
            }

            _request.Channel = (WireChatChannel)(int)channel;
            _request.Text = trimmed;
            var sent = _send(_request);
            _log(ChatLog.Sent(channel, trimmed.Length, sent));
            return sent;
        }

        public void Clear()
        {
            _lines.Clear();
        }

        // ブロックした人の文が画面に残っていると止めた意味が無いので、届いた後にブロックした分も消す。
        public int RemoveBlocked()
        {
            if (_blocks == null || _lines.Count == 0)
            {
                return 0;
            }

            var removed = _lines.RemoveAll(line => _blocks.IsBlocked(line.SenderId));
            if (removed > 0)
            {
                _log(ChatLog.RemovedBlocked(removed));
            }

            return removed;
        }

        // 入力欄から片割れのサロゲートが来ると MessageWriter が書くときに投げるので、送る前に U+FFFD へ置き換えておく。
        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var trimmed = text.Trim();
            return trimmed.Length == 0 ? trimmed : Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(trimmed));
        }
    }
}
