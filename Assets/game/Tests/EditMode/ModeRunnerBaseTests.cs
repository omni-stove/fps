using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// ModeRunnerBase が陣営の参加者をモードに入れ、死亡を報告し、自陣営の出現点に戻し、状態を <see cref="GameSignals.ModeStatus"/> に出すことを確かめる。
    /// bot (EnemyAgent) は NavMesh が要るので、ここでは Damageable と SideMember だけの参加者で確かめる。
    /// </summary>
    public sealed class ModeRunnerBaseTests
    {
        private const float RespawnSeconds = 1f;

        private readonly List<Object> _objects = new List<Object>();
        private RecordingModeRunner _runner;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _runner = Track(new GameObject("Runner")).AddComponent<RecordingModeRunner>();
            _runner.Factory = () => TeamDeathmatchRunner.CreateSession(3, 4, 60f, 2, RespawnSeconds);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            GameSignals.ResetAll();
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
        public void BeginPutsTheLocalPlayerFirstThenOrdersBySideAndName()
        {
            var player = Member("Player", 0, Vector3.zero);
            var c = Member("Bot-C", 2, new Vector3(0f, 0f, 20f));
            var b = Member("Bot-B", 1, new Vector3(0f, 0f, 10f));
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 12f));

            Begin(new[] { c, b, a, player }, Array.Empty<SideSpawnPoint>(), player);

            Assert.That(_runner.Members, Is.EqualTo(new[] { player, a, b, c }));
            Assert.That(_runner.IdOf(player), Is.EqualTo(ModeRunnerBase.LocalPlayerId));
            Assert.That(_runner.IdOf(c), Is.EqualTo(3));
            Assert.That(_runner.MemberOf(2), Is.SameAs(b));
            Assert.That(_runner.LocalPlayer, Is.SameAs(player));
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_runner.Session.GetSide(3), Is.EqualTo(new Side(2)));
        }

        [Test]
        public void AKillScoresForTheKillerSideAndRespawnsTheVictimAtItsSpawnPoint()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var spawn = SpawnPoint(1, new Vector3(30f, 0f, 30f));
            Begin(new[] { player, bot }, new[] { spawn }, player);

            KillBy(player, bot);

            var session = _runner.Session;
            Assert.That(session.GetScore(Side.Alpha), Is.EqualTo(1d));
            Assert.That(session.GetKills(0), Is.EqualTo(1));
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.IsAwaitingRespawn(1), Is.True);
            Assert.That(bot.IsAlive, Is.False);

            _runner.Tick(RespawnSeconds / 2f);
            Assert.That(bot.IsAlive, Is.False, "the respawn waits for the respawn seconds");

            _runner.Tick(RespawnSeconds);

            Assert.That(bot.IsAlive, Is.True);
            Assert.That(bot.Damageable.Health.Current, Is.EqualTo(bot.Damageable.Health.Max));
            Assert.That(bot.transform.position, Is.EqualTo(spawn.Position));
            Assert.That(_runner.Events, Is.EqualTo(new[] { "kill Player Bot-A", "spawn Bot-A" }));
        }

        [Test]
        public void ADeathWithoutAKillerOnlyCountsTheDeath()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            Begin(new[] { player, bot }, Array.Empty<SideSpawnPoint>(), player);

            bot.Damageable.Kill();

            var session = _runner.Session;
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(session.GetScore(Side.Bravo), Is.Zero);
            Assert.That(_runner.Events, Is.EqualTo(new[] { "kill - Bot-A" }));
        }

        [Test]
        public void AKillWithinTheSameSideIsNotScored()
        {
            var player = Member("Player", 0, Vector3.zero);
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 1, new Vector3(0f, 0f, 12f));
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            KillBy(a, b);

            var session = _runner.Session;
            Assert.That(session.GetScore(Side.Bravo), Is.Zero);
            Assert.That(session.GetKills(1), Is.Zero);
            Assert.That(session.GetDeaths(2), Is.EqualTo(1));
            Assert.That(_runner.Events, Is.EqualTo(new[] { "kill Bot-A Bot-B" }));
        }

        [Test]
        public void TheRespawnPicksTheOwnSidePointFarthestFromLivingEnemies()
        {
            var player = Member("Player", 0, new Vector3(8f, 0f, 0f));
            var bot = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var near = SpawnPoint(1, new Vector3(10f, 0f, 0f));
            var far = SpawnPoint(1, new Vector3(-10f, 0f, 0f));
            var otherSide = SpawnPoint(0, new Vector3(-40f, 0f, 0f));
            Begin(new[] { player, bot }, new[] { near, otherSide, far }, player);

            KillBy(player, bot);
            _runner.Tick(RespawnSeconds);

            Assert.That(bot.transform.position, Is.EqualTo(far.Position));
        }

        [Test]
        public void WithoutAnOwnSidePointTheVictimReturnsWhereItFell()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-A", 1, new Vector3(3f, 0f, 10f));
            Begin(new[] { player, bot }, new[] { SpawnPoint(0, new Vector3(-40f, 0f, 0f)) }, player);

            KillBy(player, bot);
            _runner.Tick(RespawnSeconds);

            Assert.That(bot.IsAlive, Is.True);
            Assert.That(bot.transform.position, Is.EqualTo(new Vector3(3f, 0f, 10f)));
        }

        [Test]
        public void ReachingTheScoreLimitEndsTheModeAndNamesTheWinner()
        {
            var player = Member("Player", 0, Vector3.zero);
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            KillBy(player, a);
            KillBy(player, b);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.Events, Does.Contain("ended 0"));

            _runner.Tick(RespawnSeconds * 2f);
            Assert.That(a.IsAlive, Is.False, "nobody respawns after the end");
        }

        [Test]
        public void BeginRejectsASideOutsideTheLayout()
        {
            var player = Member("Player", 0, Vector3.zero);
            var stray = Member("Bot-Stray", 5, new Vector3(0f, 0f, 10f));
            var session = TeamDeathmatchRunner.CreateSession(3, 4, 60f, 2, RespawnSeconds);

            Assert.Throws<ArgumentException>(() => _runner.Begin(session, new[] { player, stray }, Array.Empty<SideSpawnPoint>(), player));
            Assert.That(_runner.IsStarted, Is.False);

            stray.Damageable.Kill();
            Assert.That(_runner.Events, Is.Empty, "nothing was subscribed");
        }

        [Test]
        public void BeginRejectsAStartedSessionAndMembersWithoutHealth()
        {
            var player = Member("Player", 0, Vector3.zero);
            var started = TeamDeathmatchRunner.CreateSession(3, 4, 60f, 2, RespawnSeconds);
            started.Start();
            Assert.Throws<ArgumentException>(() => _runner.Begin(started, new[] { player }, Array.Empty<SideSpawnPoint>()));

            var noHealth = Track(new GameObject("NoHealth")).AddComponent<SideMember>();
            var session = TeamDeathmatchRunner.CreateSession(3, 4, 60f, 2, RespawnSeconds);
            Assert.Throws<ArgumentException>(() => _runner.Begin(session, new[] { player, noHealth }, Array.Empty<SideSpawnPoint>()));
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void StopLeavesTheMode()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            Begin(new[] { player, bot }, Array.Empty<SideSpawnPoint>(), player);
            var session = _runner.Session;

            _runner.Stop();
            KillBy(player, bot);

            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(session.GetDeaths(1), Is.Zero);
            Assert.That(_runner.Events, Is.Empty);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null, "stopping clears the status");
            Assert.That(_runner.Status, Is.Null);
        }

        [Test]
        public void StopKeepsAStatusPublishedByAnotherRunner()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            Begin(new[] { player, bot }, Array.Empty<SideSpawnPoint>(), player);
            var other = new ModeStatus(null, null, null, null, false, null, null);
            GameSignals.ModeStatus.Publish(other);

            _runner.Stop();

            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(other));
        }

        [Test]
        public void BeginAndTickPublishTheTypedStatus()
        {
            var player = Member("Player", 1, Vector3.zero);
            var a = Member("Bot-A", 0, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            var published = new List<ModeStatus>();
            GameSignals.ModeStatus.Changed += published.Add;

            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            Assert.That(published, Has.Count.EqualTo(1), "Begin publishes once");
            var status = GameSignals.ModeStatus.Value;
            Assert.That(status, Is.SameAs(published[0]));
            Assert.That(status, Is.SameAs(_runner.Status));
            Assert.That(status.RemainingSeconds, Is.EqualTo(60f));
            Assert.That(status.OwnSide, Is.EqualTo((Side?)new Side(1)));
            Assert.That(status.RespawnSeconds, Is.Null);
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
            Assert.That(status.Detail, Is.Null);
            Assert.That(status.SideScores, Is.EquivalentTo(new[] { new SideScore(Side.Alpha, 0d), new SideScore(Side.Bravo, 0d), new SideScore(new Side(2), 0d) }));

            KillBy(b, player);
            _runner.Tick(0.25f);

            Assert.That(published, Has.Count.EqualTo(2), "a tick publishes once");
            status = GameSignals.ModeStatus.Value;
            Assert.That(status.RespawnSeconds, Is.EqualTo(RespawnSeconds - 0.25f).Within(1e-4f));
            Assert.That(status.SideScores, Has.Member(new SideScore(new Side(2), 1d)));
        }

        [Test]
        public void EndingTheMatchPublishesTheWinner()
        {
            var player = Member("Player", 0, Vector3.zero);
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            KillBy(player, a);
            KillBy(player, b);

            var status = GameSignals.ModeStatus.Value;
            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void TheModesDetailIsPublished()
        {
            var player = Member("Player", 0, Vector3.zero);
            var bot = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var detail = new FakeDetail();
            _runner.Detail = detail;

            Begin(new[] { player, bot }, Array.Empty<SideSpawnPoint>(), player);

            Assert.That(GameSignals.ModeStatus.Value.Detail, Is.SameAs(detail));
        }

        [Test]
        public void TryBeginJoinsTheBootstrapPlayerAndHidesTheTargetDummies()
        {
            WithBootstrapWorld(world =>
            {
                Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
                _runner.PlayerSide = new Side(0);

                Assert.That(_runner.HidesTargetDummies, Is.True, "hiding is the default");
                Assert.That(_runner.TryBegin(), Is.True);

                Assert.That(_runner.LocalPlayer, Is.Not.Null);
                Assert.That(_runner.LocalPlayer.gameObject, Is.SameAs(world.Player));
                Assert.That(_runner.LocalPlayer.Side, Is.EqualTo(new Side(0)));
                Assert.That(world.TargetRoot.activeSelf, Is.False, "target dummies are not part of the mode");
            });
        }

        [Test]
        public void TryBeginKeepsTheTargetDummiesWhenAskedTo()
        {
            WithBootstrapWorld(world =>
            {
                Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
                _runner.HidesTargetDummies = false;

                Assert.That(_runner.TryBegin(), Is.True);

                Assert.That(world.TargetRoot.activeSelf, Is.True);
            });
        }

        private void WithBootstrapWorld(Action<BootstrapWorld> test)
        {
            var bootstrap = Track(new GameObject("Bootstrap")).AddComponent<GameBootstrap>();
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            var world = bootstrap.Build();
            try
            {
                test(world);
            }
            finally
            {
                _runner.Stop();

                Object.DestroyImmediate(world.Player);
                Object.DestroyImmediate(world.Light.gameObject);
                Object.DestroyImmediate(world.TargetRoot);
                if (world.Floor != null)
                {
                    Object.DestroyImmediate(world.Floor);
                }
            }
        }

        private void Begin(IReadOnlyList<SideMember> members, IReadOnlyList<SideSpawnPoint> spawns, SideMember localPlayer)
        {
            _runner.Begin(_runner.Factory(), members, spawns, localPlayer);
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

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        private sealed class FakeDetail
        {
        }
    }
}
