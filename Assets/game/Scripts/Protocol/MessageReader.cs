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

        private ArraySegment<byte> _data;

        public MessageReader(ArraySegment<byte> data)
        {
            Reset(data);
        }

        public MessageReader(byte[] data)
            : this(new ArraySegment<byte>(data ?? throw new ArgumentNullException(nameof(data))))
        {
        }

        /// <summary>data を先頭から読み直す。届くたびに作らずに使い回すため。</summary>
        public void Reset(ArraySegment<byte> data)
        {
            if (data.Array == null)
            {
                throw new ArgumentException("data must point at an array", nameof(data));
            }

            _data = data;
            Position = 0;
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

        /// <summary>
        /// Id の後ろの版を読み、<see cref="IMessage.Version"/> と同じなら message の <see cref="IMessage.Read"/> で中身を読む。
        /// 版が違えば中身を読まずに投げる (読んだ位置は版の前のまま)。
        /// </summary>
        public void ReadMessageBody(IMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Require(1, "a message version");
            var version = _data.Array[_data.Offset + Position];
            if (version != message.Version)
            {
                throw new MessageReadException(
                    "message " + Text(message.Id) + " is version " + Text(version) + " but this build reads only version "
                    + Text(message.Version));
            }

            Position++;
            message.Read(this);
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

        /// <summary>limit より小さい 1 バイトを読む。列挙の値や上限のある数に使う。</summary>
        public byte ReadByteBelow(int limit, string what)
        {
            Require(1, what);
            var value = _data.Array[_data.Offset + Position];
            if (value >= limit)
            {
                throw new MessageReadException(
                    what + " must be below " + Text(limit) + " (got " + Text(value) + ") at byte " + Text(Position));
            }

            Position++;
            return value;
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

        /// <summary>min 以上の 32 ビットの整数を読む。</summary>
        public int ReadInt32AtLeast(int min, string what)
        {
            Require(4, what);
            var value = unchecked((int)ReadUInt32At(_data.Offset + Position));
            if (value < min)
            {
                throw new MessageReadException(
                    what + " must be at least " + Text(min) + " (got " + Text(value) + ") at byte " + Text(Position));
            }

            Position += 4;
            return value;
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
            return SingleAt(at);
        }

        /// <summary>NaN でも無限大でもない <see cref="ReadSingle"/> を読む。</summary>
        public float ReadFiniteSingle(string what)
        {
            Require(4, what);
            var value = SingleAt(_data.Offset + Position);
            if (!IsFinite(value))
            {
                throw NotFinite(what);
            }

            Position += 4;
            return value;
        }

        /// <summary>IEEE 754 の 64 ビットを読む。NaN や無限大もそのまま返す。</summary>
        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(ReadInt64());
        }

        /// <summary>x・y の順の 2 つの <see cref="ReadSingle"/> を読む。</summary>
        public Vector2 ReadVector2()
        {
            var at = Take(8, "a 2D vector");
            return new Vector2(SingleAt(at), SingleAt(at + 4));
        }

        /// <summary>どの成分も有限な <see cref="ReadVector2"/> を読む。</summary>
        public Vector2 ReadFiniteVector2(string what)
        {
            Require(8, what);
            var at = _data.Offset + Position;
            var value = new Vector2(SingleAt(at), SingleAt(at + 4));
            if (!IsFinite(value.X) || !IsFinite(value.Y))
            {
                throw NotFinite(what);
            }

            Position += 8;
            return value;
        }

        /// <summary>x・y・z の順の 3 つの <see cref="ReadSingle"/> を読む。</summary>
        public Vector3 ReadVector3()
        {
            var at = Take(12, "a vector");
            return new Vector3(SingleAt(at), SingleAt(at + 4), SingleAt(at + 8));
        }

        /// <summary>どの成分も有限な <see cref="ReadVector3"/> を読む。</summary>
        public Vector3 ReadFiniteVector3(string what)
        {
            Require(12, what);
            var at = _data.Offset + Position;
            var value = new Vector3(SingleAt(at), SingleAt(at + 4), SingleAt(at + 8));
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw NotFinite(what);
            }

            Position += 12;
            return value;
        }

        /// <summary>x・y・z・w の順の 4 つの成分がどれも有限な四元数を読む。</summary>
        public Quaternion ReadFiniteQuaternion(string what)
        {
            Require(16, what);
            var at = _data.Offset + Position;
            var value = new Quaternion(SingleAt(at), SingleAt(at + 4), SingleAt(at + 8), SingleAt(at + 12));
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z) || !IsFinite(value.W))
            {
                throw NotFinite(what);
            }

            Position += 16;
            return value;
        }

        /// <summary>UTF-8 のバイト数 (ushort) と中身を読む。UTF-8 として正しくなければ壊れているとみなす。</summary>
        public string ReadString()
        {
            return ReadString(MessageWriter.MaxStringBytes, null);
        }

        /// <summary>
        /// <see cref="ReadString()"/> と同じく読むが、maxBytes バイトより長ければ壊れているとみなす。
        /// 中身が reuse と同じ ASCII の文字列なら、新しい文字列を作らずに reuse を返す (毎 tick 届く Id で GC を起こさないため)。
        /// </summary>
        public string ReadString(int maxBytes, string reuse)
        {
            if (maxBytes < 0 || maxBytes > MessageWriter.MaxStringBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "max bytes must be 0-" + Text(MessageWriter.MaxStringBytes));
            }

            var start = Position;
            Require(2, "a string length");
            var bytes = _data.Array;
            var at = _data.Offset + Position;
            var count = bytes[at] | (bytes[at + 1] << 8);
            if (count > maxBytes)
            {
                throw new MessageReadException(
                    "the string at byte " + Text(start) + " is " + Text(count) + " bytes; at most " + Text(maxBytes) + " are allowed");
            }

            if (count > Remaining - 2)
            {
                throw new MessageReadException(
                    "a string of " + Text(count) + " bytes at byte " + Text(start) + " runs past the end ("
                    + Text(Remaining - 2) + " bytes left)");
            }

            if (reuse != null && IsSameAscii(bytes, at + 2, count, reuse))
            {
                Position += 2 + count;
                return reuse;
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

        private static bool IsSameAscii(byte[] bytes, int at, int count, string text)
        {
            if (text.Length != count)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                var b = bytes[at + i];
                if (b >= 0x80 || b != text[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private MessageReadException NotFinite(string what)
        {
            return new MessageReadException(what + " at byte " + Text(Position) + " is not finite");
        }

        private float SingleAt(int at)
        {
            return BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32At(at)));
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
