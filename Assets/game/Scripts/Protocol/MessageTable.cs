using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Protocol
{
    // 届くたびに作らずに上書きするので、返したインスタンスの中身は次に Read するまでしか保証しない。
    // サーバーとクライアントで受け取る種類を分ける (MatchMessages) のは、相手が送るはずの無い種類を読まずに断るため。
    public sealed class MessageTable
    {
        private readonly Dictionary<ushort, IMessage> _byId = new Dictionary<ushort, IMessage>();
        private readonly MessageReader _reader = new MessageReader(Array.Empty<byte>());

        public MessageTable(IEnumerable<IMessage> messages)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            foreach (var message in messages)
            {
                if (message == null)
                {
                    throw new ArgumentException("messages must not contain null", nameof(messages));
                }

                if (_byId.ContainsKey(message.Id))
                {
                    throw new ArgumentException(
                        "two messages share id " + message.Id.ToString(CultureInfo.InvariantCulture), nameof(messages));
                }

                _byId.Add(message.Id, message);
            }
        }

        public int Count => _byId.Count;

        public bool TryGet(ushort id, out IMessage message)
        {
            return _byId.TryGetValue(id, out message);
        }

        public IMessage Read(ArraySegment<byte> data)
        {
            var reader = _reader;
            reader.Reset(data);
            var id = reader.ReadMessageId();
            if (!_byId.TryGetValue(id, out var message))
            {
                throw new MessageReadException("message id " + id.ToString(CultureInfo.InvariantCulture) + " is not accepted here");
            }

            reader.ReadMessageBody(message);
            reader.ExpectEnd();
            return message;
        }
    }
}
