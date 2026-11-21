using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    // 相手の体は remote で渡し、場に残った NetV2Client に左右されないようにする
    public sealed class NetV2GuardianLeaderPartTests
    {
        private const int _localId = 1;
        private const int _enemyId = 2;
        private const int _allyId = 3;
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _logs = new List<string>();
        private readonly Dictionary<int, NetV2RemotePlayer> _remotes = new Dictionary<int, NetV2RemotePlayer>();
        private int _ownId;
        private int _tick;
        private NetV2GuardianLeaderPart _part;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _remotes.Clear();
            _ownId = _localId;
            _tick = 0;
            _part = new NetV2GuardianLeaderPart(() => _ownId, null, id => _remotes.TryGetValue(id, out var remote) ? remote : null, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            var root = GameObject.Find(NetV2GuardianLeaderPart.RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RefusesMissingPieces()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2GuardianLeaderPart(null));
            Assert.Throws<ArgumentNullException>(() => NetV2GuardianLeaderPart.Create(null));
        }

        [Test]
        public void TheStandingEnemyLeaderGetsAMarkerAboveTheBodyButTheLocalLeaderDoesNot()
        {
            var enemy = Remote(_enemyId, new Vector3(4f, 0f, 2f));

            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId));

            Assert.That(_part.Status, Is.Not.Null);
            Assert.That(_part.MarkerCount, Is.EqualTo(1), "the local leader is told by the HUD, not by a marker over the own head");
            Assert.That(_part.MarkerOf(_localId), Is.Null);
            var marker = _part.MarkerOf(_enemyId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Anchor, Is.SameAs(enemy.transform));
            Assert.That(marker.Target, Is.Null, "a net body has no SideMember");
            Assert.That(marker.Side, Is.EqualTo(Side.Alpha));
            Assert.That(marker.IsPlaceholder, Is.True);
            Assert.That(marker.IsShown, Is.True);
            Assert.That(marker.name, Is.EqualTo(GuardianLeaderMarker.ObjectNamePrefix + enemy.name));
            Assert.That(marker.transform.parent.name, Is.EqualTo(NetV2GuardianLeaderPart.RootName));
            Assert.That(marker.ViewerTeam, Is.EqualTo((Team?)Team.Bravo), "coloured as the local player sees it");
            Assert.That(marker.Renderer.sharedMaterial.color,
                Is.EqualTo(GuardianLeaderMarker.ColorOf(Side.Alpha, Team.Bravo)).Using(ColorEqualityComparer.Instance));

            var body = BodyBounds(enemy);
            var expectedY = body.max.y + GuardianLeaderMarker.DefaultHeightAboveHead + (GuardianLeaderMarker.DefaultSize * 0.5f);
            Assert.That(marker.transform.position.x, Is.EqualTo(body.center.x).Within(0.001f));
            Assert.That(marker.transform.position.z, Is.EqualTo(body.center.z).Within(0.001f));
            Assert.That(marker.transform.position.y, Is.EqualTo(expectedY).Within(0.001f), "above the top of the body, not of the root");
            Assert.That(_logs, Is.EqualTo(new[] { ClientGuardianLog.MarkerShown(_enemyId, Side.Alpha) }));
        }

        [Test]
        public void AnAllyLeaderGetsAMarkerInTheOwnTeamsColour()
        {
            Remote(_allyId, Vector3.zero);

            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _allyId));

            var marker = _part.MarkerOf(_allyId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Side, Is.EqualTo(Side.Bravo));
            Assert.That(marker.Renderer.sharedMaterial.color,
                Is.EqualTo(GuardianLeaderMarker.ColorOf(Side.Bravo, Team.Bravo)).Using(ColorEqualityComparer.Instance));
            Assert.That(_part.MarkerOf(_enemyId), Is.Null, "a leader without a body yet has no marker");
        }

        [Test]
        public void AFallenLeaderLosesTheMarker()
        {
            Remote(_enemyId, Vector3.zero);
            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId));
            Assert.That(_part.MarkerOf(_enemyId), Is.Not.Null);

            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId, alphaFallen: true));

            Assert.That(_part.MarkerOf(_enemyId), Is.Null);
            Assert.That(_part.MarkerCount, Is.Zero);
            Assert.That(_logs, Is.EqualTo(new[]
            {
                ClientGuardianLog.MarkerShown(_enemyId, Side.Alpha),
                ClientGuardianLog.MarkerRemoved(_enemyId),
            }));
        }

        [Test]
        public void ABodyThatAppearsAfterTheSnapshotGetsTheMarkerOnTheNextTick()
        {
            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId));
            Assert.That(_part.MarkerOf(_enemyId), Is.Null);

            Remote(_enemyId, Vector3.zero);
            _part.Tick(0.016f);

            Assert.That(_part.MarkerOf(_enemyId), Is.Not.Null);
        }

        [Test]
        public void ARebuiltBodyGetsTheMarkerAgain()
        {
            var first = Remote(_enemyId, Vector3.zero);
            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId));
            var oldMarker = _part.MarkerOf(_enemyId);

            Object.DestroyImmediate(first.gameObject);
            var second = Remote(_enemyId, new Vector3(1f, 0f, 0f));
            _part.Tick(0.016f);

            var marker = _part.MarkerOf(_enemyId);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker, Is.Not.SameAs(oldMarker));
            Assert.That(marker.Anchor, Is.SameAs(second.transform));
            Assert.That(oldMarker == null, Is.True, "the marker of the old body is removed");
        }

        [Test]
        public void AConcealedLeaderDoesNotGiveItsPlaceAwayWithTheMarker()
        {
            var enemy = Remote(_enemyId, Vector3.zero);
            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId));

            enemy.SetConcealed(true);
            _part.Tick(0.016f);
            Assert.That(_part.MarkerOf(_enemyId).IsShown, Is.False);

            enemy.SetConcealed(false);
            _part.Tick(0.016f);
            Assert.That(_part.MarkerOf(_enemyId).IsShown, Is.True);
        }

        [Test]
        public void AnotherModeTakesTheMarkersAway()
        {
            Remote(_enemyId, Vector3.zero);
            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId));

            var other = Snapshot(alphaLeader: _enemyId, bravoLeader: _localId);
            other.Match.Objective = MatchObjective.None;
            _part.Apply(other);

            Assert.That(_part.Status, Is.Null);
            Assert.That(_part.MarkerCount, Is.Zero);
        }

        [Test]
        public void TheLocalIdGivenOnAcceptanceIsReadFromTheNextSnapshot()
        {
            Remote(_enemyId, Vector3.zero);
            Remote(_allyId, Vector3.zero);
            _ownId = 0;
            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _allyId));
            Assert.That(_part.MarkerOf(_enemyId).ViewerTeam, Is.Null, "before acceptance there is no own team");

            _ownId = _allyId;
            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _allyId));

            Assert.That(_part.MarkerOf(_allyId), Is.Null, "the local player is now the Bravo leader");
            Assert.That(_part.MarkerOf(_enemyId).ViewerTeam, Is.EqualTo((Team?)Team.Bravo));
        }

        [Test]
        public void DisposeRemovesTheMarkersAndTheirRootAndIgnoresLaterSnapshots()
        {
            Remote(_enemyId, Vector3.zero);
            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId));
            var marker = _part.MarkerOf(_enemyId);

            _part.Dispose();

            Assert.That(marker == null, Is.True);
            Assert.That(GameObject.Find(NetV2GuardianLeaderPart.RootName), Is.Null);
            Assert.That(_part.Handle(new ChatMessage()), Is.False);

            _part.Apply(Snapshot(alphaLeader: _enemyId, bravoLeader: _localId));
            _part.Tick(0.016f);
            Assert.That(_part.MarkerCount, Is.Zero);
        }

        private NetV2RemotePlayer Remote(int playerId, Vector3 position)
        {
            var remote = NetV2RemotePlayer.Create(playerId, _stance, MotorSpec.Default, null);
            remote.transform.position = position;
            _created.Add(remote.gameObject);
            _remotes[playerId] = remote;
            return remote;
        }

        private static Bounds BodyBounds(NetV2RemotePlayer remote)
        {
            var renderers = remote.Body.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var bodyRenderer in renderers)
            {
                bounds.Encapsulate(bodyRenderer.bounds);
            }

            return bounds;
        }

        private SnapshotMessage Snapshot(int alphaLeader, int bravoLeader, bool alphaFallen = false)
        {
            var snapshot = new SnapshotMessage { Tick = ++_tick };
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Objective = MatchObjective.Guardian;
            AddPlayer(snapshot, _localId, Team.Bravo);
            AddPlayer(snapshot, _allyId, Team.Bravo);
            AddPlayer(snapshot, _enemyId, Team.Alpha);
            AddLeader(snapshot, Team.Alpha, alphaLeader, alphaFallen);
            AddLeader(snapshot, Team.Bravo, bravoLeader, false);
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, Team team)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = team;
            row.Health = 100;
        }

        private static void AddLeader(SnapshotMessage snapshot, Team team, int leader, bool fallen)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = Side.FromTeam(team).Id;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Leader;
            item.Team = team;
            item.HasOwner = true;
            item.OwnerId = leader;
            item.State = fallen ? ObjectiveItemState.Dropped : ObjectiveItemState.Carried;
            item.HasCarrier = !fallen;
            item.CarrierId = fallen ? 0 : leader;
        }
    }
}
