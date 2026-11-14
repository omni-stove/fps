using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Protocol
{
    /// <summary>
    /// 受け取るメッセージの種類ごとに 1 つのインスタンスを持ち、届いたバイト列をその Id のインスタンスに読み込む。
    /// 届くたびに作らずに上書きするので、返したインスタンスの中身は次に <see cref="Read"/> するまでしか保証しない。
    /// </summary>
    /// <remarks>
    /// サーバーとクライアントで受け取る種類を分ける (<see cref="MatchMessages"/>) のは、相手が送るはずの無い種類を読まずに断るため。
    /// </remarks>
    public sealed class MessageTable
    {
        private readonly Dictionary<ushort, IMessage> _byId = new Dictionary<ushort, IMessage>();
        private readonly MessageReader _reader = new MessageReader(Array.Empty<byte>());

        /// <exception cref="ArgumentException">null を含む、または Id が重なる。</exception>
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

        /// <summary>受け取る種類の数。</summary>
        public int Count => _byId.Count;

        /// <summary>id の種類を受け取るなら、そのインスタンスを返す。</summary>
        public bool TryGet(ushort id, out IMessage message)
        {
            return _byId.TryGetValue(id, out message);
        }

        /// <summary>
        /// data を 1 つのメッセージとして読み、その Id のインスタンスに入れて返す。
        /// </summary>
        /// <exception cref="MessageReadException">
        /// 受け取らない Id・違う版・足りない・壊れている・後ろに余計なバイトがある。送り手を信じずに切る目安にする。
        /// </exception>
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
