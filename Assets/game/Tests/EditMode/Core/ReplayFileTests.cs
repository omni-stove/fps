using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ReplayFileTests
    {
        private static readonly DateTime _recordedAt = new DateTime(2026, 11, 18, 9, 15, 30, DateTimeKind.Utc);

        [Test]
        public void RecordsComeBackInTheOrderAndTickTheyWereWritten()
        {
            var output = new MemoryStream();
            using (var writer = new ReplayWriter(output, Header(), true))
            {
                writer.Append(0, ReplayRecordKind.Connected, 1, 0, default);
                writer.Append(2, ReplayRecordKind.Data, 1, 0, new ArraySegment<byte>(new byte[] { 10, 20, 30 }));
                writer.Append(2, ReplayRecordKind.Data, 1, 1, new ArraySegment<byte>(new byte[] { 40 }));
                writer.Append(9, ReplayRecordKind.Disconnected, 1, 0, new ArraySegment<byte>(new byte[] { (byte)'b', (byte)'y', (byte)'e' }));
                writer.Finish(12);
            }

            var read = new List<string>();
            using (var reader = new ReplayReader(new MemoryStream(output.ToArray())))
            {
                Assert.That(reader.Header.DurationTicks, Is.EqualTo(12));
                Assert.That(reader.Header.DurationSeconds, Is.EqualTo(0.4f).Within(1e-6f));
                while (reader.TryRead(out var record))
                {
                    read.Add(record.Tick + " " + record.Kind + " ch" + record.Channel + " " + BitConverter.ToString(record.Payload.Array, record.Payload.Offset, record.Payload.Count));
                }

                Assert.That(reader.IsTruncated, Is.False);
                Assert.That(reader.Count, Is.EqualTo(4));
            }

            Assert.That(read, Is.EqualTo(new[]
            {
                "0 Connected ch0 ",
                "2 Data ch0 0A-14-1E",
                "2 Data ch1 28",
                "9 Disconnected ch0 62-79-65",
            }));
        }

        [Test]
        public void TheHeaderKeepsEveryField()
        {
            var output = new MemoryStream();
            using (new ReplayWriter(output, Header(), true))
            {
            }

            var header = ReplayFile.ReadHeader(new MemoryStream(output.ToArray()));
            Assert.That(header.ProtocolVersion, Is.EqualTo((ushort)21));
            Assert.That(header.ClientVersion, Is.EqualTo("1.4.0"));
            Assert.That(header.MapId, Is.EqualTo("Harbor"));
            Assert.That(header.RecordedAtUtc, Is.EqualTo(_recordedAt));
            Assert.That(header.RecordedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(header.TickRate, Is.EqualTo(30));
            Assert.That(header.PlayerId, Is.EqualTo(4));
            Assert.That(header.DurationTicks, Is.EqualTo(0), "closing with no records writes a zero length");
        }

        [Test]
        public void TheListEntryReadsTheWrittenLengthOrCountsToTheLastRecord()
        {
            var finished = new MemoryStream();
            using (var writer = new ReplayWriter(finished, Header(), true))
            {
                writer.Append(15, ReplayRecordKind.Connected, 1, 0, default);
                writer.Finish(90);
            }

            var entry = ReplayFile.ReadEntry("20261118-091530", new MemoryStream(finished.ToArray()), out var header);
            Assert.That(entry, Is.EqualTo(new ReplayEntry("20261118-091530", "Harbor", _recordedAt, 3f)));
            Assert.That(header.PlayerId, Is.EqualTo(4));

            // 長さを書き戻す前に落ちたファイル (シークできない流れも同じ) は、最後の記録までを長さとする
            var unfinished = new MemoryStream();
            var open = new ReplayWriter(unfinished, Header(), true);
            open.Append(15, ReplayRecordKind.Connected, 1, 0, default);
            open.Append(45, ReplayRecordKind.Data, 1, 0, new ArraySegment<byte>(new byte[] { 1, 2, 3 }));
            var cut = unfinished.ToArray();

            Assert.That(ReplayFile.ReadEntry("crashed", new MemoryStream(cut), out _).DurationSeconds, Is.EqualTo(1.5f).Within(1e-6f));
        }

        [Test]
        public void TheWriterRefusesRecordsThatGoBackInTime()
        {
            using (var writer = new ReplayWriter(new MemoryStream(), Header()))
            {
                writer.Append(5, ReplayRecordKind.Data, 1, 0, default);
                Assert.Throws<ArgumentOutOfRangeException>(() => writer.Append(4, ReplayRecordKind.Data, 1, 0, default));
                Assert.Throws<ArgumentException>(() => writer.Append(6, ReplayRecordKind.Data, 0, 0, default));
                Assert.Throws<ArgumentException>(
                    () => writer.Append(6, ReplayRecordKind.Data, 1, 0, new ArraySegment<byte>(new byte[ReplayFile.MaxPayloadBytes + 1])));
                Assert.Throws<ArgumentOutOfRangeException>(() => writer.Finish(4));
                writer.Finish(5);
                Assert.Throws<InvalidOperationException>(() => writer.Append(6, ReplayRecordKind.Data, 1, 0, default));
            }
        }

        [Test]
        public void SomethingThatIsNotAReplayIsRefused()
        {
            Assert.Throws<InvalidDataException>(() => ReplayFile.ReadHeader(new MemoryStream(new byte[] { 1, 2, 3 })));

            var wrongMark = Written();
            wrongMark[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => ReplayFile.ReadHeader(new MemoryStream(wrongMark)));

            var wrongFormat = Written();
            wrongFormat[4] = 99;
            var error = Assert.Throws<InvalidDataException>(() => ReplayFile.ReadHeader(new MemoryStream(wrongFormat)));
            Assert.That(error.Message, Does.Contain("format 99"));
        }

        [Test]
        public void ADamagedRecordIsAnErrorButACutOffOneIsTheEnd()
        {
            var bytes = Written(new ArraySegment<byte>(new byte[] { 7, 7 }));
            var recordStart = bytes.Length - ReplayFile.RecordHeaderBytes - 2;

            var unknownKind = (byte[])bytes.Clone();
            unknownKind[recordStart + 4] = 200;
            using (var reader = new ReplayReader(new MemoryStream(unknownKind)))
            {
                Assert.Throws<InvalidDataException>(() => reader.TryRead(out _));
            }

            var tooLong = (byte[])bytes.Clone();
            tooLong[recordStart + 17] = 0x7f;
            using (var reader = new ReplayReader(new MemoryStream(tooLong)))
            {
                Assert.Throws<InvalidDataException>(() => reader.TryRead(out _));
            }

            var cut = new byte[bytes.Length - 1];
            Array.Copy(bytes, cut, cut.Length);
            using (var reader = new ReplayReader(new MemoryStream(cut)))
            {
                Assert.That(reader.TryRead(out _), Is.False);
                Assert.That(reader.IsTruncated, Is.True);
                Assert.That(reader.IsEnded, Is.True);
            }
        }

        [Test]
        public void TheHeaderRefusesValuesThatCannotBeReadBack()
        {
            Assert.Throws<ArgumentException>(() => new ReplayHeader(21, "1.0.0", "Harbor", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local), 30, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayHeader(21, "1.0.0", "Harbor", _recordedAt, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayHeader(21, "1.0.0", "Harbor", _recordedAt, 30, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayHeader(21, "1.0.0", "Harbor", _recordedAt, 30, 1, -2));
            Assert.Throws<ArgumentException>(() => new ReplayHeader(21, "1.0.0", new string('m', ReplayFile.MaxTextLength + 1), _recordedAt, 30, 1));
            Assert.Throws<ArgumentNullException>(() => new ReplayHeader(21, null, "Harbor", _recordedAt, 30, 1));
        }

        private static ReplayHeader Header()
        {
            return new ReplayHeader(21, "1.4.0", "Harbor", _recordedAt, 30, 4);
        }

        private static byte[] Written(ArraySegment<byte> payload = default)
        {
            var output = new MemoryStream();
            using (var writer = new ReplayWriter(output, Header(), true))
            {
                writer.Append(3, ReplayRecordKind.Data, 1, 0, payload);
            }

            return output.ToArray();
        }
    }
}
