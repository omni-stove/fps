using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fps.Protocol
{
    /// <summary>
    /// <see cref="MessageWriter"/> が書いたバイト列を先頭から読む。データは送り手を信じずに読み、
    /// 足りない・壊れているときは <see cref="MessageReadException"/> を投げる (読んだ位置は進めない)。
    /// </summary>
    public sealed class MessageReader
    {
        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);

        private readonly ArraySegment<byte> _data;

        public MessageReader(ArraySegment<byte> data)
        {
            if (data.Array == null)
            {
                throw new ArgumentException("data must point at an array", nameof(data));
            }

            _data = data;
        }

        public MessageReader(byte[] data)
            : this(new ArraySegment<byte>(data ?? throw new ArgumentNullException(nameof(data))))
        {
        }

        /// <summary>読み終えたバイト数。</summary>
        public int Position { get; private set; }

        /// <summary>まだ読んでいないバイト数。</summary>
        public int Remaining => _data.Count - Position;

        /// <summary>全部読み終えたか。</summary>
        public bool IsAtEnd => Remaining == 0;

        /// <summary>メッセージの先頭の Id を読む。</summary>
        public ushort ReadMessageId()
        {
            return ReadUInt16();
        }

        /// <summary>読み残しがあれば投げる。メッセージの後ろに余計なバイトが付いていないかを確かめる。</summary>
        public void ExpectEnd()
        {
            if (!IsAtEnd)
            {
                throw new MessageReadException(Text(Remaining) + " bytes are left unread at byte " + Text(Position));
            }
        }

        public byte ReadByte()
        {
            var at = Take(1, "a byte");
            return _data.Array[at];
        }

        /// <summary>1 バイトの 0 か 1 を読む。それ以外の値は壊れているとみなす。</summary>
        public bool ReadBool()
        {
            Require(1, "a bool");
            var value = _data.Array[_data.Offset + Position];
            if (value > 1)
            {
                throw new MessageReadException("a bool must be 0 or 1 (got " + Text(value) + ") at byte " + Text(Position));
            }

            Position++;
            return value == 1;
        }

        public ushort ReadUInt16()
        {
            var at = Take(2, "a 16-bit integer");
            var bytes = _data.Array;
            return (ushort)(bytes[at] | (bytes[at + 1] << 8));
        }

        public int ReadInt32()
        {
            return unchecked((int)ReadUInt32());
        }

        public uint ReadUInt32()
        {
            var at = Take(4, "a 32-bit integer");
            return ReadUInt32At(at);
        }

        public long ReadInt64()
        {
            return unchecked((long)ReadUInt64());
        }

        public ulong ReadUInt64()
        {
            var at = Take(8, "a 64-bit integer");
            return ReadUInt32At(at) | ((ulong)ReadUInt32At(at + 4) << 32);
        }

        /// <summary>IEEE 754 の 32 ビットを読む。NaN や無限大もそのまま返す (妥当性はメッセージが見る)。</summary>
        public float ReadSingle()
        {
            var at = Take(4, "a float");
            return BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32At(at)));
        }

        /// <summary>x・y・z の順の 3 つの <see cref="ReadSingle"/> を読む。</summary>
        public Vector3 ReadVector3()
        {
            var at = Take(12, "a vector");
            return new Vector3(
                BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32At(at))),
                BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32At(at + 4))),
                BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32At(at + 8))));
        }

        /// <summary>UTF-8 のバイト数 (ushort) と中身を読む。UTF-8 として正しくなければ壊れているとみなす。</summary>
        public string ReadString()
        {
            var start = Position;
            Require(2, "a string length");
            var bytes = _data.Array;
            var at = _data.Offset + Position;
            var count = bytes[at] | (bytes[at + 1] << 8);
            if (count > Remaining - 2)
            {
                throw new MessageReadException(
                    "a string of " + Text(count) + " bytes at byte " + Text(start) + " runs past the end ("
                    + Text(Remaining - 2) + " bytes left)");
            }

            string text;
            try
            {
                text = _utf8.GetString(bytes, at + 2, count);
            }
            catch (DecoderFallbackException e)
            {
                throw new MessageReadException("the string at byte " + Text(start) + " is not valid UTF-8", e);
            }

            Position += 2 + count;
            return text;
        }

        /// <summary>count バイトをそのまま読み、受け取ったデータの配列を指す区間を返す (写さない)。</summary>
        public ArraySegment<byte> ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            var at = Take(count, Text(count) + " bytes");
            return new ArraySegment<byte>(_data.Array, at, count);
        }

        /// <returns>読み始める配列の中の位置。</returns>
        private int Take(int count, string what)
        {
            Require(count, what);
            var at = _data.Offset + Position;
            Position += count;
            return at;
        }

        private void Require(int count, string what)
        {
            if (count > Remaining)
            {
                throw new MessageReadException(
                    "expected " + what + " at byte " + Text(Position) + " but only " + Text(Remaining) + " bytes are left");
            }
        }

        private uint ReadUInt32At(int at)
        {
            var bytes = _data.Array;
            return bytes[at] | ((uint)bytes[at + 1] << 8) | ((uint)bytes[at + 2] << 16) | ((uint)bytes[at + 3] << 24);
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
