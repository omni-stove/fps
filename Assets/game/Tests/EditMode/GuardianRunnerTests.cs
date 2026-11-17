using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // リーダーは乱数で選ばれるので、誰がリーダーかは GuardianRunner.Leaders から引いて確かめる。
    public sealed class GuardianRunnerTests
    {
        private const float RespawnSeconds = 1f;

        private readonly List<Object> _objects = new List<Object>();
        private GuardianRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _runner = Track(new GameObject("Runner")).AddComponent<GuardianRunner>();
            _runner.RespawnSeconds = RespawnSeconds;
            _runner.TimeLimitSeconds = 60f;
            _runner.LeaderSeed = 7;
        }

        [TearDown]
        public void TearDown()
        {
            _runner.ClearMarkers();
            _runner.Stop();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void EachTeamGetsALeaderWithAMarkerAboveTheHead()
        {
            var player = Member("Player", 0, Vector3.zero);
            var ally = Member("Bot-0-1", 0, new Vector3(4f, 0f, 0f));
            var enemyA = Member("Bot-1-1", 1, new Vector3(0f, 0f, 30f));
            var enemyB = Member("Bot-1-2", 1, new Vector3(4f, 0f, 30f));

            _runner.BeginGuardian(new[] { player, ally, enemyA, enemyB }, new SideSpawnPoint[0], player);

            var leaders = _runner.Leaders;
            Assert.That(leaders.GetLeader(Side.Alpha), Is.Not.Null);
            Assert.That(leaders.GetLeader(Side.Bravo), Is.Not.Null);

            var enemyLeader = _runner.MemberOf(leaders.GetLeader(Side.Bravo).Value);
            var marker = _runner.MarkerOf(leaders.GetLeader(Side.Bravo).Value);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Target, Is.SameAs(enemyLeader));
            Assert.That(marker.IsShown, Is.True);
            Assert.That(marker.GetComponent<Collider>() == null, Is.True, "the marker does not block shots");
            Assert.That(marker.transform.position.y, Is.GreaterThan(enemyLeader.transform.position.y + 1f));
            Assert.That(marker.transform.position.x, Is.EqualTo(enemyLeader.transform.position.x).Within(1e-4f));
            Assert.That(marker.Renderer.sharedMaterial.color, Is.EqualTo(GuardianLeaderMarker.ColorOf(Side.Bravo)).Using(ColorEqualityComparer.Instance));

            var ownLeaderId = leaders.GetLeader(Side.Alpha).Value;
            if (ownLeaderId == ModeRunnerBase.LocalPlayerId)
            {
                Assert.That(_runner.MarkerOf(ownLeaderId), Is.Null, "no marker floats over the own camera");
            }
            else
            {
                Assert.That(_runner.MarkerOf(ownLeaderId), Is.Not.Null);
            }
        }

        [Test]
        public void TheEnemyStopsComingBackOnceTheirLeaderFallsAndWipingThemOutWins()
        {
            var player = Member("Player", 0, Vector3.zero);
            var enemyA = Member("Bot-1-1", 1, new Vector3(0f, 0f, 30f));
            var enemyB = Member("Bot-1-2", 1, new Vector3(4f, 0f, 30f));
            _runner.BeginGuardian(new[] { player, enemyA, enemyB }, new[] { SpawnPoint(1, new Vector3(0f, 0f, 40f)) }, player);

            var leaderId = _runner.Leaders.GetLeader(Side.Bravo).Value;
            var leader = _runner.MemberOf(leaderId);
            var other = leader == enemyA ? enemyB : enemyA;

            KillBy(player, other);
            _runner.Tick(RespawnSeconds);
            Assert.That(other.IsAlive, Is.True, "the leader is alive, so the teammate comes back");

            KillBy(player, leader);
            Assert.That(_runner.MarkerOf(leaderId).IsShown, Is.False, "the marker goes away with the leader");
            Assert.That(Detail().Leaders.HasLeaderFallen(Side.Bravo), Is.True, "the status carries the fallen leader");

            KillBy(player, other);
            _runner.Tick(RespawnSeconds * 2f);

            Assert.That(other.IsAlive, Is.False);
            Assert.That(leader.IsAlive, Is.False);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void WhenTheOwnLeaderFallsThePlayerCannotComeBack()
        {
            var player = Member("Player", 0, Vector3.zero);
            var allyA = Member("Bot-0-1", 0, new Vector3(4f, 0f, 0f));
            var allyB = Member("Bot-0-2", 0, new Vector3(-4f, 0f, 0f));
            var enemy = Member("Bot-1-1", 1, new Vector3(0f, 0f, 30f));
            _runner.BeginGuardian(new[] { player, allyA, allyB, enemy }, new[] { SpawnPoint(0, new Vector3(0f, 0f, -5f)) }, player);

            var ownLeader = _runner.MemberOf(_runner.Leaders.GetLeader(Side.Alpha).Value);
            KillBy(enemy, ownLeader);
            if (player.IsAlive)
            {
                KillBy(enemy, player);
            }

            _runner.Tick(RespawnSeconds * 2f);

            Assert.That(player.IsAlive, Is.False);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing), "some allies are still standing");
            Assert.That(_runner.Session.IsEliminated(ModeRunnerBase.LocalPlayerId), Is.True);
            var detail = Detail();
            Assert.That(detail.LocalPlayerId, Is.EqualTo((int?)ModeRunnerBase.LocalPlayerId));
            Assert.That(detail.IsLocalPlayerEliminated, Is.True);
            Assert.That(detail.Leaders, Is.SameAs(_runner.Leaders));
            Assert.That(detail.Leaders.HasLeaderFallen(Side.Alpha), Is.True);
            Assert.That(detail.Leaders.HasLeaderFallen(Side.Bravo), Is.False);
            Assert.That(_runner.Status.OwnSide, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.Status.RespawnSeconds, Is.Null, "no respawn countdown once the leader is down");
        }

        [Test]
        public void ThePlayerIsToldWhenTheyAreTheLeader()
        {
            var player = Member("Player", 0, Vector3.zero);
            var enemy = Member("Bot-1-1", 1, new Vector3(0f, 0f, 30f));

            _runner.BeginGuardian(new[] { player, enemy }, new SideSpawnPoint[0], player);

            Assert.That(_runner.Leaders.GetLeader(Side.Alpha), Is.EqualTo((int?)ModeRunnerBase.LocalPlayerId), "the player is the only Alpha");
            Assert.That(Detail().LocalPlayerId, Is.EqualTo((int?)ModeRunnerBase.LocalPlayerId));
            Assert.That(Detail().IsLocalPlayerEliminated, Is.False);
            Assert.That(_runner.MarkerOf(ModeRunnerBase.LocalPlayerId), Is.Null);
        }

        [Test]
        public void MarkersCanBeTurnedOff()
        {
            var player = Member("Player", 0, Vector3.zero);
            var enemy = Member("Bot-1-1", 1, new Vector3(0f, 0f, 30f));
            _runner.ShowsLeaderMarkers = false;

            _runner.BeginGuardian(new[] { player, enemy }, new SideSpawnPoint[0], player);

            Assert.That(_runner.Leaders.GetLeader(Side.Bravo), Is.EqualTo((int?)1));
            Assert.That(_runner.MarkerOf(1), Is.Null);
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private SideSpawnPoint SpawnPoint(int side, Vector3 position)
        {
            var point = Track(new GameObject("Spawn-" + side)).AddComponent<SideSpawnPoint>();
            point.transform.position = position;
            point.Side = new Side(side);
            return point;
        }

        private GuardianStatus Detail()
        {
            Assert.That(_runner.Status, Is.Not.Null);
            Assert.That(_runner.Status.Detail, Is.InstanceOf<GuardianStatus>());
            return (GuardianStatus)_runner.Status.Detail;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
