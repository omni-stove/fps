using System;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class PacketSplitter
    {
        public const byte WholeTag = 1;
        public const byte FragmentTag = 2;
        public const int WholeHeaderSize = 1;
        public const int FragmentHeaderSize = 7;

        private readonly byte[] _buffer;
        private ushort _nextMessageId;

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

        public int MaxMessageSize => ushort.MaxValue * (MaxPacketSize - FragmentHeaderSize);

        // 渡すパケットは内部のバッファを使い回すので、send の中でコピーするか送り切る。
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
