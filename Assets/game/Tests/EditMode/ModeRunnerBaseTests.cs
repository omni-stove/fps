using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // bot (EnemyAgent) は NavMesh が要るので、Damageable と SideMember だけの参加者で確かめる。
    public sealed class ModeRunnerBaseTests
    {
        private const float RespawnSeconds = 1f;

        private readonly List<Object> _objects = new List<Object>();
        private RecordingModeRunner _runner;

        private readonly List<MatchResult> _results = new List<MatchResult>();

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            CombatEvents.ClearSubscribers();
            _results.Clear();
            CombatEvents.MatchFinished += _results.Add;
            _runner = Track(new GameObject("Runner")).AddComponent<RecordingModeRunner>();
            _runner.Factory = () => TeamDeathmatchRunner.CreateSession(3, 4, 60f, 2, RespawnSeconds);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            CombatEvents.ClearSubscribers();
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
        public void ABeaconIsUsedFirstAndLosesAUse()
        {
            var (victim, mate, enemy) = AnchorScene();
            var beacon = PlaceBeacon(victim, new Vector3(10f, 0f, 0f), 2);
            var expected = beacon.SpawnPose.position;
            _runner.Rules = GameRules.Default.WithRespawnAnchors(new[] { RespawnAnchor.Beacon, RespawnAnchor.Squad, RespawnAnchor.Base });
            Begin(new[] { victim, mate, enemy }, new[] { SpawnPoint(0, new Vector3(-30f, 0f, 0f)) }, null);

            KillBy(enemy, victim);
            LogAssert.Expect(LogType.Log, ModeLog.Spawn(_runner.IdOf(victim), Side.Alpha, expected.ToNumerics(), RespawnAnchor.Beacon));
            _runner.Tick(RespawnSeconds);

            Assert.That(victim.IsAlive, Is.True);
            Assert.That(Vector3.Distance(victim.transform.position, expected), Is.LessThan(1e-4f));
            Assert.That(beacon.UsesLeft, Is.EqualTo(1), "the respawn took one use");
        }

        [Test]
        public void ABrokenBeaconFallsToTheSquadNearTheSafestMate()
        {
            var (victim, mate, enemy) = AnchorScene();
            var beacon = PlaceBeacon(victim, new Vector3(10f, 0f, 0f), 2);
            _runner.Rules = GameRules.Default.WithRespawnAnchors(new[] { RespawnAnchor.Beacon, RespawnAnchor.Squad, RespawnAnchor.Base });
            Begin(new[] { victim, mate, enemy }, new[] { SpawnPoint(0, new Vector3(-30f, 0f, 0f)) }, null);
            beacon.Deployable.Remove(DeployRemoval.Destroyed);

            KillBy(enemy, victim);
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape("[Mode] spawn player=" + _runner.IdOf(victim) + " side=0 at=") + ".* from=Squad$"));
            _runner.Tick(RespawnSeconds);

            // 味方 (0, 0, -10) のまわり 2 m の 8 か所のうち、敵 (0, 0, 40) からいちばん遠いのは真後ろ。
            Assert.That(victim.IsAlive, Is.True);
            Assert.That(Vector3.Distance(victim.transform.position, new Vector3(0f, 0f, -12f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void WithoutABeaconOrAMateTheVictimReturnsToTheBase()
        {
            var (victim, _, enemy) = AnchorScene();
            var basePoint = SpawnPoint(0, new Vector3(-30f, 0f, 0f));
            _runner.Rules = GameRules.Default.WithRespawnAnchors(new[] { RespawnAnchor.Beacon, RespawnAnchor.Squad, RespawnAnchor.Base });
            Begin(new[] { victim, enemy }, new[] { basePoint }, null);

            KillBy(enemy, victim);
            LogAssert.Expect(LogType.Log, ModeLog.Spawn(_runner.IdOf(victim), Side.Alpha, basePoint.Position.ToNumerics(), RespawnAnchor.Base));
            _runner.Tick(RespawnSeconds);

            Assert.That(victim.transform.position, Is.EqualTo(basePoint.Position));
        }

        [Test]
        public void ARecentlyShotMateIsNotASquadAnchor()
        {
            var (victim, mate, enemy) = AnchorScene();
            var basePoint = SpawnPoint(0, new Vector3(-30f, 0f, 0f));
            _runner.Rules = GameRules.Default.WithRespawnAnchors(new[] { RespawnAnchor.Squad, RespawnAnchor.Base });
            Begin(new[] { victim, mate, enemy }, new[] { basePoint }, null);

            mate.Damageable.TakeDamage(10);
            KillBy(enemy, victim);
            _runner.Tick(RespawnSeconds);

            Assert.That(SquadSpawnSpec.DefaultDamageCooldownSeconds, Is.GreaterThan(RespawnSeconds));
            Assert.That(victim.transform.position, Is.EqualTo(basePoint.Position));
        }

        [Test]
        public void ARoundResetRespawnGoesToTheBaseAndKeepsTheBeacon()
        {
            var (victim, mate, enemy) = AnchorScene();
            var beacon = PlaceBeacon(victim, new Vector3(10f, 0f, 0f), 2);
            var basePoint = SpawnPoint(0, new Vector3(-30f, 0f, 0f));
            _runner.Rules = GameRules.Default.WithRespawnAnchors(new[] { RespawnAnchor.Beacon, RespawnAnchor.Base });
            Begin(new[] { victim, mate, enemy }, new[] { basePoint }, null);

            LogAssert.Expect(LogType.Log, ModeLog.Spawn(_runner.IdOf(victim), Side.Alpha, basePoint.Position.ToNumerics()));
            _runner.Respawn(victim);

            Assert.That(victim.transform.position, Is.EqualTo(basePoint.Position));
            Assert.That(beacon.UsesLeft, Is.EqualTo(2));
        }

        [Test]
        public void WithoutGivenRulesTheDefaultRespawnsAtTheBase()
        {
            Assert.That(_runner.Rules, Is.SameAs(GameRules.Default));

            var (victim, _, enemy) = AnchorScene();
            var beacon = PlaceBeacon(victim, new Vector3(10f, 0f, 0f), 2);
            var basePoint = SpawnPoint(0, new Vector3(-30f, 0f, 0f));
            Begin(new[] { victim, enemy }, new[] { basePoint }, null);

            KillBy(enemy, victim);
            _runner.Tick(RespawnSeconds);

            Assert.That(victim.transform.position, Is.EqualTo(basePoint.Position));
            Assert.That(beacon.UsesLeft, Is.EqualTo(2));
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
        public void WinningRaisesTheLocalPlayersResultOnce()
        {
            var player = Member("Player", 0, Vector3.zero);
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            KillBy(player, a);
            Assert.That(_results, Is.Empty, "nothing is raised before the end");
            KillBy(player, b);

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Won, 2, 0, 2) }));
        }

        [Test]
        public void LosingRaisesTheLocalPlayersDeathsAndOwnSideScore()
        {
            var player = Member("Player", 0, Vector3.zero);
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            KillBy(a, player);
            KillBy(a, b);

            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Lost, 0, 1, 0) }));
        }

        [Test]
        public void ATiedTimeUpRaisesADraw()
        {
            var player = Member("Player", 0, Vector3.zero);
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);

            KillBy(player, a);
            KillBy(b, player);
            _runner.Tick(60f);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.Null);
            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Draw, 1, 1, 1) }));
        }

        [Test]
        public void EndingTheMatchAgainRaisesNothing()
        {
            var player = Member("Player", 0, Vector3.zero);
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            Begin(new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player);
            KillBy(player, a);
            KillBy(player, b);

            _runner.EndMatchFor(Side.Bravo);
            _runner.EndMatchFor(null);
            _runner.Tick(60f);

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Won, 2, 0, 2) }));
        }

        [Test]
        public void EndMatchCalledByTheModeDecidesTheOutcomeFromTheWinner()
        {
            var player = Member("Player", 1, Vector3.zero);
            var bot = Member("Bot-A", 0, new Vector3(0f, 0f, 10f));
            Begin(new[] { player, bot }, Array.Empty<SideSpawnPoint>(), player);

            _runner.EndMatchFor(new Side(1));

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Won, 0, 0, 0) }));
        }

        [Test]
        public void AMatchWithoutALocalPlayerRaisesNothing()
        {
            var a = Member("Bot-A", 1, new Vector3(0f, 0f, 10f));
            var b = Member("Bot-B", 2, new Vector3(0f, 0f, 12f));
            var c = Member("Bot-C", 2, new Vector3(0f, 0f, 14f));
            _runner.Begin(_runner.Factory(), new[] { a, b, c }, Array.Empty<SideSpawnPoint>());

            KillBy(a, b);
            KillBy(a, c);

            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_results, Is.Empty);
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

        // 味方の近くへ戻すには足元に床が要る。倒される人と味方は Alpha、敵は Bravo で、敵は味方から 50 m 離れている。
        private (SideMember Victim, SideMember Mate, SideMember Enemy) AnchorScene()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(200f, 1f, 200f);

            var victim = Member("Bot-V", 0, new Vector3(5f, 0f, 5f));
            var mate = Member("Bot-M", 0, new Vector3(0f, 0f, -10f));
            var enemy = Member("Bot-E", 1, new Vector3(0f, 0f, 40f));
            Physics.SyncTransforms();
            return (victim, mate, enemy);
        }

        private SpawnBeacon PlaceBeacon(SideMember owner, Vector3 position, int uses)
        {
            var definition = ScriptableObject.CreateInstance<BeaconDeployDefinition>();
            definition.name = "DeployBeacon";
            definition.Id = "beacon";
            definition.Uses = uses;
            _objects.Add(definition);

            var spec = definition.ToDeploySpec();
            var deployable = definition.CreateDeployed(new Pose(position, Quaternion.identity), spec);
            Track(deployable.gameObject);
            deployable.Initialize(owner.gameObject, owner.name, definition.Id, 1, owner.Side, spec.MaxHealth);
            var beacon = deployable.GetComponent<SpawnBeacon>();
            beacon.Arm(definition.ToBeaconSpec());
            Physics.SyncTransforms();
            return beacon;
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
