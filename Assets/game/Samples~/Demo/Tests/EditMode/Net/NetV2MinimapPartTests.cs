using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.Net.Tests
{
    public sealed class NetV2MinimapPartTests
    {
        private const int _localId = 3;
        private const int _allyId = 5;
        private const int _enemyId = 7;
        private const float _interval = 0.5f;

        private readonly Dictionary<int, Vector3> _pointPositions = new Dictionary<int, Vector3>();
        private SharedPingMarkers _pings;
        private NetV2MinimapPart _part;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Minimap.Clear();
            _pointPositions.Clear();
            _pings = new SharedPingMarkers();
            _part = new NetV2MinimapPart(() => _localId, _pings, new MinimapSpec(updateIntervalSeconds: _interval), PointPosition);
            _changes = 0;
            GameSignals.Minimap.Changed += CountChange;
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.Minimap.Changed -= CountChange;
            _part.Dispose();
            GameSignals.Minimap.Clear();
        }

        [Test]
        public void OnlyAlliesAndPingsAreShownAndEnemiesAreNot()
        {
            var snapshot = Snapshot();
            Assert.That(_pings.Place(11, PingKind.Enemy, new Vector3(30f, 0f, 40f), false, 5f, 0f), Is.True);

            _part.Apply(snapshot);
            _part.Tick(0f);

            var status = GameSignals.Minimap.Value;
            Assert.That(status.HasValue, Is.True);
            Assert.That(status.Value.SelfPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(status.Value.SelfHeadingDegrees, Is.EqualTo(90f).Within(0.001f));
            var contacts = status.Value.Contacts;
            Assert.That(contacts.Count, Is.EqualTo(2));
            Assert.That(contacts[0].Kind, Is.EqualTo(MinimapContactKind.Ally));
            Assert.That(contacts[0].Position, Is.EqualTo(new Vector3(10f, 0f, -5f)));
            Assert.That(contacts[0].HeadingDegrees, Is.EqualTo(180f).Within(0.001f));
            Assert.That(contacts[0].Team, Is.EqualTo((Team?)Team.Bravo));
            Assert.That(contacts[1], Is.EqualTo(new MinimapContact(MinimapContactKind.Ping, new Vector3(30f, 0f, 40f), 0f, Team.Bravo)));
            foreach (var contact in contacts)
            {
                Assert.That(contact.Kind, Is.Not.EqualTo(MinimapContactKind.Enemy));
                Assert.That(contact.Position, Is.Not.EqualTo(new Vector3(-20f, 0f, 8f)), "the enemy's position is not on the map");
            }
        }

        [Test]
        public void WhenTheOtherPlayerSwitchesToTheEnemyTeamTheyLeaveTheMap()
        {
            var snapshot = Snapshot();
            _part.Apply(snapshot);
            _part.Tick(0f);
            Assert.That(CountOf(MinimapContactKind.Ally), Is.EqualTo(1));

            snapshot.Players[1].Team = Team.Alpha;
            _part.Apply(snapshot);
            _part.Tick(_interval);

            Assert.That(CountOf(MinimapContactKind.Ally), Is.EqualTo(0));
            Assert.That(GameSignals.Minimap.Value.Value.Contacts, Is.Empty);
        }

        [Test]
        public void ADownAllyIsNotShown()
        {
            var snapshot = Snapshot();
            snapshot.Players[1].Health = 0;

            _part.Apply(snapshot);
            _part.Tick(0f);

            Assert.That(CountOf(MinimapContactKind.Ally), Is.EqualTo(0));
        }

        [Test]
        public void ControlPointsAreShownAtTheirMapPlaceWithTheirOwner()
        {
            var snapshot = Snapshot();
            AddPoint(snapshot, 0, Team.Alpha);
            AddPoint(snapshot, 1, null);
            AddPoint(snapshot, 2, Team.Bravo);
            _pointPositions[0] = new Vector3(5f, 0f, 5f);
            _pointPositions[1] = new Vector3(-5f, 0f, 5f);

            _part.Apply(snapshot);
            _part.Tick(0f);

            var contacts = GameSignals.Minimap.Value.Value.Contacts;
            Assert.That(contacts.Count, Is.EqualTo(3), "the ally and the two points the map knows");
            Assert.That(contacts[1], Is.EqualTo(new MinimapContact(MinimapContactKind.Objective, new Vector3(5f, 0f, 5f), 0f, Team.Alpha)));
            Assert.That(contacts[2], Is.EqualTo(new MinimapContact(MinimapContactKind.Objective, new Vector3(-5f, 0f, 5f), 0f, null)));
        }

        [Test]
        public void ItPublishesOncePerIntervalAndOnlyWhenTheStatusChanges()
        {
            var snapshot = Snapshot();
            _part.Tick(_interval);
            Assert.That(_changes, Is.EqualTo(0), "nothing is published before a snapshot arrives");

            _part.Apply(snapshot);
            _part.Tick(0f);
            Assert.That(_changes, Is.EqualTo(1));

            _part.Apply(snapshot);
            _part.Tick(_interval);
            Assert.That(_changes, Is.EqualTo(1), "the same status is not published again");

            snapshot.Players[0].Position = new Vector3(4f, 2f, 3f);
            _part.Apply(snapshot);
            _part.Tick(_interval * 0.5f);
            Assert.That(_changes, Is.EqualTo(1), "a change waits for the interval");
            Assert.That(GameSignals.Minimap.Value.Value.SelfPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));

            _part.Tick(_interval * 0.5f);
            Assert.That(_changes, Is.EqualTo(2));
            Assert.That(GameSignals.Minimap.Value.Value.SelfPosition, Is.EqualTo(new Vector3(4f, 2f, 3f)));
        }

        [Test]
        public void APingPlacedBetweenSnapshotsIsShownOnTheNextInterval()
        {
            _part.Apply(Snapshot());
            _part.Tick(0f);
            Assert.That(CountOf(MinimapContactKind.Ping), Is.EqualTo(0));

            _pings.Place(12, PingKind.Location, new Vector3(0f, 0f, 9f), false, 5f, 0f);
            _part.Tick(_interval);

            Assert.That(CountOf(MinimapContactKind.Ping), Is.EqualTo(1));
        }

        [Test]
        public void TheMapIsClearedWhenTheLocalPlayerIsNotInTheSnapshot()
        {
            var snapshot = Snapshot();
            _part.Apply(snapshot);
            _part.Tick(0f);
            Assert.That(GameSignals.Minimap.HasValue, Is.True);

            snapshot.Players.Clear();
            _part.Apply(snapshot);
            _part.Tick(_interval);

            Assert.That(GameSignals.Minimap.HasValue, Is.False);
        }

        [Test]
        public void ABrokenPoseFromTheServerSkipsOnlyThatPlayer()
        {
            var snapshot = Snapshot();
            ref var broken = ref snapshot.Players.Add();
            broken.PlayerId = 9;
            broken.Team = Team.Bravo;
            broken.Health = 100;
            broken.Position = new Vector3(float.NaN, 0f, 0f);

            _part.Apply(snapshot);
            _part.Tick(0f);

            Assert.That(CountOf(MinimapContactKind.Ally), Is.EqualTo(1));
        }

        [Test]
        public void DisposeClearsOnlyTheStatusThisPartPublished()
        {
            _part.Apply(Snapshot());
            _part.Tick(0f);

            _part.Dispose();
            Assert.That(GameSignals.Minimap.HasValue, Is.False);

            var other = new MinimapStatus(Vector3.Zero, 0f, null);
            GameSignals.Minimap.Publish(other);
            _part.Dispose();
            _part.Apply(Snapshot());
            _part.Tick(_interval);
            Assert.That(GameSignals.Minimap.Value, Is.EqualTo((MinimapStatus?)other));
        }

        [Test]
        public void TheConstructorRejectsMissingArguments()
        {
            var spec = MinimapSpec.Default;
            Assert.Throws<ArgumentNullException>(() => new NetV2MinimapPart(null, _pings, spec, PointPosition));
            Assert.Throws<ArgumentNullException>(() => new NetV2MinimapPart(() => _localId, null, spec, PointPosition));
            Assert.Throws<ArgumentNullException>(() => new NetV2MinimapPart(() => _localId, _pings, null, PointPosition));
            Assert.Throws<ArgumentNullException>(() => new NetV2MinimapPart(() => _localId, _pings, spec, null));
            Assert.That(_part.Handle(new PingRemovedMessage()), Is.False, "pings stay with ClientPings");
        }

        private void CountChange(MinimapStatus? status)
        {
            _changes++;
        }

        private Vector3? PointPosition(int index)
        {
            return _pointPositions.TryGetValue(index, out var position) ? position : (Vector3?)null;
        }

        private static int CountOf(MinimapContactKind kind)
        {
            var count = 0;
            foreach (var contact in GameSignals.Minimap.Value.Value.Contacts)
            {
                if (contact.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static SnapshotMessage Snapshot()
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Playing;
            ref var local = ref snapshot.Players.Add();
            local.PlayerId = _localId;
            local.Team = Team.Bravo;
            local.Health = 100;
            local.Position = new Vector3(1f, 2f, 3f);
            local.Yaw = MathF.PI / 2f;
            ref var ally = ref snapshot.Players.Add();
            ally.PlayerId = _allyId;
            ally.Team = Team.Bravo;
            ally.Health = 60;
            ally.Position = new Vector3(10f, 0f, -5f);
            ally.Yaw = MathF.PI;
            ref var enemy = ref snapshot.Players.Add();
            enemy.PlayerId = _enemyId;
            enemy.Team = Team.Alpha;
            enemy.Health = 100;
            enemy.Position = new Vector3(-20f, 0f, 8f);
            return snapshot;
        }

        private static void AddPoint(SnapshotMessage snapshot, int index, Team? owner)
        {
            ref var point = ref snapshot.ControlPoints.Add();
            point.Index = index;
            point.HasOwner = owner.HasValue;
            point.Owner = owner ?? Team.Alpha;
        }
    }
}
