using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class ReaderHelperTests
    {
        [Test]
        public void DoubleVector2AndQuaternionRoundTrip()
        {
            var writer = new MessageWriter();
            writer.WriteDouble(-12345.678901);
            writer.WriteVector2(new Vector2(0.5f, -0.25f));
            writer.WriteQuaternion(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f));

            var reader = new MessageReader(writer.ToArray());

            Assert.That(reader.ReadDouble(), Is.EqualTo(-12345.678901));
            Assert.That(reader.ReadVector2(), Is.EqualTo(new Vector2(0.5f, -0.25f)));
            Assert.That(reader.ReadFiniteQuaternion("q"), Is.EqualTo(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f)));
            reader.ExpectEnd();
        }

        [Test]
        public void FiniteReadsRejectNaNAndInfinityWithoutMoving()
        {
            var writer = new MessageWriter();
            writer.WriteSingle(float.NaN);
            writer.WriteVector2(new Vector2(1f, float.PositiveInfinity));
            writer.WriteVector3(new Vector3(float.NegativeInfinity, 0f, 0f));
            var bytes = writer.ToArray();

            var reader = new MessageReader(bytes);
            Assert.Throws<MessageReadException>(() => reader.ReadFiniteSingle("x"));
            Assert.That(reader.Position, Is.Zero);
            reader.ReadSingle();
            Assert.Throws<MessageReadException>(() => reader.ReadFiniteVector2("v2"));
            Assert.That(reader.Position, Is.EqualTo(4));
            reader.ReadVector2();
            Assert.Throws<MessageReadException>(() => reader.ReadFiniteVector3("v3"));
            Assert.That(reader.Position, Is.EqualTo(12));
        }

        [Test]
        public void ByteBelowRejectsTheLimitWithoutMoving()
        {
            var reader = new MessageReader(new byte[] { 3, 2 });

            Assert.Throws<MessageReadException>(() => reader.ReadByteBelow(3, "kind"));
            Assert.That(reader.Position, Is.Zero);
            Assert.That(reader.ReadByteBelow(4, "kind"), Is.EqualTo(3));
            Assert.That(reader.ReadByteBelow(3, "kind"), Is.EqualTo(2));
        }

        [Test]
        public void Int32AtLeastRejectsSmallerValuesWithoutMoving()
        {
            var writer = new MessageWriter();
            writer.WriteInt32(-2);
            var reader = new MessageReader(writer.ToArray());

            Assert.Throws<MessageReadException>(() => reader.ReadInt32AtLeast(-1, "tick"));
            Assert.That(reader.Position, Is.Zero);
            Assert.That(reader.ReadInt32AtLeast(-2, "tick"), Is.EqualTo(-2));
        }

        [Test]
        public void StringLongerThanTheLimitIsRejectedOnBothSides()
        {
            var writer = new MessageWriter();

            Assert.Throws<ArgumentException>(() => writer.WriteString("abcd", 3));
            Assert.That(writer.Length, Is.Zero);

            writer.WriteString("abcd");
            var reader = new MessageReader(writer.ToArray());
            Assert.Throws<MessageReadException>(() => reader.ReadString(3, null));
            Assert.That(reader.Position, Is.Zero);
            Assert.That(reader.ReadString(4, null), Is.EqualTo("abcd"));
        }

        [Test]
        public void SameAsciiStringIsReusedAndDifferentOneIsNot()
        {
            var writer = new MessageWriter();
            writer.WriteString("rifle");
            writer.WriteString("rifle");
            writer.WriteString("ライフル");
            var reader = new MessageReader(writer.ToArray());
            var reuse = new string("rifle".ToCharArray());

            Assert.That(reader.ReadString(64, reuse), Is.SameAs(reuse));
            var other = reader.ReadString(64, "pistol");
            Assert.That(other, Is.EqualTo("rifle"));
            Assert.That(reader.ReadString(64, "ライフル"), Is.EqualTo("ライフル"));
        }

        [Test]
        public void ResetReadsNewDataFromTheStart()
        {
            var reader = new MessageReader(new byte[] { 1 });
            reader.ReadByte();

            reader.Reset(new ArraySegment<byte>(new byte[] { 9, 8 }, 1, 1));

            Assert.That(reader.Position, Is.Zero);
            Assert.That(reader.ReadByte(), Is.EqualTo(8));
            Assert.That(reader.IsAtEnd, Is.True);
        }
    }
}
