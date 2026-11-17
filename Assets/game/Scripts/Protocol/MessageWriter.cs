using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Protocol
{
    // 毎回作らずに使い回せるよう、書いた中身は Reset で捨てる。
    public sealed class MessageWriter
    {
        // 1 つのメッセージの長さを ushort で表せる範囲に収める。
        public const int DefaultMaxLength = ushort.MaxValue;

        // 長さの前置きが ushort のため。
        public const int MaxStringBytes = ushort.MaxValue;

        private const int _initialCapacity = 256;

        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);

        private byte[] _buffer;

        public MessageWriter(int maxLength = DefaultMaxLength)
        {
            if (maxLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "max length must be positive");
            }

            MaxLength = maxLength;
            _buffer = new byte[Math.Min(_initialCapacity, maxLength)];
        }

        public int MaxLength { get; }

        public int Length { get; private set; }

        public void Reset()
        {
            Length = 0;
        }

        // 写さずに中の配列を指すので、次に書くか Reset するまでしか中身を保証しない。
        public ArraySegment<byte> AsSegment()
        {
            return new ArraySegment<byte>(_buffer, 0, Length);
        }

        public byte[] ToArray()
        {
            var copy = new byte[Length];
            Buffer.BlockCopy(_buffer, 0, copy, 0, Length);
            return copy;
        }

        public void WriteMessage(IMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // 本文の途中で失敗したときに、Id や書きかけの本文を残すと受け手のメッセージの区切りがずれるため、書き始めの位置へ戻す。
            var start = Length;
            try
            {
                WriteUInt16(message.Id);
                WriteByte(message.Version);
                message.Write(this);
            }
            catch
            {
                Length = start;
                throw;
            }
        }

        public void WriteByte(byte value)
        {
            var at = Reserve(1);
            _buffer[at] = value;
        }

        public void WriteBool(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteUInt16(ushort value)
        {
            var at = Reserve(2);
            _buffer[at] = (byte)value;
            _buffer[at + 1] = (byte)(value >> 8);
        }

        public void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        public void WriteUInt32(uint value)
        {
            var at = Reserve(4);
            _buffer[at] = (byte)value;
            _buffer[at + 1] = (byte)(value >> 8);
            _buffer[at + 2] = (byte)(value >> 16);
            _buffer[at + 3] = (byte)(value >> 24);
        }

        public void WriteInt64(long value)
        {
            WriteUInt64(unchecked((ulong)value));
        }

        public void WriteUInt64(ulong value)
        {
            // 8 バイトを 1 回で取るのは、収まらないときに下位 4 バイトだけが残らないようにするため。
            var at = Reserve(8);
            PutUInt32(at, (uint)value);
            PutUInt32(at + 4, (uint)(value >> 32));
        }

        private void PutUInt32(int at, uint value)
        {
            _buffer[at] = (byte)value;
            _buffer[at + 1] = (byte)(value >> 8);
            _buffer[at + 2] = (byte)(value >> 16);
            _buffer[at + 3] = (byte)(value >> 24);
        }

        // NaN もそのまま書くのは、値の妥当性を読む側のメッセージが見るため。
        public void WriteSingle(float value)
        {
            WriteInt32(BitConverter.SingleToInt32Bits(value));
        }

        public void WriteDouble(double value)
        {
            WriteInt64(BitConverter.DoubleToInt64Bits(value));
        }

        public void WriteVector2(Vector2 value)
        {
            // 8 バイトを 1 回で取るのは、収まらないときに x だけが残らないようにするため。
            var at = Reserve(8);
            PutUInt32(at, unchecked((uint)BitConverter.SingleToInt32Bits(value.X)));
            PutUInt32(at + 4, unchecked((uint)BitConverter.SingleToInt32Bits(value.Y)));
        }

        public void WriteVector3(Vector3 value)
        {
            // 12 バイトを 1 回で取るのは、収まらないときに x や y だけが残らないようにするため。
            var at = Reserve(12);
            PutUInt32(at, unchecked((uint)BitConverter.SingleToInt32Bits(value.X)));
            PutUInt32(at + 4, unchecked((uint)BitConverter.SingleToInt32Bits(value.Y)));
            PutUInt32(at + 8, unchecked((uint)BitConverter.SingleToInt32Bits(value.Z)));
        }

        public void WriteQuaternion(Quaternion value)
        {
            // 16 バイトを 1 回で取るのは、収まらないときに一部の成分だけが残らないようにするため。
            var at = Reserve(16);
            PutUInt32(at, unchecked((uint)BitConverter.SingleToInt32Bits(value.X)));
            PutUInt32(at + 4, unchecked((uint)BitConverter.SingleToInt32Bits(value.Y)));
            PutUInt32(at + 8, unchecked((uint)BitConverter.SingleToInt32Bits(value.Z)));
            PutUInt32(at + 12, unchecked((uint)BitConverter.SingleToInt32Bits(value.W)));
        }

        public void WriteString(string value)
        {
            WriteString(value, MaxStringBytes);
        }

        public void WriteString(string value, int maxBytes)
        {
            if (maxBytes < 0 || maxBytes > MaxStringBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "max bytes must be 0-" + Text(MaxStringBytes));
            }

            var text = value ?? "";
            int count;
            try
            {
                count = _utf8.GetByteCount(text);
            }
            catch (EncoderFallbackException e)
            {
                throw new ArgumentException("string is not valid UTF-16", nameof(value), e);
            }

            if (count > maxBytes)
            {
                throw new ArgumentException(
                    "string is " + Text(count) + " bytes in UTF-8; at most " + Text(maxBytes) + " can be written", nameof(value));
            }

            // 前置きと中身を 1 回で取るのは、収まらないときに前置きだけが残らないようにするため。
            var at = Reserve(2 + count);
            _buffer[at] = (byte)count;
            _buffer[at + 1] = (byte)(count >> 8);
            at += 2;
            _utf8.GetBytes(text, 0, text.Length, _buffer, at);
        }

        public void WriteBytes(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null)
            {
                throw new ArgumentException("bytes must point at an array", nameof(bytes));
            }

            var at = Reserve(bytes.Count);
            Buffer.BlockCopy(bytes.Array, bytes.Offset, _buffer, at, bytes.Count);
        }

        private int Reserve(int count)
        {
            if (count > MaxLength - Length)
            {
                throw new InvalidOperationException(
                    "message would be " + Text((long)Length + count) + " bytes; at most " + Text(MaxLength) + " can be written");
            }

            var needed = Length + count;
            if (needed > _buffer.Length)
            {
                var size = Math.Max(needed, (int)Math.Min((long)_buffer.Length * 2, MaxLength));
                Array.Resize(ref _buffer, size);
            }

            var at = Length;
            Length = needed;
            return at;
        }

        private static string Text(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
