using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="PacketSplitter"/> が分けたパケットを、送り手 1 人分について元のメッセージに戻す。
    /// 断片は順不同で届いてもよい。揃わないまま上限数を超えたメッセージは古いものから捨てる。
    /// </summary>
    public sealed class PacketReassembler
    {
        private readonly int _maxMessageSize;
        private readonly int _maxPendingMessages;
        private readonly long _maxPendingBytes;
        private long _pendingBytes;
        private readonly Dictionary<ushort, Pending> _pending = new Dictionary<ushort, Pending>();
        private readonly List<ushort> _arrivalOrder = new List<ushort>();

        /// <param name="maxMessageSize">受け付けるメッセージの最大バイト数。超えるものは捨てる。</param>
        /// <param name="maxPendingMessages">同時に組み立て途中にしておけるメッセージの数。</param>
        /// <param name="maxPendingBytes">組み立て途中のメッセージが溜めてよい合計バイト数。0 以下なら <paramref name="maxMessageSize"/> × <paramref name="maxPendingMessages"/>。</param>
        public PacketReassembler(int maxMessageSize, int maxPendingMessages, int maxPendingBytes = 0)
        {
            if (maxMessageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMessageSize), maxMessageSize, "must be positive");
            }

            if (maxPendingMessages <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPendingMessages), maxPendingMessages, "must be positive");
            }

            _maxMessageSize = maxMessageSize;
            _maxPendingMessages = maxPendingMessages;
            _maxPendingBytes = maxPendingBytes > 0 ? maxPendingBytes : (long)maxMessageSize * maxPendingMessages;
        }

        /// <summary>組み立て途中のメッセージの数。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// パケットを 1 つ受け取る。<see cref="PacketReadResult.Complete"/> のときだけ <paramref name="message"/> が有効。
        /// 分けられていないメッセージは <paramref name="packet"/> の一部を指すので、次の受信の前にコピーすること。
        /// </summary>
        public PacketReadResult Read(ArraySegment<byte> packet, out ArraySegment<byte> message)
        {
            if (packet.Array == null)
            {
                throw new ArgumentException("segment has no array", nameof(packet));
            }

            message = default;
            if (packet.Count < PacketSplitter.WholeHeaderSize)
            {
                return PacketReadResult.Malformed;
            }

            var array = packet.Array;
            var tag = array[packet.Offset];
            if (tag == PacketSplitter.WholeTag)
            {
                var length = packet.Count - PacketSplitter.WholeHeaderSize;
                if (length > _maxMessageSize)
                {
                    return PacketReadResult.Malformed;
                }

                message = new ArraySegment<byte>(array, packet.Offset + PacketSplitter.WholeHeaderSize, length);
                return PacketReadResult.Complete;
            }

            if (tag != PacketSplitter.FragmentTag || packet.Count <= PacketSplitter.FragmentHeaderSize)
            {
                return PacketReadResult.Malformed;
            }

            var messageId = PacketSplitter.ReadUInt16(array, packet.Offset + 1);
            var index = PacketSplitter.ReadUInt16(array, packet.Offset + 3);
            var count = PacketSplitter.ReadUInt16(array, packet.Offset + 5);
            var pieceLength = packet.Count - PacketSplitter.FragmentHeaderSize;

            // 1 断片あたり 1 バイト以上あるので、総数が上限バイト数を超えるものは組み立てても上限を超える
            if (count < 2 || index >= count || count > _maxMessageSize)
            {
                Drop(messageId);
                return PacketReadResult.Malformed;
            }

            if (!_pending.TryGetValue(messageId, out var pending))
            {
                if (_pending.Count >= _maxPendingMessages)
                {
                    Drop(_arrivalOrder[0]);
                }

                pending = new Pending(count);
                _pending.Add(messageId, pending);
                _arrivalOrder.Add(messageId);
            }
            else if (pending.Count != count || pending.Pieces.ContainsKey(index))
            {
                Drop(messageId);
                return PacketReadResult.Malformed;
            }

            if (pending.TotalBytes + pieceLength > _maxMessageSize || pieceLength > _maxPendingBytes)
            {
                Drop(messageId);
                return PacketReadResult.Malformed;
            }

            // 送り手全体の合計が上限を超えるなら、古いメッセージから捨てる
            while (_pendingBytes + pieceLength > _maxPendingBytes)
            {
                var oldest = _arrivalOrder[0];
                if (oldest == messageId)
                {
                    if (_arrivalOrder.Count == 1)
                    {
                        Drop(messageId);
                        return PacketReadResult.Malformed;
                    }

                    oldest = _arrivalOrder[1];
                }

                Drop(oldest);
            }

            var piece = new byte[pieceLength];
            Buffer.BlockCopy(array, packet.Offset + PacketSplitter.FragmentHeaderSize, piece, 0, pieceLength);
            pending.Pieces[index] = piece;
            pending.TotalBytes += pieceLength;
            _pendingBytes += pieceLength;

            if (pending.Pieces.Count < count)
            {
                return PacketReadResult.Incomplete;
            }

            var assembled = new byte[pending.TotalBytes];
            var offset = 0;
            for (var i = 0; i < count; i++)
            {
                var part = pending.Pieces[i];
                Buffer.BlockCopy(part, 0, assembled, offset, part.Length);
                offset += part.Length;
            }

            Drop(messageId);
            message = new ArraySegment<byte>(assembled);
            return PacketReadResult.Complete;
        }

        /// <summary>組み立て途中のメッセージをすべて捨てる。</summary>
        public void Clear()
        {
            _pending.Clear();
            _arrivalOrder.Clear();
            _pendingBytes = 0;
        }

        private void Drop(ushort messageId)
        {
            if (_pending.TryGetValue(messageId, out var pending))
            {
                _pending.Remove(messageId);
                _arrivalOrder.Remove(messageId);
                _pendingBytes -= pending.TotalBytes;
            }
        }

        private sealed class Pending
        {
            public Pending(int count)
            {
                Count = count;
            }

            public int Count { get; }

            // 断片が届いた分だけ確保する (count だけで大きな配列を作らせない)
            public Dictionary<int, byte[]> Pieces { get; } = new Dictionary<int, byte[]>();

            public int TotalBytes { get; set; }
        }
    }
}
