using System;
using System.Linq;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class AntiCheatMessageTests
    {
        [Test]
        public void APacketReachesTheServerUnchanged()
        {
            var packet = Enumerable.Range(0, AntiCheatPacket.MaxPacketSize).Select(i => (byte)i).ToArray();
            var sent = new AntiCheatToServerMessage();
            sent.SetPacket(new ArraySegment<byte>(packet));

            var got = (AntiCheatToServerMessage)MatchMessages.CreateServerTable().Read(Write(sent));

            Assert.That(got.Packet.ToArray(), Is.EqualTo(packet));
        }

        [Test]
        public void APacketReachesTheClientUnchangedAndIsCopiedOutOfTheReceiveBuffer()
        {
            var sent = new AntiCheatToClientMessage();
            sent.SetPacket(new ArraySegment<byte>(new byte[] { 1, 1, 0 }));
            var bytes = Write(sent);

            var got = (AntiCheatToClientMessage)MatchMessages.CreateClientTable().Read(bytes);
            Array.Clear(bytes.Array, 0, bytes.Array.Length);

            Assert.That(got.Packet.ToArray(), Is.EqualTo(new byte[] { 1, 1, 0 }));
        }

        [Test]
        public void EachSideOnlyAcceptsItsOwnDirection()
        {
            var toServer = new AntiCheatToServerMessage();
            toServer.SetPacket(new ArraySegment<byte>(new byte[] { 2, 7 }));

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(Write(toServer)));
        }

        [Test]
        public void EmptyAndOversizedPacketsAreNotRead()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.AntiCheatToServer);
            writer.WriteByte(AntiCheatMessage.CurrentVersion);
            var table = MatchMessages.CreateServerTable();

            Assert.Throws<MessageReadException>(() => table.Read(writer.AsSegment()));

            writer.WriteBytes(new ArraySegment<byte>(new byte[AntiCheatPacket.MaxPacketSize + 1]));
            Assert.Throws<MessageReadException>(() => table.Read(writer.AsSegment()));
        }

        [Test]
        public void EmptyAndOversizedPacketsCannotBeSet()
        {
            var message = new AntiCheatToServerMessage();

            Assert.Throws<ArgumentException>(() => message.SetPacket(new ArraySegment<byte>(Array.Empty<byte>())));
            Assert.Throws<ArgumentException>(() => message.SetPacket(new ArraySegment<byte>(new byte[AntiCheatPacket.MaxPacketSize + 1])));
            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(message));
        }

        private static ArraySegment<byte> Write(IMessage message)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            return new ArraySegment<byte>(writer.ToArray());
        }
    }
}
