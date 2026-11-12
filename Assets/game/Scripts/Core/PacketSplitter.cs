using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// 1 パケットの上限 (EOS P2P では 1170 バイト) を超えるメッセージを複数のパケットに分ける。
    /// 受け手は <see cref="PacketReassembler"/> で元に戻す。
    /// </summary>
    /// <remarks>
    /// パケットの形式 (数値はリトルエンディアン):
    /// 分けないとき <c>[<see cref="WholeTag"/>][本文]</c>、
    /// 分けるとき <c>[<see cref="FragmentTag"/>][メッセージ ID 2][番号 2][総数 2][本文の断片]</c>。
    /// </remarks>
    public sealed class PacketSplitter
    {
        public const byte WholeTag = 1;
        public const byte FragmentTag = 2;
        public const int WholeHeaderSize = 1;
        public const int FragmentHeaderSize = 7;

        private readonly byte[] _buffer;
        private ushort _nextMessageId;

        /// <param name="maxPacketSize">1 パケットの最大バイト数。ヘッダ込み。</param>
        public PacketSplitter(int maxPacketSize)
        {
            if (maxPacketSize <= FragmentHeaderSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPacketSize), maxPacketSize, "must be larger than the fragment header");
            }

            _buffer = new byte[maxPacketSize];
        }

        public int MaxPacketSize => _buffer.Length;

        /// <summary>分ければ送れるメッセージの最大バイト数。</summary>
        public int MaxMessageSize => ushort.MaxValue * (MaxPacketSize - FragmentHeaderSize);

        /// <summary>
        /// <paramref name="message"/> をパケットに分けて順に <paramref name="send"/> へ渡す。
        /// 渡すパケットは内部のバッファを使い回すので、<paramref name="send"/> の中でコピーするか送り切ること。
        /// </summary>
        /// <returns>渡したパケットの数。</returns>
        public int Split(ArraySegment<byte> message, Action<ArraySegment<byte>> send)
        {
            if (message.Array == null)
            {
                throw new ArgumentException("segment has no array", nameof(message));
            }

            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }

            if (message.Count <= MaxPacketSize - WholeHeaderSize)
            {
                _buffer[0] = WholeTag;
                Buffer.BlockCopy(message.Array, message.Offset, _buffer, WholeHeaderSize, message.Count);
                send(new ArraySegment<byte>(_buffer, 0, WholeHeaderSize + message.Count));
                return 1;
            }

            if (message.Count > MaxMessageSize)
            {
                throw new ArgumentException(
                    "message of " + message.Count.ToString(CultureInfo.InvariantCulture)
                    + " bytes exceeds the limit of " + MaxMessageSize.ToString(CultureInfo.InvariantCulture),
                    nameof(message));
            }

            var chunk = MaxPacketSize - FragmentHeaderSize;
            var count = (message.Count + chunk - 1) / chunk;
            var messageId = _nextMessageId;
            _nextMessageId = unchecked((ushort)(_nextMessageId + 1));

            for (var index = 0; index < count; index++)
            {
                var offset = index * chunk;
                var length = Math.Min(chunk, message.Count - offset);
                _buffer[0] = FragmentTag;
                WriteUInt16(_buffer, 1, messageId);
                WriteUInt16(_buffer, 3, (ushort)index);
                WriteUInt16(_buffer, 5, (ushort)count);
                Buffer.BlockCopy(message.Array, message.Offset + offset, _buffer, FragmentHeaderSize, length);
                send(new ArraySegment<byte>(_buffer, 0, FragmentHeaderSize + length));
            }

            return count;
        }

        internal static ushort ReadUInt16(byte[] array, int offset)
        {
            return (ushort)(array[offset] | (array[offset + 1] << 8));
        }

        private static void WriteUInt16(byte[] array, int offset, ushort value)
        {
            array[offset] = (byte)value;
            array[offset + 1] = (byte)(value >> 8);
        }
    }
}
