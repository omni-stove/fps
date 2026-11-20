using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Protocol
{
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

        // 届くたびに作らずに使い回せるよう、別の data を先頭から読み直せるようにする。
        public void Reset(ArraySegment<byte> data)
        {
            if (data.Array == null)
            {
                throw new ArgumentException("data must point at an array", nameof(data));
            }

            _data = data;
            Position = 0;
        }

        public int Position { get; private set; }

        public int Remaining => _data.Count - Position;

        public bool IsAtEnd => Remaining == 0;

        public ushort ReadMessageId()
        {
            return ReadUInt16();
        }

        public void ReadMessageBody(IMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Require(1, "a message version");
            var version = _data.Array[_data.Offset + Position];
            if (version < message.Version && message is IReadsOlderVersions older && version >= older.OldestReadableVersion)
            {
                Position++;
                older.ReadOlder(this, version);
                return;
            }

            if (version != message.Version)
            {
                throw new MessageReadException(
                    "message " + Text(message.Id) + " is version " + Text(version) + " but this build reads only version "
                    + Text(message.Version));
            }

            Position++;
            message.Read(this);
        }

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

        // NaN や無限大もそのまま返すのは、妥当性をメッセージが見るため。
        public float ReadSingle()
        {
            var at = Take(4, "a float");
            return SingleAt(at);
        }

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

        public float ReadFiniteSingleBetween(float min, float max, string what)
        {
            Require(4, what);
            var value = SingleAt(_data.Offset + Position);
            if (!IsFinite(value))
            {
                throw NotFinite(what);
            }

            if (value < min || value > max)
            {
                throw new MessageReadException(
                    what + " must be within " + min.ToString(CultureInfo.InvariantCulture) + " to "
                    + max.ToString(CultureInfo.InvariantCulture) + " (got " + value.ToString(CultureInfo.InvariantCulture)
                    + ") at byte " + Text(Position));
            }

            Position += 4;
            return value;
        }

        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(ReadInt64());
        }

        public Vector2 ReadVector2()
        {
            var at = Take(8, "a 2D vector");
            return new Vector2(SingleAt(at), SingleAt(at + 4));
        }

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

        public Vector3 ReadVector3()
        {
            var at = Take(12, "a vector");
            return new Vector3(SingleAt(at), SingleAt(at + 4), SingleAt(at + 8));
        }

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

        public string ReadString()
        {
            return ReadString(MessageWriter.MaxStringBytes, null);
        }

        // 毎 tick 届く Id で GC を起こさないよう、中身が reuse と同じ ASCII の文字列なら新しい文字列を作らずに reuse を返す。
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

        public ArraySegment<byte> ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            var at = Take(count, Text(count) + " bytes");
            return new ArraySegment<byte>(_data.Array, at, count);
        }

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
