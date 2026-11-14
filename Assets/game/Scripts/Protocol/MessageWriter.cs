using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fps.Protocol
{
    /// <summary>
    /// メッセージをバイト列に書く。数はリトルエンディアン、文字列は UTF-8 のバイト数 (ushort) を前に付けて書く。
    /// 毎回作らずに使い回せるよう、書いた中身は <see cref="Reset"/> で捨てる。
    /// </summary>
    public sealed class MessageWriter
    {
        /// <summary>既定の最長のバイト数。1 つのメッセージの長さを ushort で表せる範囲に収める。</summary>
        public const int DefaultMaxLength = ushort.MaxValue;

        /// <summary>文字列 1 つの最長のバイト数 (UTF-8)。長さの前置きが ushort のため。</summary>
        public const int MaxStringBytes = ushort.MaxValue;

        private const int _initialCapacity = 256;

        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);

        private byte[] _buffer;

        /// <param name="maxLength">書ける最長のバイト数。超えると <see cref="InvalidOperationException"/> を投げる。</param>
        public MessageWriter(int maxLength = DefaultMaxLength)
        {
            if (maxLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "max length must be positive");
            }

            MaxLength = maxLength;
            _buffer = new byte[Math.Min(_initialCapacity, maxLength)];
        }

        /// <summary>書ける最長のバイト数。</summary>
        public int MaxLength { get; }

        /// <summary>今までに書いたバイト数。</summary>
        public int Length { get; private set; }

        /// <summary>書いた中身を捨てる。確保した配列は使い回す。</summary>
        public void Reset()
        {
            Length = 0;
        }

        /// <summary>書いた中身を指す。次に書くか <see cref="Reset"/> するまでしか中身を保証しない。</summary>
        public ArraySegment<byte> AsSegment()
        {
            return new ArraySegment<byte>(_buffer, 0, Length);
        }

        /// <summary>書いた中身を写した新しい配列。</summary>
        public byte[] ToArray()
        {
            var copy = new byte[Length];
            Buffer.BlockCopy(_buffer, 0, copy, 0, Length);
            return copy;
        }

        /// <summary>メッセージの Id・版・中身の順に書く。</summary>
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

        /// <summary>1 バイトで書く (true は 1、false は 0)。</summary>
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

        /// <summary>IEEE 754 の 32 ビットのビット列のまま書く (NaN も書ける。値の妥当性は読む側のメッセージが見る)。</summary>
        public void WriteSingle(float value)
        {
            WriteInt32(BitConverter.SingleToInt32Bits(value));
        }

        /// <summary>IEEE 754 の 64 ビットのビット列のまま書く。</summary>
        public void WriteDouble(double value)
        {
            WriteInt64(BitConverter.DoubleToInt64Bits(value));
        }

        /// <summary>x・y の順に <see cref="WriteSingle"/> で書く。</summary>
        public void WriteVector2(Vector2 value)
        {
            // 8 バイトを 1 回で取るのは、収まらないときに x だけが残らないようにするため。
            var at = Reserve(8);
            PutUInt32(at, unchecked((uint)BitConverter.SingleToInt32Bits(value.X)));
            PutUInt32(at + 4, unchecked((uint)BitConverter.SingleToInt32Bits(value.Y)));
        }

        /// <summary>x・y・z の順に <see cref="WriteSingle"/> で書く。</summary>
        public void WriteVector3(Vector3 value)
        {
            // 12 バイトを 1 回で取るのは、収まらないときに x や y だけが残らないようにするため。
            var at = Reserve(12);
            PutUInt32(at, unchecked((uint)BitConverter.SingleToInt32Bits(value.X)));
            PutUInt32(at + 4, unchecked((uint)BitConverter.SingleToInt32Bits(value.Y)));
            PutUInt32(at + 8, unchecked((uint)BitConverter.SingleToInt32Bits(value.Z)));
        }

        /// <summary>x・y・z・w の順に <see cref="WriteSingle"/> で書く。</summary>
        public void WriteQuaternion(Quaternion value)
        {
            // 16 バイトを 1 回で取るのは、収まらないときに一部の成分だけが残らないようにするため。
            var at = Reserve(16);
            PutUInt32(at, unchecked((uint)BitConverter.SingleToInt32Bits(value.X)));
            PutUInt32(at + 4, unchecked((uint)BitConverter.SingleToInt32Bits(value.Y)));
            PutUInt32(at + 8, unchecked((uint)BitConverter.SingleToInt32Bits(value.Z)));
            PutUInt32(at + 12, unchecked((uint)BitConverter.SingleToInt32Bits(value.W)));
        }

        /// <summary>UTF-8 のバイト数 (ushort) を書いてから中身を書く。null は空文字列として書く。</summary>
        /// <exception cref="ArgumentException">UTF-8 で <see cref="MaxStringBytes"/> バイトを超える、または対になっていないサロゲートを含む。</exception>
        public void WriteString(string value)
        {
            WriteString(value, MaxStringBytes);
        }

        /// <summary><see cref="WriteString(string)"/> と同じく書くが、UTF-8 で maxBytes バイトを超えるなら書かずに投げる。</summary>
        /// <exception cref="ArgumentException">UTF-8 で maxBytes バイトを超える、または対になっていないサロゲートを含む。</exception>
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

        /// <summary>バイト列をそのまま (長さを付けずに) 書く。</summary>
        public void WriteBytes(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null)
            {
                throw new ArgumentException("bytes must point at an array", nameof(bytes));
            }

            var at = Reserve(bytes.Count);
            Buffer.BlockCopy(bytes.Array, bytes.Offset, _buffer, at, bytes.Count);
        }

        /// <returns>書き始める位置。</returns>
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
