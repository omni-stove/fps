using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using ChatChannel = Shooter.Core.ChatChannel;

namespace Shooter.NetV2
{
    public sealed class NetV2ChatPart : INetV2ClientPart
    {
        private readonly ClientChat _chat;
        private readonly Action<string> _log;
        private readonly Sender _sender;
        private readonly ClientBlocks _blocks;
        private double _clock;
        private bool _started;
        private bool _off;
        private bool _disposed;

        public NetV2ChatPart(
            Func<IMessage, bool> send, int capacity = ClientChat.DefaultCapacity, Action<string> log = null, ClientBlocks blocks = null)
        {
            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }

            _log = log ?? (line => Debug.Log(line));
            _blocks = blocks;
            _chat = new ClientChat(capacity, send, _log, blocks);
            _sender = new Sender(this);
            if (_blocks != null)
            {
                _blocks.Changed += HandleBlocksChanged;
            }
        }

        public ClientChat Chat => _chat;

        public IChatSender ChatSender => _sender;

        public bool Handle(IMessage message)
        {
            if (_disposed)
            {
                return false;
            }

            // 他の部品に回しても使い道がないので、Off の間に届いた行も受け取ったことにして捨てる。
            if (_off && message is ChatMessage)
            {
                return true;
            }

            if (!_chat.Handle(message, _clock))
            {
                return false;
            }

            if (_started)
            {
                GameSignals.Chat.Publish(_chat.Lines);
            }

            return true;
        }

        // 部品はつなぐ前に作られ、試合に入ったことは最初のスナップショットで分かるので、そこで送る口と行を出す。
        // ルールは試合の途中でも変わり得るので、一度出した後も毎回 TextChat を見て出し入れする。
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            _off = snapshot.Match.Rules.TextChat != TextChat.On;
            if (_off)
            {
                if (_started)
                {
                    _started = false;
                    GameSignals.ChatSender.ClearIfCurrent(_sender);
                    GameSignals.Chat.ClearIfCurrent(_chat.Lines);
                }

                return;
            }

            if (_started)
            {
                return;
            }

            _started = true;
            GameSignals.ChatSender.Publish(_sender);
            GameSignals.Chat.Publish(_chat.Lines);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _clock += deltaSeconds;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_blocks != null)
            {
                _blocks.Changed -= HandleBlocksChanged;
            }

            GameSignals.ChatSender.ClearIfCurrent(_sender);
            GameSignals.Chat.ClearIfCurrent(_chat.Lines);
            _chat.Clear();
        }

        private void HandleBlocksChanged()
        {
            if (!_disposed && _chat.RemoveBlocked() > 0 && _started)
            {
                GameSignals.Chat.Publish(_chat.Lines);
            }
        }

        // 部品そのものを渡すと UI から Handle や Dispose まで呼べてしまうので、送る口だけの包みを渡す。
        private sealed class Sender : IChatSender
        {
            private readonly NetV2ChatPart _part;

            public Sender(NetV2ChatPart part)
            {
                _part = part;
            }

            public void Send(ChatChannel channel, string text)
            {
                if (_part._disposed)
                {
                    _part._log(ChatLog.Sent(channel, text?.Length ?? 0, false));
                    return;
                }

                // 引っ込めた後も UI が口を握ったままのことがあり、サーバーが断る文を送らないようにここでも止める。
                if (_part._off)
                {
                    _part._log(ChatLog.TextChatOff(channel, text?.Length ?? 0));
                    return;
                }

                _part._chat.Send(channel, text, out _);
            }
        }
    }
}
