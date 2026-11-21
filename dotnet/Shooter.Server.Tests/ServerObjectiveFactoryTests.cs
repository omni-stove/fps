using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerObjectiveFactoryTests
    {
        private StringWriter _log;
        private FakeHost _host;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _host = new FakeHost(_log);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void NoObjectiveLeavesTheKillLimitAndTimeLimitToTheSession()
        {
            var settings = new NetMatchSettings(scoreLimit: 30, timeLimitSeconds: 600f, respawnSeconds: 4f);

            var objective = ServerObjectiveFactory.Create(MatchObjective.None, settings, _host);

            Assert.That(objective, Is.TypeOf<TeamKillsRules>());
            Assert.That(objective.Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(objective.PlaysInRounds, Is.False);
            Assert.That(objective.KeepsMatchScore, Is.False);
            Assert.That(objective.HoldsFire, Is.False);
            Assert.That(objective.RemainingSeconds, Is.Null);
            Assert.That(objective.CurrentRoundPhase(), Is.Null);
            Assert.That(objective.DecideWinner(Team.Bravo), Is.EqualTo(Team.Bravo));
            Assert.That(objective.Tick(true, 1f), Is.False);
            var spec = objective.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(30));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(4f));
            Assert.That(spec.RespawnsByRound, Is.False);

            var snapshot = new MatchSnapshot { AlphaPoints = 5, BravoPoints = 6 };
            objective.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.Zero);
        }

        [Test]
        public void DominationScoresByPointsAndKeepsTheSessionFromEndingOnKills()
        {
            var settings = new NetMatchSettings(scoreLimit: 30, timeLimitSeconds: 600f);

            var objective = ServerObjectiveFactory.Create(MatchObjective.Domination, settings, _host);

            Assert.That(objective, Is.TypeOf<DominationRules>());
            Assert.That(objective.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(objective.PlaysInRounds, Is.False);
            Assert.That(objective.KeepsMatchScore, Is.False);
            var spec = objective.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(spec.RespawnsByRound, Is.False);
        }

        [Test]
        public void DominationBuildsItsPointsFromTheMapAndLogsWhenStarted()
        {
            var objective = (DominationRules)ServerObjectiveFactory.Create(MatchObjective.Domination, new NetMatchSettings(), _host);
            Assert.That(objective.Domination, Is.Null);

            objective.UseMap(TestMaps.Data("NetTest", TestRules.Points(controlPoints: 3)), TestRules.Points(controlPoints: 3));
            objective.Start();

            Assert.That(objective.Domination.Mode.PointCount, Is.EqualTo(3));
            Assert.That(_log.ToString(), Does.Contain("[Domination] net points=3"));
            var points = new SnapshotList<ControlPointSnapshot>(8, "control points");
            objective.FillControlPoints(points);
            Assert.That(points.Count, Is.EqualTo(3));
            Assert.That(points[2].Index, Is.EqualTo(2));
            Assert.That(points[2].HasOwner, Is.False);
            var snapshot = default(MatchSnapshot);
            objective.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(objective.DecideWinner(Team.Alpha), Is.Null, "an even point score is a draw whoever has more kills");
            objective.Stop();
        }

        [Test]
        public void DominationRefusesAMapWithoutControlPoints()
        {
            var objective = ServerObjectiveFactory.Create(MatchObjective.Domination, new NetMatchSettings(), _host);
            var map = TestMaps.Data("Empty", TestRules.Points());

            var check = Assert.Throws<InvalidOperationException>(() => objective.CheckMap(map, TestRules.Points()));
            Assert.That(check.Message, Is.EqualTo("cannot change to map Empty for the Domination match: it has no control points"));
            var use = Assert.Throws<InvalidOperationException>(() => objective.UseMap(map, TestRules.Points()));
            Assert.That(use.Message, Is.EqualTo("cannot start the Domination match: map Empty has no control points"));
            Assert.Throws<InvalidOperationException>(() => objective.Start(), "Start needs a map");
        }

        [Test]
        public void EliminationPlaysInRoundsWithoutKillOrTimeLimits()
        {
            var settings = new NetMatchSettings(respawnSeconds: 2f, roundWinsToWin: 4, roundPreparationSeconds: 5f, roundTimeLimitSeconds: 90f);

            var objective = ServerObjectiveFactory.Create(MatchObjective.Elimination, settings, _host);

            Assert.That(objective, Is.TypeOf<EliminationRules>());
            Assert.That(objective.Objective, Is.EqualTo(MatchObjective.Elimination));
            Assert.That(objective.PlaysInRounds, Is.True);
            Assert.That(objective.KeepsMatchScore, Is.True);
            Assert.That(objective.HoldsFire, Is.False, "the first round has not started preparing");
            Assert.That(objective.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Waiting));
            Assert.That(((EliminationRules)objective).Elimination.Rounds.WinsToWin, Is.EqualTo(4));
            var spec = objective.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(2f));
            Assert.That(spec.RespawnsByRound, Is.True);
            Assert.DoesNotThrow(() => objective.CheckMap(TestMaps.Data("Empty", TestRules.Points()), TestRules.Points()));

            objective.Start();
            Assert.That(_log.ToString(), Does.Contain("[Match] elimination winsToWin=4 preparation=5s roundTimeLimit=90s"));
            objective.Stop();
        }

        [Test]
        public void EliminationRestartsFromNoRoundWins()
        {
            var objective = ServerObjectiveFactory.Create(MatchObjective.Elimination, new NetMatchSettings(), _host);

            objective.MatchRestarted();

            Assert.That(_host.Scores, Is.EqualTo((0, 0)));
            var snapshot = new MatchSnapshot { AlphaPoints = 3, BravoPoints = 1 };
            objective.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Elimination));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.Zero);
        }

        [Test]
        public void AnObjectiveWithoutServerRulesIsRefused()
        {
            var e = Assert.Throws<InvalidOperationException>(
                () => ServerObjectiveFactory.Create((MatchObjective)99, new NetMatchSettings(), _host));

            Assert.That(e.Message, Is.EqualTo("the dedicated server has no rules for the match objective 99"));
        }

        [TestCase(MatchObjective.CaptureTheFlag, typeof(CaptureTheFlagRules))]
        [TestCase(MatchObjective.Hardpoint, typeof(HardpointRules))]
        [TestCase(MatchObjective.SearchAndDestroy, typeof(SearchAndDestroyRules))]
        [TestCase(MatchObjective.FreeForAll, typeof(FreeForAllRules))]
        [TestCase(MatchObjective.KillConfirmed, typeof(KillConfirmedRules))]
        [TestCase(MatchObjective.GunGame, typeof(GunGameRules))]
        [TestCase(MatchObjective.Payload, typeof(PayloadRules))]
        [TestCase(MatchObjective.Horde, typeof(HordeRules))]
        [TestCase(MatchObjective.Infected, typeof(InfectedRules))]
        [TestCase(MatchObjective.Guardian, typeof(GuardianRules))]
        [TestCase(MatchObjective.Execution, typeof(ExecutionRules))]
        public void EveryOnlineModeHasServerRules(MatchObjective objective, Type expected)
        {
            var settings = new NetMatchSettings(gunGame: new GunGameSpec(600f, 2f, new[] { TestRules.RifleId }, Array.Empty<string>(), true));

            var rules = ServerObjectiveFactory.Create(objective, settings, _host);

            Assert.That(rules, Is.TypeOf(expected));
            Assert.That(rules.Objective, Is.EqualTo(objective));
        }

        [Test]
        public void EveryObjectiveTheProtocolCarriesHasServerRules()
        {
            var settings = new NetMatchSettings(gunGame: new GunGameSpec(600f, 2f, new[] { TestRules.RifleId }, Array.Empty<string>(), true));

            using var stashes = new TempDirectory();
            var parts = Parts(stashes.Path);

            foreach (var objective in ServerObjectiveFactory.Supported)
            {
                var rules = ServerObjectiveFactory.Create(objective, settings, _host, parts);

                Assert.That(rules.Objective, Is.EqualTo(objective));
                Assert.That(
                    rules.UsesLoot,
                    Is.EqualTo(objective == MatchObjective.BattleRoyale || objective == MatchObjective.Extraction),
                    objective + ": only the modes that open boxes get the map boxes");
            }
        }

        [Test]
        public void EveryObjectiveIsSupported()
        {
            foreach (var objective in Enum.GetValues<MatchObjective>())
            {
                Assert.That(ServerObjectiveFactory.Supports(objective), Is.True, objective.ToString());
            }

            Assert.That(ServerObjectiveFactory.Supported.Count, Is.EqualTo(16));
            Assert.That(ServerObjectiveFactory.Supports((MatchObjective)99), Is.False);
        }

        // 部品をつながずに作る口 (MatchRulesModule の既定の選び方) でも、試合の場を使わない目的は作れる
        [TestCase(MatchObjective.Horde)]
        [TestCase(MatchObjective.Infected)]
        [TestCase(MatchObjective.Guardian)]
        [TestCase(MatchObjective.Execution)]
        public void TheLaterOnlineModesNeedNoMatchParts(MatchObjective objective)
        {
            using var stashes = new TempDirectory();

            var bare = ServerObjectiveFactory.Create(objective, new NetMatchSettings(), _host);
            var withParts = ServerObjectiveFactory.Create(objective, new NetMatchSettings(), _host, Parts(stashes.Path));

            Assert.That(bare.Objective, Is.EqualTo(objective));
            Assert.That(withParts.GetType(), Is.EqualTo(bare.GetType()));
            Assert.That(bare.UsesLoot, Is.False);
        }

        [Test]
        public void HordePlaysWithoutKillOrTimeLimitsAndFixesThePlayersAgainstTheBots()
        {
            var settings = new NetMatchSettings(respawnSeconds: 3f);

            var objective = ServerObjectiveFactory.Create(MatchObjective.Horde, settings, _host);

            Assert.That(objective.FixedTeam(false), Is.EqualTo((Team?)Team.Alpha));
            Assert.That(objective.FixedTeam(true), Is.EqualTo((Team?)Team.Bravo));
            var spec = objective.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnsByRound, Is.True);
            Assert.That(((HordeRules)objective).Fortify(1), Is.EqualTo(HordePurchaseVerdict.NothingToDo), "without parts there are no fortifications");
        }

        [Test]
        public void HordeTakesTheFortificationsFromTheMatchParts()
        {
            var fortifications = new FakeFortifications();
            var parts = Parts(null);
            parts = new ServerObjectiveParts(parts.Rules, parts.Loot, parts.World, null, 1, fortifications);

            var objective = ServerObjectiveFactory.Create(MatchObjective.Horde, new NetMatchSettings(), _host, parts);
            objective.Start();

            Assert.That(fortifications.Prepared, Is.EqualTo(1));
        }

        [Test]
        public void TheRegistrationRefusesAnObjectiveTwiceOrWithoutRules()
        {
            ServerObjectiveCreator create = (settings, host, parts) => new TeamKillsRules();

            var twice = Assert.Throws<ArgumentException>(
                () => ServerObjectiveFactory.Register((MatchObjective.None, create), (MatchObjective.None, create)));
            var empty = Assert.Throws<ArgumentException>(() => ServerObjectiveFactory.Register((MatchObjective.Horde, null)));
            var registered = ServerObjectiveFactory.Register((MatchObjective.Horde, create));

            Assert.That(twice.Message, Does.StartWith("the match objective None is registered twice"));
            Assert.That(empty.Message, Does.StartWith("the match objective Horde is registered without rules"));
            Assert.That(registered[MatchObjective.Horde], Is.SameAs(create));
            Assert.Throws<ArgumentNullException>(() => ServerObjectiveFactory.Register(null));
        }

        [Test]
        public void PayloadPushesTheCartOfTheMapWithTheConfiguredTuning()
        {
            var tuning = new PayloadSpec(cartSpeed: 3f, timeLimitSeconds: 240f);
            var settings = new NetMatchSettings(respawnSeconds: 6f, payload: tuning);

            var objective = (PayloadRules)ServerObjectiveFactory.Create(MatchObjective.Payload, settings, _host);

            Assert.That(objective.Objective, Is.EqualTo(MatchObjective.Payload));
            Assert.That(objective.PlaysInRounds, Is.False);
            Assert.That(objective.KeepsMatchScore, Is.False);
            var spec = objective.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(6f));
            Assert.That(objective.Payload, Is.Null);

            var points = TestRules.PayloadPoints();
            objective.UseMap(TestMaps.Data("NetArena01", points), points);
            objective.Start();

            Assert.That(objective.Payload.Spec, Is.SameAs(tuning));
            Assert.That(objective.RemainingSeconds, Is.EqualTo(240f));
            Assert.That(_log.ToString(), Does.Contain("[Payload] net attackers=Alpha length=20.0m checkpoints=1 radius=3 cartSpeed=3"));
            var snapshot = default(MatchSnapshot);
            objective.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Payload));
            Assert.That(snapshot.PayloadProgress, Is.Zero);
            var items = new SnapshotList<ObjectiveItemSnapshot>(SnapshotMessage.MaxObjectiveItems, "objective item");
            objective.FillObjectiveItems(items);
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.Cart));
            Assert.That(objective.DecideWinner(Team.Alpha), Is.EqualTo(Team.Bravo), "the defenders hold until the cart arrives");
            objective.Stop();
        }

        [TestCase(MatchObjective.BattleRoyale, "BattleRoyale")]
        [TestCase(MatchObjective.Extraction, "Extraction")]
        public void WithoutTheMatchPartsTheBoxModesAreRefused(MatchObjective objective, string name)
        {
            var e = Assert.Throws<InvalidOperationException>(
                () => ServerObjectiveFactory.Create(objective, new NetMatchSettings(), _host));

            Assert.That(e.Message, Is.EqualTo(
                "the " + name + " rules need the loot module and the match world; create the server modules with ServerModules.Create"));
        }

        [Test]
        public void BattleRoyaleIsMadeWithTheWorldOfThePartsAndTheDefaultTuningBeforeRulesAreLoaded()
        {
            using var stashes = new TempDirectory();

            var objective = ServerObjectiveFactory.Create(MatchObjective.BattleRoyale, new NetMatchSettings(), _host, Parts(stashes.Path));

            Assert.That(objective, Is.TypeOf<BattleRoyaleRules>());
            Assert.That(((BattleRoyaleRules)objective).Spec, Is.SameAs(BattleRoyaleSpec.Default), "the rules module has loaded no rules yet");
            Assert.That(objective.KeepsMatchScore, Is.True);
            Assert.That(objective.PlaysInRounds, Is.False);
        }

        [Test]
        public void ExtractionIsMadeWithTheStashStoreOfTheParts()
        {
            using var stashes = new TempDirectory();

            var objective = ServerObjectiveFactory.Create(MatchObjective.Extraction, new NetMatchSettings(), _host, Parts(stashes.Path));

            Assert.That(objective, Is.TypeOf<ExtractionRules>());
            Assert.That(objective.Objective, Is.EqualTo(MatchObjective.Extraction));
            Assert.That(objective.UsesLoot, Is.True);
        }

        // 試合の途中で Extraction に替えようとしたときも、起動のときと同じ理由で断る
        [Test]
        public void ExtractionWithoutAStashStoreIsRefusedWithTheReasonOfTheLaunch()
        {
            var parts = Parts(null);

            var e = Assert.Throws<InvalidOperationException>(
                () => ServerObjectiveFactory.Create(MatchObjective.Extraction, new NetMatchSettings(), _host, parts));

            Assert.That(ServerLaunch.TryParse(Array.Empty<string>(), _ => null, out var launch, out var error), Is.True, error);
            Assert.That(e.Message, Is.EqualTo(launch.StashProblem(new[] { MatchObjective.Extraction })));
            Assert.That(e.Message, Does.StartWith("the Extraction rules keep each account's stash on disk; pass --stash-dir <directory>"));
            Assert.DoesNotThrow(
                () => ServerObjectiveFactory.Create(MatchObjective.BattleRoyale, new NetMatchSettings(), _host, parts),
                "Battle Royale keeps no stash");
        }

        [Test]
        public void ThePartsNeedTheRulesTheLootAndTheWorld()
        {
            var rules = new MatchRulesModule();
            var combat = new ServerCombat(rules, new ServerMovement(ServerMotorSettings.Default));
            var loot = new ServerLoot(rules, combat, new ServerInventories(rules, combat));
            var world = new BattleRoyaleTestWorld();

            Assert.Throws<ArgumentNullException>(() => new ServerObjectiveParts(null, loot, world, null));
            Assert.Throws<ArgumentNullException>(() => new ServerObjectiveParts(rules, null, world, null));
            Assert.Throws<ArgumentNullException>(() => new ServerObjectiveParts(rules, loot, null, null));
            Assert.That(new ServerObjectiveParts(rules, loot, world, null).Stashes, Is.Null, "a server without --stash-dir has no stash store");
        }

        [TestCase(MatchObjective.CaptureTheFlag, "flag")]
        [TestCase(MatchObjective.Hardpoint, "hardpoint")]
        [TestCase(MatchObjective.SearchAndDestroy, "bomb site")]
        [TestCase(MatchObjective.Payload, "payload route")]
        public void ModesThatNeedMapPointsRefuseAMapWithoutThem(MatchObjective objective, string what)
        {
            var objectiveRules = ServerObjectiveFactory.Create(objective, new NetMatchSettings(), _host);
            var map = TestMaps.Data("Empty", TestRules.Points());

            Assert.Throws<InvalidOperationException>(() => objectiveRules.CheckMap(map, TestRules.Points()), what);
        }

        [Test]
        public void TheFactoryNeedsSettingsAndAHost()
        {
            Assert.Throws<ArgumentNullException>(() => ServerObjectiveFactory.Create(MatchObjective.None, null, _host));
            Assert.Throws<ArgumentNullException>(() => ServerObjectiveFactory.Create(MatchObjective.None, new NetMatchSettings(), null));
        }

        // 目的を作るだけで試合は始めないので、モジュールは Start しない。directory が null なら持ち物庫の無いサーバー
        private ServerObjectiveParts Parts(string directory)
        {
            var rules = new MatchRulesModule();
            var combat = new ServerCombat(rules, new ServerMovement(ServerMotorSettings.Default));
            var loot = new ServerLoot(rules, combat, new ServerInventories(rules, combat), 1);
            ServerStashStore store = null;
            if (directory != null)
            {
                Assert.That(ServerStashStore.TryOpen(directory, _log, out store, out var error), Is.True, error);
            }

            return new ServerObjectiveParts(rules, loot, new BattleRoyaleTestWorld(), store, 1);
        }

        private sealed class FakeFortifications : IHordeFortifications
        {
            public int Prepared { get; private set; }

            public void Prepare()
            {
                Prepared++;
            }

            public void Tick()
            {
            }

            public HordeFortificationTarget? FindInReach(Vector3 position, float radius)
            {
                return null;
            }

            public int Restore(int fortificationId, HordeFortificationAction action)
            {
                return 0;
            }
        }

        // Session を作る前の目的だけを確かめるので、Session と体は持たない
        private sealed class FakeHost : IMatchObjectiveHost
        {
            public FakeHost(TextWriter log)
            {
                Log = log;
            }

            public TeamDeathmatchSession Session => null;

            public ServerBodies Bodies => null;

            public TextWriter Log { get; }

            public (int Alpha, int Bravo)? Scores { get; private set; }

            public void SetMatchScores(int alpha, int bravo)
            {
                Scores = (alpha, bravo);
            }

            public void ClearDowned()
            {
            }
        }
    }
}
