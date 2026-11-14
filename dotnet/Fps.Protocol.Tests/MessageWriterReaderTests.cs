using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class MessageWriterReaderTests
    {
        [Test]
        public void EveryValueRoundTrips()
        {
            var writer = new MessageWriter();
            writer.WriteByte(200);
            writer.WriteBool(true);
            writer.WriteBool(false);
            writer.WriteUInt16(0xBEEF);
            writer.WriteInt32(-123456789);
            writer.WriteUInt32(0xDEADBEEF);
            writer.WriteInt64(long.MinValue + 1);
            writer.WriteUInt64(ulong.MaxValue - 1);
            writer.WriteSingle(-1.5f);
            writer.WriteVector3(new Vector3(1f, -2.25f, 3.5f));
            writer.WriteString("こんにちは, world");
            writer.WriteBytes(new ArraySegment<byte>(new byte[] { 9, 8, 7 }));

            var reader = new MessageReader(writer.ToArray());

            Assert.That(reader.ReadByte(), Is.EqualTo(200));
            Assert.That(reader.ReadBool(), Is.True);
            Assert.That(reader.ReadBool(), Is.False);
            Assert.That(reader.ReadUInt16(), Is.EqualTo(0xBEEF));
            Assert.That(reader.ReadInt32(), Is.EqualTo(-123456789));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(0xDEADBEEF));
            Assert.That(reader.ReadInt64(), Is.EqualTo(long.MinValue + 1));
            Assert.That(reader.ReadUInt64(), Is.EqualTo(ulong.MaxValue - 1));
            Assert.That(reader.ReadSingle(), Is.EqualTo(-1.5f));
            Assert.That(reader.ReadVector3(), Is.EqualTo(new Vector3(1f, -2.25f, 3.5f)));
            Assert.That(reader.ReadString(), Is.EqualTo("こんにちは, world"));
            Assert.That(reader.ReadBytes(3).ToArray(), Is.EqualTo(new byte[] { 9, 8, 7 }));
            Assert.That(reader.IsAtEnd, Is.True);
            reader.ExpectEnd();
        }

        [Test]
        public void IntegersAreLittleEndian()
        {
            var writer = new MessageWriter();
            writer.WriteUInt32(0x04030201);

            Assert.That(writer.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void NullStringIsWrittenAsEmpty()
        {
            var writer = new MessageWriter();
            writer.WriteString(null);

            Assert.That(new MessageReader(writer.ToArray()).ReadString(), Is.Empty);
        }

        [Test]
        public void NaNSurvivesAsIs()
        {
            var writer = new MessageWriter();
            writer.WriteSingle(float.NaN);

            Assert.That(float.IsNaN(new MessageReader(writer.ToArray()).ReadSingle()), Is.True);
        }

        [Test]
        public void MessageIdIsWrittenBeforeTheBody()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new EchoMessage { Text = "hi" });

            var reader = new MessageReader(writer.AsSegment());
            Assert.That(reader.ReadMessageId(), Is.EqualTo(EchoMessage.MessageId));
            var echo = new EchoMessage();
            echo.Read(reader);
            Assert.That(echo.Text, Is.EqualTo("hi"));
            reader.ExpectEnd();
        }

        [Test]
        public void ResetReusesTheWriter()
        {
            var writer = new MessageWriter();
            writer.WriteInt32(1);
            writer.Reset();
            writer.WriteByte(5);

            Assert.That(writer.Length, Is.EqualTo(1));
            Assert.That(writer.ToArray(), Is.EqualTo(new byte[] { 5 }));
        }

        [Test]
        public void WriterGrowsPastItsFirstBuffer()
        {
            var writer = new MessageWriter();
            for (var i = 0; i < 1000; i++)
            {
                writer.WriteInt32(i);
            }

            var reader = new MessageReader(writer.ToArray());
            for (var i = 0; i < 1000; i++)
            {
                Assert.That(reader.ReadInt32(), Is.EqualTo(i));
            }

            Assert.That(reader.IsAtEnd, Is.True);
        }

        [Test]
        public void WritingPastMaxLengthThrowsAndKeepsWhatWasWritten()
        {
            var writer = new MessageWriter(5);
            writer.WriteInt32(7);

            Assert.Throws<InvalidOperationException>(() => writer.WriteUInt16(1));
            Assert.That(writer.Length, Is.EqualTo(4));
        }

        [Test]
        public void NonPositiveMaxLengthIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MessageWriter(0));
        }

        [Test]
        public void StringLongerThanTheLengthPrefixIsRejected()
        {
            var writer = new MessageWriter(int.MaxValue);

            Assert.Throws<ArgumentException>(() => writer.WriteString(new string('a', MessageWriter.MaxStringBytes + 1)));
            Assert.That(writer.Length, Is.Zero);
        }

        [Test]
        public void LoneSurrogateIsRejected()
        {
            var writer = new MessageWriter();

            Assert.Throws<ArgumentException>(() => writer.WriteString("\ud800"));
            Assert.That(writer.Length, Is.Zero);
        }

        [Test]
        public void ReadingPastTheEndThrowsWithoutMoving()
        {
            var reader = new MessageReader(new byte[] { 1, 2, 3 });

            Assert.Throws<MessageReadException>(() => reader.ReadInt32());
            Assert.That(reader.Position, Is.Zero);
            Assert.That(reader.ReadUInt16(), Is.EqualTo(0x0201));
        }

        [Test]
        public void StringLongerThanTheDataIsRejected()
        {
            var reader = new MessageReader(new byte[] { 10, 0, (byte)'a', (byte)'b' });

            Assert.Throws<MessageReadException>(() => reader.ReadString());
            Assert.That(reader.Position, Is.Zero);
        }

        [Test]
        public void InvalidUtf8IsRejected()
        {
            var reader = new MessageReader(new byte[] { 2, 0, 0xC3, 0x28 });

            Assert.Throws<MessageReadException>(() => reader.ReadString());
        }

        [Test]
        public void BoolOtherThanZeroOrOneIsRejected()
        {
            var reader = new MessageReader(new byte[] { 2 });

            Assert.Throws<MessageReadException>(() => reader.ReadBool());
            Assert.That(reader.Position, Is.Zero);
        }

        [Test]
        public void LeftoverBytesFailExpectEnd()
        {
            var reader = new MessageReader(new byte[] { 1, 2 });
            reader.ReadByte();

            Assert.Throws<MessageReadException>(reader.ExpectEnd);
        }

        [Test]
        public void ReaderReadsOnlyItsSegment()
        {
            var bytes = new byte[] { 0xFF, 7, 0, 0xFF };
            var reader = new MessageReader(new ArraySegment<byte>(bytes, 1, 2));

            Assert.That(reader.ReadUInt16(), Is.EqualTo(7));
            Assert.That(reader.IsAtEnd, Is.True);
            Assert.Throws<MessageReadException>(() => reader.ReadByte());
        }

        [Test]
        public void ReadBytesPointsIntoTheReceivedData()
        {
            var bytes = new byte[] { 1, 2, 3, 4 };
            var reader = new MessageReader(new ArraySegment<byte>(bytes, 1, 3));
            reader.ReadByte();

            var segment = reader.ReadBytes(2);

            Assert.That(segment.Array, Is.SameAs(bytes));
            Assert.That(segment.Offset, Is.EqualTo(2));
            Assert.That(segment.Count, Is.EqualTo(2));
        }

        private sealed class EchoMessage : IMessage
        {
            public const ushort MessageId = 42;

            public string Text { get; set; }

            public ushort Id => MessageId;

            public void Write(MessageWriter writer)
            {
                writer.WriteString(Text);
            }

            public void Read(MessageReader reader)
            {
                Text = reader.ReadString();
            }
        }
    }
}
