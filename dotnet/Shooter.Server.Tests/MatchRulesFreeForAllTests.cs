using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // FreeForAllRules はまだ ServerObjectiveFactory に載せていないので、目的の作り方を差し替えた MatchRulesModule で試合として進める
    public sealed class MatchRulesFreeForAllTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private MatchRulesModule _module;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Stop();
            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        private FreeForAllRules Rules => (FreeForAllRules)_module.Objective;

        [Test]
        public void TheFirstToTheKillLimitWinsEvenAgainstATeammate()
        {
            Start(new FreeForAllSpec(scoreLimit: 3, timeLimitSeconds: 300f, respawnSeconds: 0.5f, pointsPerKill: 1));
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            Assert.That(_log.ToString(), Does.Contain("[FreeForAll] net scoreLimit=3 timeLimit=300s respawn=0.5s pointsPerKill=1"));
            Step();
            Assert.That(_module.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(Rules.Mode.State, Is.EqualTo(GameModeState.Playing));

            Assert.That(_module.ApplyDamage(1, 3, 100), Is.EqualTo(DamageOutcome.Killed), "a teammate in the session is still an enemy");
            Step();
            Assert.That(Row(1).Kills, Is.EqualTo(1), "the session does not count a kill of a teammate, the free-for-all does");
            Assert.That(Row(3).Deaths, Is.EqualTo(1));
            Assert.That(Rules.GetScore(1), Is.EqualTo(1));

            Advance(0.6f);
            Assert.That(_module.Session.IsAlive(3), Is.True, "the session brings the fallen back after the free-for-all respawn time");
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();
            Assert.That(_match.AlphaScore, Is.Zero, "team kills are not the match score in a free-for-all");
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));

            Advance(0.6f);
            Assert.That(_module.ApplyDamage(1, 3, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();

            Assert.That(Rules.Mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Rules.WinnerId, Is.EqualTo(1));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_module.Session.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.FreeForAll));
            Assert.That(snapshot.HasWinner, Is.False, "no team wins a free-for-all");
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(1), "points per kill");
            Assert.That(snapshot.BravoPoints, Is.EqualTo(3), "kill limit");
            Assert.That(Row(1).Kills, Is.EqualTo(3));
            Assert.That(Row(2).Deaths, Is.EqualTo(1));
            Assert.That(Row(3).Deaths, Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[FreeForAll] ended winner=1 scores=1:3,2:0,3:0"));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"draw\"").And.Contain("\"scoreAlpha\":\"0\""));
        }

        [Test]
        public void PointsPerKillMultiplyTheKillsTowardTheLimit()
        {
            Start(new FreeForAllSpec(scoreLimit: 4, timeLimitSeconds: 300f, respawnSeconds: 0.5f, pointsPerKill: 2));
            Join(1);
            Join(2);
            Step();

            _module.ApplyDamage(2, 1, 100);
            Step();
            Assert.That(Rules.GetScore(2), Is.EqualTo(2));
            Assert.That(Row(2).Kills, Is.EqualTo(1));
            Assert.That(Match().AlphaPoints, Is.EqualTo(2));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));

            Advance(0.6f);
            _module.ApplyDamage(2, 1, 100);
            Step();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Rules.WinnerId, Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[FreeForAll] ended winner=2 scores=1:0,2:4"));
        }

        [Test]
        public void TheSoleLeaderWinsAtTimeUp()
        {
            Start(new FreeForAllSpec(scoreLimit: 10, timeLimitSeconds: 2f, respawnSeconds: 0.5f));
            Join(1);
            Join(2);
            Join(3);
            Step();
            Assert.That(Match().RemainingSeconds, Is.EqualTo(2));

            _module.ApplyDamage(3, 2, 100);
            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Rules.WinnerId, Is.EqualTo(3));
            Assert.That(Match().RemainingSeconds, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[FreeForAll] ended winner=3 scores=1:0,2:0,3:1"));
        }

        [Test]
        public void AnEvenTimeUpIsADraw()
        {
            Start(new FreeForAllSpec(scoreLimit: 10, timeLimitSeconds: 2f, respawnSeconds: 0.5f));
            Join(1);
            Join(2);
            Step();

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Rules.WinnerId, Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[FreeForAll] ended winner=draw scores=1:0,2:0"));
        }

        [Test]
        public void ASuicideCountsADeathButNoPoint()
        {
            Start(new FreeForAllSpec(scoreLimit: 3, timeLimitSeconds: 300f, respawnSeconds: 0.5f));
            Join(1);
            Join(2);
            Step();

            Assert.That(_module.ApplyDamage(null, 1, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();

            Assert.That(Row(1).Deaths, Is.EqualTo(1));
            Assert.That(Row(1).Kills, Is.Zero);
            Assert.That(Rules.GetScore(1), Is.Zero);
            Assert.That(Rules.GetScore(2), Is.Zero);
        }

        [Test]
        public void TheNextMatchStartsFromNoPoints()
        {
            Start(new FreeForAllSpec(scoreLimit: 1, timeLimitSeconds: 300f, respawnSeconds: 0.5f), restartDelaySeconds: 2f);
            Join(1);
            Join(2);
            Step();
            _module.ApplyDamage(1, 2, 100);
            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(Rules.Mode.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(Rules.GetScore(1), Is.Zero);
            Assert.That(Row(1).Kills, Is.Zero);
            Assert.That(Rules.WinnerId, Is.Null);
            Assert.That(Match().RemainingSeconds, Is.EqualTo(300));
            Assert.That(_log.ToString(), Does.Contain("[FreeForAll] restart"));
        }

        [Test]
        public void APlayerWhoComesBackKeepsTheirKillsAndPoints()
        {
            Start(new FreeForAllSpec(scoreLimit: 10, timeLimitSeconds: 300f, respawnSeconds: 0.5f));
            Join(1);
            Join(2);
            Join(3);
            Step();
            _module.ApplyDamage(1, 2, 100);
            Step();

            _match.Leave(new ConnectionId(1));
            Step();
            Assert.That(Rules.Mode.HasPlayer(1), Is.False);
            Join(1);
            Step();

            Assert.That(Rules.Mode.HasPlayer(1), Is.True);
            Assert.That(Row(1).Kills, Is.EqualTo(1));
            Assert.That(Rules.GetScore(1), Is.EqualTo(1));
        }

        [Test]
        public void TheSessionLetsEveryoneShootEveryoneAndNeverEndsOnItsOwn()
        {
            var rules = new FreeForAllRules(Settings(new FreeForAllSpec(respawnSeconds: 4f)), new NullHost(_log));
            var gameRules = GameRules.Default
                .WithFriendlyFire(FriendlyFire.Off)
                .WithRespawnMode(RespawnMode.None)
                .WithActions(ActionRules.Default.WithDowned(Downed.Revive));

            var spec = rules.CreateSessionSpec(gameRules, Settings(FreeForAllSpec.Default));

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.FreeForAll));
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.True);
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.CurrentRoundPhase(), Is.Null);
            Assert.That(rules.RemainingSeconds, Is.EqualTo(FreeForAllMode.DefaultTimeLimitSeconds));
            Assert.That(rules.DecideWinner(Team.Alpha), Is.Null);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(4f), "the respawn time is the free-for-all one");
            Assert.That(spec.RespawnsByRound, Is.False);
            Assert.That(spec.Rules.FriendlyFire, Is.EqualTo(FriendlyFire.On));
            Assert.That(spec.Rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(spec.Rules.Actions.Downed, Is.EqualTo(Downed.Off));
            Assert.DoesNotThrow(() => rules.CheckMap(TestMaps.Data("Empty", TestRules.Points()), TestRules.Points()));
        }

        [Test]
        public void TheFactoryDoesNotPickTheFreeForAllYet()
        {
            var e = Assert.Throws<InvalidOperationException>(
                () => ServerObjectiveFactory.Create(MatchObjective.FreeForAll, new NetMatchSettings(), new NullHost(_log)));

            Assert.That(e.Message, Is.EqualTo("the dedicated server has no rules for the match objective FreeForAll"));
        }

        [Test]
        public void TheSnapshotHasNoControlPointsOrObjectiveItems()
        {
            Start(new FreeForAllSpec());
            Join(1);
            Join(2);
            Step();
            var snapshot = new SnapshotMessage();
            snapshot.ControlPoints.Add();
            snapshot.ObjectiveItems.Add();
            ref var row1 = ref snapshot.Players.Add();
            row1.PlayerId = 1;
            ref var row2 = ref snapshot.Players.Add();
            row2.PlayerId = 2;

            _module.Contribute(snapshot, null);

            Assert.That(snapshot.ControlPoints.Count, Is.Zero);
            Assert.That(snapshot.ObjectiveItems.Count, Is.Zero);
            Assert.That(snapshot.Match.Objective, Is.EqualTo(MatchObjective.FreeForAll));
            Assert.That(snapshot.Match.AlphaPoints, Is.EqualTo(FreeForAllMode.DefaultPointsPerKill));
            Assert.That(snapshot.Match.BravoPoints, Is.EqualTo(FreeForAllMode.DefaultScoreLimit));
            Assert.That(snapshot.Match.RemainingSeconds, Is.EqualTo(300));
        }

        private static NetMatchSettings Settings(FreeForAllSpec freeForAll, float restartDelaySeconds = 10f)
        {
            return new NetMatchSettings(restartDelaySeconds: restartDelaySeconds, freeForAll: freeForAll);
        }

        private static GameRules GameRulesFor()
        {
            return GameRules.Default
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(MatchObjective.FreeForAll);
        }

        private static IMatchObjectiveRules CreateObjective(MatchObjective objective, NetMatchSettings settings, IMatchObjectiveHost host)
        {
            return objective == MatchObjective.FreeForAll
                ? new FreeForAllRules(settings, host)
                : ServerObjectiveFactory.Create(objective, settings, host);
        }

        private void Start(FreeForAllSpec freeForAll, float restartDelaySeconds = 10f)
        {
            var settings = Settings(freeForAll, restartDelaySeconds);
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points()),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(GameRulesFor(), settings));
            var module = new MatchRulesModule(CreateObjective);
            module.Start(context);
            _module = module;
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private MatchSnapshot Match()
        {
            var snapshot = default(MatchSnapshot);
            _module.FillMatch(ref snapshot);
            return snapshot;
        }

        private PlayerSnapshot Row(int id)
        {
            var row = default(PlayerSnapshot);
            Assert.That(_module.FillPlayer(id, ref row), Is.True);
            return row;
        }

        private void Step()
        {
            _match.Tick(_tickSeconds);
            _module.Tick(++_tick, _tickSeconds);
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Step();
            }
        }

        private List<string> JsonOf(string type)
        {
            return _json.ToString().Split('\n').Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal)).ToList();
        }

        // Session を作る前に決まることだけを確かめるので、Session と体は持たない
        private sealed class NullHost : IMatchObjectiveHost
        {
            public NullHost(TextWriter log)
            {
                Log = log;
            }

            public TeamDeathmatchSession Session => null;

            public ServerBodies Bodies => null;

            public TextWriter Log { get; }

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }
        }
    }
}
