using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    // サーバーが Concealed を立てた相手の行は、名簿に残したまま姿を描かせないことを確かめる
    public sealed class ClientMatchConcealTests
    {
        private const int _playerId = 3;
        private const int _enemyId = 7;
        private static readonly Vector3 _spawn = new Vector3(0f, 1.1f, 0f);
        private static readonly Vector3 _before = new Vector3(0f, 1.1f, 20f);
        private static readonly Vector3 _after = new Vector3(5f, 1.1f, 5f);

        private FakeTransport _transport;
        private MatchClient _client;
        private ClientMatch _match;
        private List<int> _added;
        private List<int> _removed;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeTransport();
            _client = new MatchClient(_transport, "0.2.0", "device1", "tester", _ => { });
            _added = new List<int>();
            _removed = new List<int>();
        }

        [TearDown]
        public void TearDown()
        {
            _match?.Detach();
            _client.Dispose();
            _transport.Dispose();
        }

        [Test]
        public void AConcealedPlayerStaysOnTheRosterButHasNoPose()
        {
            Join();

            Deliver(2, concealed: true, _enemyId, Vector3.Zero);

            Assert.That(_added, Is.EqualTo(new[] { _enemyId }));
            Assert.That(_removed, Is.Empty);
            Assert.That(_match.RemotePlayerIds, Does.Contain(_enemyId));
            Assert.That(_match.IsConcealed(_enemyId), Is.True);
            Assert.That(_match.TrySampleRemote(_enemyId, out _), Is.False);
        }

        [Test]
        public void HidingAShownPlayerDropsItsPoseWithoutRemovingIt()
        {
            Join();
            Deliver(2, concealed: false, _enemyId, _before);
            Assert.That(_match.TrySampleRemote(_enemyId, out _), Is.True);

            Deliver(3, concealed: true, _enemyId, Vector3.Zero);

            Assert.That(_removed, Is.Empty);
            Assert.That(_match.IsConcealed(_enemyId), Is.True);
            Assert.That(_match.TrySampleRemote(_enemyId, out _), Is.False);
        }

        [Test]
        public void ShownAgainItIsRecordedFreshFromThatTick()
        {
            Join();
            Deliver(2, concealed: false, _enemyId, _before);
            Deliver(3, concealed: true, _enemyId, Vector3.Zero);

            Deliver(4, concealed: false, _enemyId, _after);

            Assert.That(_match.IsConcealed(_enemyId), Is.False);
            Assert.That(_match.TrySampleRemote(_enemyId, out var pose), Is.True);
            Assert.That(pose.Position, Is.EqualTo(_after), "the pose does not slide from where it was before it hid");
            Assert.That(_added, Is.EqualTo(new[] { _enemyId }), "showing again is not a new player");
        }

        [Test]
        public void AConcealedPlayerWhoLeavesIsRemovedAndForgotten()
        {
            Join();
            Deliver(2, concealed: true, _enemyId, Vector3.Zero);

            DeliverOwnOnly(3);

            Assert.That(_removed, Is.EqualTo(new[] { _enemyId }));
            Assert.That(_match.IsConcealed(_enemyId), Is.False);
            Assert.That(_match.RemotePlayerIds, Is.Empty);
        }

        private void Join()
        {
            _client.Connect("127.0.0.1", 7777);
            _transport.Connected();
            _client.Poll();
            _transport.Deliver(new ConnectAcceptedMessage
            {
                PlayerId = _playerId,
                ServerVersion = "0.2.0",
                TickRate = 60,
                ServerTick = 0,
                Map = "NetArena01",
                MaxHealth = 100,
                Motor = NetMotorSettings.Default,
                KillPlaneY = ConnectAcceptedMessage.DefaultKillPlaneY,
            });
            _client.Poll();
            var world = new ClientWorld(
                "Test",
                new WorldMap(new[]
                {
                    WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                }),
                NetMapLadders.None);
            DeliverOwnOnly(1);
            _match = new ClientMatch(_client, world.CreateSimulation(_client.Motor, _client.TickRate, _client.KillPlaneY));
            _match.PlayerAdded += _added.Add;
            _match.PlayerRemoved += _removed.Add;
            Assert.That(_match.IsPredicting, Is.True);
        }

        private void DeliverOwnOnly(int tick)
        {
            _transport.Deliver(Snapshot(tick));
            _client.Poll();
        }

        private void Deliver(int tick, bool concealed, int otherId, Vector3 position)
        {
            var snapshot = Snapshot(tick);
            ref var other = ref snapshot.Players.Add();
            other.PlayerId = otherId;
            other.Team = Team.Bravo;
            other.Health = 100;
            other.Position = position;
            other.IsGrounded = !concealed;
            other.WeaponId = concealed ? "" : "rifle";
            other.Concealed = concealed;
            _transport.Deliver(snapshot);
            _client.Poll();
        }

        private static SnapshotMessage Snapshot(int tick)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            snapshot.HasOwner = true;
            snapshot.Owner.LastProcessedInputTick = -1;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = _playerId;
            own.Team = Team.Alpha;
            own.Position = _spawn;
            own.Health = 100;
            own.IsGrounded = true;
            own.WeaponId = "";
            return snapshot;
        }
    }
}
