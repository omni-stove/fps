using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AntiCheatPacketTests
    {
        [Test]
        public void HelloRoundTrips()
        {
            var buffer = new byte[AntiCheatPacket.MaxPacketSize];

            foreach (var enabled in new[] { true, false })
            {
                var length = AntiCheatPacket.WriteHello(buffer, enabled);
                var read = AntiCheatPacket.Read(new ArraySegment<byte>(buffer, 0, length), out var error);

                Assert.That(length, Is.EqualTo(AntiCheatPacket.HelloSize));
                Assert.That(error, Is.Null);
                Assert.That(read.Kind, Is.EqualTo(AntiCheatPacketKind.Hello));
                Assert.That(read.PeerEnabled, Is.EqualTo(enabled));
            }
        }

        [Test]
        public void MessageRoundTripsFromAnOffset()
        {
            var source = new byte[] { 9, 9, 1, 2, 3, 9 };
            var buffer = new byte[AntiCheatPacket.MaxPacketSize + 4];

            var length = AntiCheatPacket.WriteMessage(buffer, new ArraySegment<byte>(source, 2, 3));
            var shifted = new byte[length + 2];
            Buffer.BlockCopy(buffer, 0, shifted, 2, length);
            var read = AntiCheatPacket.Read(new ArraySegment<byte>(shifted, 2, length), out var error);

            Assert.That(length, Is.EqualTo(4));
            Assert.That(error, Is.Null);
            Assert.That(read.Kind, Is.EqualTo(AntiCheatPacketKind.Message));
            var expected = new byte[] { 1, 2, 3 };
            Assert.That(read.Payload.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void MessageAcceptsTheLargestPayload()
        {
            var buffer = new byte[AntiCheatPacket.MaxPacketSize];
            var payload = new byte[AntiCheatPacket.MaxPayloadSize];

            var length = AntiCheatPacket.WriteMessage(buffer, new ArraySegment<byte>(payload));
            var read = AntiCheatPacket.Read(new ArraySegment<byte>(buffer, 0, length), out _);

            Assert.That(read.Kind, Is.EqualTo(AntiCheatPacketKind.Message));
            Assert.That(read.Payload.Count, Is.EqualTo(AntiCheatPacket.MaxPayloadSize));
        }

        [Test]
        public void WriteMessageRejectsEmptyOrOversized()
        {
            var buffer = new byte[AntiCheatPacket.MaxPacketSize + 16];

            Assert.Throws<ArgumentException>(() => AntiCheatPacket.WriteMessage(buffer, new ArraySegment<byte>(Array.Empty<byte>())));
            Assert.Throws<ArgumentException>(() => AntiCheatPacket.WriteMessage(buffer, default));
            Assert.Throws<ArgumentException>(() =>
                AntiCheatPacket.WriteMessage(buffer, new ArraySegment<byte>(new byte[AntiCheatPacket.MaxPayloadSize + 1])));
        }

        [Test]
        public void WriteRejectsSmallBuffers()
        {
            Assert.Throws<ArgumentException>(() => AntiCheatPacket.WriteHello(new byte[2], true));
            Assert.Throws<ArgumentException>(() =>
                AntiCheatPacket.WriteMessage(new byte[3], new ArraySegment<byte>(new byte[3])));
            Assert.Throws<ArgumentNullException>(() => AntiCheatPacket.WriteHello(null, true));
        }

        [TestCase(new byte[] { 7, 1, 1 }, "unknown packet kind")]
        [TestCase(new byte[] { 1, 2, 1 }, "protocol version")]
        [TestCase(new byte[] { 1, 1 }, "hello must be")]
        [TestCase(new byte[] { 1, 1, 1, 0 }, "hello must be")]
        [TestCase(new byte[] { 1, 1, 2 }, "enabled flag")]
        [TestCase(new byte[] { 2 }, "message must be")]
        public void ReadRejectsMalformedPackets(byte[] packet, string expectedError)
        {
            var read = AntiCheatPacket.Read(new ArraySegment<byte>(packet), out var error);

            Assert.That(read.Kind, Is.EqualTo(AntiCheatPacketKind.Invalid));
            Assert.That(error, Does.Contain(expectedError));
        }

        [Test]
        public void ReadRejectsOversizedMessage()
        {
            var packet = new byte[AntiCheatPacket.MaxPacketSize + 1];
            packet[0] = (byte)AntiCheatPacketKind.Message;

            var read = AntiCheatPacket.Read(new ArraySegment<byte>(packet), out var error);

            Assert.That(read.Kind, Is.EqualTo(AntiCheatPacketKind.Invalid));
            Assert.That(error, Does.Contain("message must be"));
        }

        [Test]
        public void ReadRejectsEmptyPackets()
        {
            var fromDefault = AntiCheatPacket.Read(default, out var defaultError);
            var fromEmpty = AntiCheatPacket.Read(new ArraySegment<byte>(Array.Empty<byte>()), out var emptyError);

            Assert.That(fromDefault.Kind, Is.EqualTo(AntiCheatPacketKind.Invalid));
            Assert.That(fromEmpty.Kind, Is.EqualTo(AntiCheatPacketKind.Invalid));
            Assert.That(defaultError, Does.Contain("empty"));
            Assert.That(emptyError, Does.Contain("empty"));
        }
    }
}
