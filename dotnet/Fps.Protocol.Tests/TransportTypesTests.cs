using System;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class TransportTypesTests
    {
        [Test]
        public void ConnectionIdComparesByValue()
        {
            Assert.That(new ConnectionId(3) == new ConnectionId(3), Is.True);
            Assert.That(new ConnectionId(3) != new ConnectionId(4), Is.True);
            var first = new ConnectionId(3);
            var second = new ConnectionId(3);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void ConnectionIdZeroIsNone()
        {
            Assert.That(ConnectionId.None.IsValid, Is.False);
            Assert.That(new ConnectionId(1).IsValid, Is.True);
            Assert.That(ConnectionId.None.ToString(), Is.EqualTo("#none"));
            Assert.That(new ConnectionId(12).ToString(), Is.EqualTo("#12"));
        }

        [Test]
        public void DataEventCarriesItsPayload()
        {
            var bytes = new byte[] { 1, 2, 3 };

            var e = TransportEvent.Data(new ConnectionId(5), TransportChannel.Unreliable, new ArraySegment<byte>(bytes, 1, 2));

            Assert.That(e.Kind, Is.EqualTo(TransportEventKind.Data));
            Assert.That(e.Connection, Is.EqualTo(new ConnectionId(5)));
            Assert.That(e.Channel, Is.EqualTo(TransportChannel.Unreliable));
            Assert.That(e.Payload.Count, Is.EqualTo(2));
        }

        [Test]
        public void ConnectionEventsHaveNoPayload()
        {
            var connected = TransportEvent.Connected(new ConnectionId(1));
            var disconnected = TransportEvent.Disconnected(new ConnectionId(1), "fps:server-stopped");

            Assert.That(connected.Kind, Is.EqualTo(TransportEventKind.Connected));
            Assert.That(disconnected.Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(connected.Payload.Count, Is.Zero);
            Assert.That(connected.Reason, Is.Empty);
            Assert.That(disconnected.Reason, Is.EqualTo("fps:server-stopped"));
        }

        [Test]
        public void DisconnectedEventTreatsNullReasonAsEmpty()
        {
            Assert.That(TransportEvent.Disconnected(new ConnectionId(1), null).Reason, Is.Empty);
            Assert.That(default(TransportEvent).Reason, Is.Empty);
        }

        [Test]
        public void EventsNeedAConnection()
        {
            Assert.Throws<ArgumentException>(() => TransportEvent.Connected(ConnectionId.None));
            Assert.Throws<ArgumentException>(() => TransportEvent.Disconnected(ConnectionId.None, ""));
            Assert.Throws<ArgumentException>(
                () => TransportEvent.Data(ConnectionId.None, TransportChannel.Reliable, new ArraySegment<byte>(new byte[1])));
        }

        [Test]
        public void DataEventRejectsUnknownChannelAndMissingArray()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TransportEvent.Data(new ConnectionId(1), (TransportChannel)99, new ArraySegment<byte>(new byte[1])));
            Assert.Throws<ArgumentException>(
                () => TransportEvent.Data(new ConnectionId(1), TransportChannel.Reliable, default));
        }
    }
}
