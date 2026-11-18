using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Text;

namespace Shooter.Core
{
    // 形: 先頭に "SKRP"・形の版・ReplayHeader、続いて記録 (tick・種類・接続・経路・長さ・中身) を tick の順に並べる。数はすべてリトルエンディアン。
    // 長さは記録を閉じるときに書き戻すので、ヘッダーの中の位置を固定する (DurationOffset)。
    public static class ReplayFile
    {
        public const string Extension = ".replay";

        public const int FormatVersion = 1;

        public const int MaxTextLength = 128;

        // 届くメッセージは通信路の 1 パケットに収まるので、これを超える長さは壊れたファイルとして読まない。
        public const int MaxPayloadBytes = 64 * 1024;

        public const int RecordHeaderBytes = 4 + 1 + 8 + 1 + 4;

        private static readonly byte[] _magic = { (byte)'S', (byte)'K', (byte)'R', (byte)'P' };
        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);

        // 長さを書き戻せるよう、可変長の欄 (文字) より前に置く。
        public const int DurationOffset = 4 + 4;

        public const int FixedHeaderBytes = DurationOffset + 4 + 2 + 8 + 4 + 4;

        public static void WriteHeader(Stream stream, ReplayHeader header)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            var fixedPart = new byte[FixedHeaderBytes];
            _magic.CopyTo(fixedPart, 0);
            var span = fixedPart.AsSpan();
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4), FormatVersion);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(DurationOffset), header.DurationTicks);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(DurationOffset + 4), header.ProtocolVersion);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(DurationOffset + 6), header.RecordedAtUtc.Ticks);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(DurationOffset + 14), header.TickRate);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(DurationOffset + 18), header.PlayerId);
            stream.Write(fixedPart, 0, fixedPart.Length);
            WriteText(stream, header.ClientVersion);
            WriteText(stream, header.MapId);
        }

        public static ReplayHeader ReadHeader(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var fixedPart = new byte[FixedHeaderBytes];
            if (Fill(stream, fixedPart, 0, fixedPart.Length) != fixedPart.Length)
            {
                throw new InvalidDataException("the file is too short to be a replay");
            }

            var span = new ReadOnlySpan<byte>(fixedPart);
            if (!span.Slice(0, 4).SequenceEqual(_magic))
            {
                throw new InvalidDataException("the file is not a replay (no SKRP mark)");
            }

            var format = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4));
            if (format != FormatVersion)
            {
                throw new InvalidDataException(
                    "the replay is format " + Text(format) + " but this build reads only format " + Text(FormatVersion));
            }

            var duration = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(DurationOffset));
            var protocol = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(DurationOffset + 4));
            var recordedTicks = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(DurationOffset + 6));
            var tickRate = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(DurationOffset + 14));
            var playerId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(DurationOffset + 18));
            if (recordedTicks < DateTime.MinValue.Ticks || recordedTicks > DateTime.MaxValue.Ticks)
            {
                throw new InvalidDataException("the replay's recording time is out of range");
            }

            var clientVersion = ReadText(stream, "client version");
            var mapId = ReadText(stream, "map");
            try
            {
                return new ReplayHeader(
                    protocol, clientVersion, mapId, new DateTime(recordedTicks, DateTimeKind.Utc), tickRate, playerId, duration);
            }
            catch (ArgumentException e)
            {
                throw new InvalidDataException("the replay header is not valid: " + e.Message, e);
            }
        }

        // 一覧に並べるだけなので記録の中身は読まずに飛ばし、長さが書き戻されていないときだけ最後の tick まで数える。
        public static ReplayEntry ReadEntry(string id, Stream stream, out ReplayHeader header)
        {
            using (var reader = new ReplayReader(stream, true))
            {
                header = reader.Header;
                var ticks = header.DurationTicks;
                if (!header.HasDuration)
                {
                    ticks = reader.SkipToEnd();
                }

                return new ReplayEntry(id, header.MapId, header.RecordedAtUtc, ticks / (float)header.TickRate);
            }
        }

        public static void WriteDuration(Stream stream, long headerStart, int durationTicks)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (durationTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks), durationTicks, "durationTicks must not be negative");
            }

            var bytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, durationTicks);
            var end = stream.Position;
            stream.Position = headerStart + DurationOffset;
            stream.Write(bytes, 0, bytes.Length);
            stream.Position = end;
        }

        public static void WriteRecordHeader(Span<byte> destination, int tick, ReplayRecordKind kind, ulong connection, byte channel, int length)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, tick);
            destination[4] = (byte)kind;
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(5), connection);
            destination[13] = channel;
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(14), length);
        }

        // Stream.Read は求めた長さより少なく返すことがあるので、終わりに着くか埋まるまで読み続ける。
        public static int Fill(Stream stream, byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (total < count)
            {
                var read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        internal static string Text(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static void WriteText(Stream stream, string value)
        {
            var bytes = _utf8.GetBytes(value ?? string.Empty);
            var length = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(length, (ushort)bytes.Length);
            stream.Write(length, 0, length.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string ReadText(Stream stream, string what)
        {
            var length = new byte[2];
            if (Fill(stream, length, 0, 2) != 2)
            {
                throw new InvalidDataException("the replay header ends before its " + what);
            }

            var count = BinaryPrimitives.ReadUInt16LittleEndian(length);
            if (count > MaxTextLength * 4)
            {
                throw new InvalidDataException("the replay's " + what + " is " + Text(count) + " bytes; at most " + Text(MaxTextLength * 4) + " are read");
            }

            var bytes = new byte[count];
            if (Fill(stream, bytes, 0, count) != count)
            {
                throw new InvalidDataException("the replay header ends inside its " + what);
            }

            try
            {
                return _utf8.GetString(bytes);
            }
            catch (DecoderFallbackException e)
            {
                throw new InvalidDataException("the replay's " + what + " is not UTF-8", e);
            }
        }
    }
}
