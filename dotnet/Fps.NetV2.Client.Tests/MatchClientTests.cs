using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class MatchClientTests
    {
        private FakeTransport _transport;
        private MatchClient _client;
        private List<string> _log;
        private List<DisconnectNotice> _notices;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeTransport();
            _log = new List<string>();
            _notices = new List<DisconnectNotice>();
            _client = new MatchClient(_transport, "0.2.0", "device1", "tester", _log.Add);
            _client.Disconnected += _notices.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _transport.Dispose();
        }

        [Test]
        public void SendsItsRequestOnceTheTransportConnectsAndIsAcceptedWithTheServersMap()
        {
            var accepted = 0;
            _client.Accepted += () => accepted++;
            _client.Connect("127.0.0.1", 7777);
            _transport.Connected();
            _client.Poll();

            Assert.That(_client.State, Is.EqualTo(MatchClientState.Approving));
            Assert.That(_transport.Sent, Is.EqualTo(new[] { (TransportChannel.Reliable, MessageIds.ConnectRequest) }));

            Accept();

            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(_client.State, Is.EqualTo(MatchClientState.Connected));
            Assert.That(_client.PlayerId, Is.EqualTo(3));
            Assert.That(_client.Map, Is.EqualTo("NetArena01"));
            Assert.That(_client.MaxHealth, Is.EqualTo(120));
            Assert.That(_log, Has.Some.Contains("[Net] client connected id=3 (local)"));
        }

        [Test]
        public void ServerStoppingIsTheServerStoppedNoticeEvenWhenTheTransportGivesNoReason()
        {
            Connect();
            _transport.Deliver(new ServerDisconnectingMessage { Reason = DisconnectNotice.ServerStoppedReason });
            _transport.Disconnected("");
            _client.Poll();

            Assert.That(_notices, Has.Count.EqualTo(1));
            Assert.That(_notices[0].Kind, Is.EqualTo(DisconnectKind.ServerStopped));
            Assert.That(_notices[0].WasConnected, Is.True);
            Assert.That(_client.State, Is.EqualTo(MatchClientState.Disconnected));
        }

        [Test]
        public void ARejectedVersionIsTheVersionMismatchNotice()
        {
            _client.Connect("127.0.0.1", 7777);
            _transport.Connected();
            _transport.Deliver(new ConnectRejectedMessage { Reason = DisconnectNotice.VersionMismatchReason("0.3.0") });
            _transport.Disconnected(DisconnectNotice.VersionMismatchReason("0.3.0"));
            _client.Poll();

            Assert.That(_notices[0].Kind, Is.EqualTo(DisconnectKind.VersionMismatch));
            Assert.That(_notices[0].ServerVersion, Is.EqualTo("0.3.0"));
            Assert.That(_notices[0].WasConnected, Is.False);
        }

        [Test]
        public void AServerRefusingToApproveIsTheRejectedNotice()
        {
            _client.Connect("127.0.0.1", 7777);
            _transport.Connected();
            _transport.Disconnected(DisconnectNotice.RejectedReason(DisconnectNotice.ServerDrainingDetail));
            _client.Poll();

            Assert.That(_notices[0].Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(_notices[0].Detail, Is.EqualTo(DisconnectNotice.ServerDrainingDetail));
        }

        [Test]
        public void LosingTheServerWithoutAWordIsATimeout()
        {
            Connect();
            _transport.Disconnected("");
            _client.Poll();

            Assert.That(_notices[0].Kind, Is.EqualTo(DisconnectKind.TimedOut));
            Assert.That(_notices[0].WasConnected, Is.True);
        }

        [Test]
        public void NeverReachingTheServerIsAConnectTimeout()
        {
            _client.Connect("127.0.0.1", 7777);
            _transport.Disconnected("");
            _client.Poll();

            Assert.That(_notices[0].Kind, Is.EqualTo(DisconnectKind.TimedOut));
            Assert.That(_notices[0].WasConnected, Is.False);
        }

        [Test]
        public void LeavingDoesNotReportADisconnect()
        {
            Connect();
            _client.Leave();
            _transport.Disconnected("");
            _client.Poll();

            Assert.That(_notices, Is.Empty);
            Assert.That(_transport.ShutDown, Is.True);
        }

        [Test]
        public void DeltaSnapshotsAreResolvedAgainstTheKeptBaselineAndAcknowledgedInTheNextInput()
        {
            Connect();
            var received = new List<int>();
            _client.SnapshotReceived += snapshot => received.Add(snapshot.Tick);
            var first = Snapshot(10, new Vector3(1f, 0f, 0f));
            _transport.Deliver(first);
            _client.Poll();
            var second = Snapshot(11, new Vector3(2f, 0f, 0f));
            second.Baseline = first;
            _transport.Deliver(second);
            _client.Poll();

            Assert.That(received, Is.EqualTo(Ticks(10, 11)));
            Assert.That(_client.LatestSnapshot.Players[0].Position, Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(_client.LatestSnapshot.Players[0].Team, Is.EqualTo(Team.Bravo), "unchanged fields come from the baseline");

            var input = new InputMessage();
            input.Add(new InputFrame(0, Vector2.Zero, 0f, InputButtons.None));
            _client.SendInput(input);

            Assert.That(_transport.LastInput.AckedSnapshotTick, Is.EqualTo(11));
            Assert.That(_transport.Sent[^1].Channel, Is.EqualTo(TransportChannel.Unreliable));
        }

        [Test]
        public void AnOlderSnapshotIsKeptAsABaselineButNotReported()
        {
            Connect();
            var received = new List<int>();
            _client.SnapshotReceived += snapshot => received.Add(snapshot.Tick);
            _transport.Deliver(Snapshot(12, Vector3.Zero));
            _transport.Deliver(Snapshot(11, Vector3.One));
            _client.Poll();

            Assert.That(received, Is.EqualTo(Ticks(12)));
            Assert.That(_client.LatestSnapshot.Tick, Is.EqualTo(12));
        }

        [Test]
        public void ADeltaWithoutItsBaselineIsDropped()
        {
            Connect();
            var baseline = Snapshot(5, Vector3.Zero);
            var delta = Snapshot(6, Vector3.One);
            delta.Baseline = baseline;
            _transport.Deliver(delta);
            _client.Poll();

            Assert.That(_client.LatestSnapshot, Is.Null);
            Assert.That(_client.UnresolvedSnapshots, Is.EqualTo(1));
        }

        [Test]
        public void EventsAfterAcceptanceArePassedOn()
        {
            Connect();
            var events = new List<ushort>();
            _client.MessageReceived += message => events.Add(message.Id);
            _transport.Deliver(new KilledMessage { KillerId = 1, VictimId = 2, WeaponId = "rifle" });
            _client.Poll();

            Assert.That(events, Is.EqualTo(new[] { MessageIds.Killed }));
        }

        [Test]
        public void NothingIsSentBeforeAcceptance()
        {
            _client.Connect("127.0.0.1", 7777);
            _client.Send(new PingRequestMessage(), TransportChannel.Reliable);

            Assert.That(_transport.Sent, Is.Empty);
        }

        private void Connect()
        {
            _client.Connect("127.0.0.1", 7777);
            _transport.Connected();
            _client.Poll();
            Accept();
        }

        private void Accept()
        {
            _transport.Deliver(new ConnectAcceptedMessage
            {
                PlayerId = 3,
                ServerVersion = "0.2.0",
                TickRate = 60,
                ServerTick = 100,
                Map = "NetArena01",
                MaxHealth = 120,
            });
            _client.Poll();
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static int[] Ticks(params int[] values) => values;

        private static SnapshotMessage Snapshot(int tick, Vector3 position)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = 3;
            row.Team = Team.Bravo;
            row.Position = position;
            row.WeaponId = "";
            snapshot.HasOwner = true;
            return snapshot;
        }
    }
}
