using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesEliminationTests
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

        [Test]
        public void RoundsAreWonByWipingTheOtherTeamAndTheMatchEndsAtTheWinsToWin()
        {
            Start(Settings(winsToWin: 2, preparationSeconds: 1f));
            Join(1);
            Join(2);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(_module.Objective, Is.TypeOf<EliminationRules>());
            Assert.That(_log.ToString(), Does.Contain("[Match] elimination winsToWin=2 preparation=1s roundTimeLimit=30s"));

            Step();
            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Preparing));
            Assert.That(_module.CanFire(1), Is.False);
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Ignored));

            Advance(1.1f);
            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Live));
            Assert.That(_module.CanFire(1), Is.True);
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();

            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Preparing));
            Assert.That(_module.Elimination.GetRoundWins(Team.Alpha), Is.EqualTo(1));
            Assert.That(_match.AlphaScore, Is.EqualTo(1));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.Session.IsAlive(2), Is.True, "the next round brings the fallen back");
            Assert.That(Row(2).Deaths, Is.EqualTo(1), "the next round keeps the stats");
            Assert.That(Row(1).Kills, Is.EqualTo(1));
            Assert.That(Match().AlphaPoints, Is.EqualTo(1));
            Assert.That(Match().BravoPoints, Is.Zero);

            Advance(1.1f);
            _module.ApplyDamage(1, 2, 100);
            Step();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Ended));
            var snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.HasRound, Is.True);
            Assert.That(snapshot.Round, Is.EqualTo(RoundPhase.Ended));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Alpha\"").And.Contain("\"scoreAlpha\":\"2\""));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Match] round 1 preparing"));
            Assert.That(log, Does.Contain("[Match] round 1 live"));
            Assert.That(log, Does.Contain("[Match] round 1 ended winner=Alpha wins=1-0"));
            Assert.That(log, Does.Contain("[Match] round 2 ended winner=Alpha wins=2-0"));
            Assert.That(log, Does.Contain("[Match] round phase=Preparing"));
            Assert.That(log, Does.Contain("[Match] round phase=Ended"));
        }

        [Test]
        public void AFallenPlayerStaysDownUntilTheNextRound()
        {
            Start(Settings(winsToWin: 3, preparationSeconds: 0f), respawnSeconds: 0f);
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            Step();
            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Live));

            Assert.That(_module.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Killed));
            Advance(5f);

            Assert.That(_module.Session.IsAlive(1), Is.False);
            Assert.That(_module.CanFire(1), Is.False);
            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Live));
            Assert.That(_module.Elimination.GetRoundWins(Team.Bravo), Is.Zero);
            Assert.That(_match.BravoScore, Is.Zero, "kills do not score in rounds");
        }

        [Test]
        public void TheTeamWithMorePlayersStandingTakesTheRoundAtTimeUp()
        {
            Start(Settings(winsToWin: 3, preparationSeconds: 0f, roundTimeLimitSeconds: 2f));
            Join(1);
            Join(2);
            Join(3);
            Step();
            Assert.That(Match().RemainingSeconds, Is.EqualTo(2));

            Advance(2.1f);

            Assert.That(_module.Elimination.GetRoundWins(Team.Alpha), Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Match] round 1 ended winner=Alpha wins=1-0"));
        }

        [Test]
        public void AnEvenTimeUpIsADrawThatScoresNoOne()
        {
            Start(Settings(winsToWin: 3, preparationSeconds: 0f, roundTimeLimitSeconds: 2f));
            Join(1);
            Join(2);
            Step();

            Advance(2.1f);

            Assert.That(_module.Elimination.GetRoundWins(Team.Alpha), Is.Zero);
            Assert.That(_module.Elimination.GetRoundWins(Team.Bravo), Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Match] round 1 ended winner=draw wins=0-0"));
            Assert.That(_module.Elimination.Rounds.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void TheSnapshotCountsDownThePreparation()
        {
            Start(Settings(winsToWin: 3, preparationSeconds: 4f));
            Join(1);
            Join(2);
            Step();

            Advance(1f);

            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Elimination));
            Assert.That(snapshot.HasRound, Is.True);
            Assert.That(snapshot.Round, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(3));
        }

        [Test]
        public void TheNextMatchStartsFromNoRoundWins()
        {
            Start(Settings(winsToWin: 1, preparationSeconds: 0f), restartDelaySeconds: 2f);
            Join(1);
            Join(2);
            Step();
            _module.ApplyDamage(1, 2, 100);
            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_match.AlphaScore, Is.EqualTo(1));

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.AlphaScore, Is.Zero);
            Assert.That(_module.Elimination.GetRoundWins(Team.Alpha), Is.Zero);
            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Live));
            Assert.That(_module.Elimination.Rounds.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void ALeaverIsDroppedFromTheRoundWithoutWipingOutTheTeam()
        {
            Start(Settings(winsToWin: 3, preparationSeconds: 0f));
            Join(1);
            Join(2);
            Join(3);
            Step();

            _match.Leave(new ConnectionId(2));
            Step();

            Assert.That(_module.Elimination.HasPlayer(2), Is.False);
            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Live), "a team with nobody left is not wiped out");
        }

        [Test]
        public void TeamDeathmatchHasNoRounds()
        {
            Start(Settings(winsToWin: 3, preparationSeconds: 1f), objective: MatchObjective.None);
            Join(1);
            Join(2);
            Step();

            Assert.That(_module.Objective, Is.TypeOf<TeamKillsRules>());
            Assert.That(_module.Elimination, Is.Null);
            Assert.That(_module.CurrentRoundPhase(), Is.Null);
            Assert.That(_module.CanFire(1), Is.True);
        }

        private static NetMatchSettings Settings(
            int winsToWin, float preparationSeconds, float roundTimeLimitSeconds = 30f, float respawnSeconds = 3f, float restartDelaySeconds = 10f)
        {
            return new NetMatchSettings(
                respawnSeconds: respawnSeconds,
                restartDelaySeconds: restartDelaySeconds,
                roundWinsToWin: winsToWin,
                roundPreparationSeconds: preparationSeconds,
                roundTimeLimitSeconds: roundTimeLimitSeconds);
        }

        private static GameRules Rules(MatchObjective objective)
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithRespawnMode(RespawnMode.None)
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(objective);
        }

        private void Start(
            NetMatchSettings settings, float? respawnSeconds = null, float? restartDelaySeconds = null, MatchObjective objective = MatchObjective.Elimination)
        {
            if (respawnSeconds.HasValue || restartDelaySeconds.HasValue)
            {
                settings = new NetMatchSettings(
                    respawnSeconds: respawnSeconds ?? settings.RespawnSeconds,
                    restartDelaySeconds: restartDelaySeconds ?? settings.RestartDelaySeconds,
                    roundWinsToWin: settings.RoundWinsToWin,
                    roundPreparationSeconds: settings.RoundPreparationSeconds,
                    roundTimeLimitSeconds: settings.RoundTimeLimitSeconds);
            }

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
                TestRules.Data(Rules(objective), settings));
            var module = new MatchRulesModule();
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
    }
}
