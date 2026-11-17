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
        private double _clock;
        private bool _started;
        private bool _disposed;

        public NetV2ChatPart(Func<IMessage, bool> send, int capacity = ClientChat.DefaultCapacity, Action<string> log = null)
        {
            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }

            _log = log ?? (line => Debug.Log(line));
            _chat = new ClientChat(capacity, send, _log);
            _sender = new Sender(this);
        }

        public ClientChat Chat => _chat;

        public IChatSender ChatSender => _sender;

        public bool Handle(IMessage message)
        {
            if (_disposed || !_chat.Handle(message, _clock))
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
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null || _started)
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
            GameSignals.ChatSender.ClearIfCurrent(_sender);
            GameSignals.Chat.ClearIfCurrent(_chat.Lines);
            _chat.Clear();
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

                _part._chat.Send(channel, text, out _);
            }
        }
    }
}
