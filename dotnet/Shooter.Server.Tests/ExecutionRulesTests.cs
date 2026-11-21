using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 試合を通した流れ (ダウン・とどめ・勝敗) は ServerMeleeTests.Execution で確かめ、ここは Session の無いときの値と引数を確かめる
    public sealed class ExecutionRulesTests
    {
        private StringWriter _log;
        private LogHost _host;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _host = new LogHost(_log);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void TheObjectivePlaysInRoundsAndKeepsTheSessionFromEndingFirst()
        {
            var settings = new NetMatchSettings(
                respawnSeconds: 9f, roundWinsToWin: 2, roundPreparationSeconds: 4f, roundTimeLimitSeconds: 90f, execution: new ExecutionSpec(2f, 2.5f));
            var rules = new ExecutionRules(settings, _host);

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Execution));
            Assert.That(rules.PlaysInRounds, Is.True);
            Assert.That(rules.KeepsMatchScore, Is.True);
            Assert.That(rules.HoldsFire, Is.False, "the round has not started preparing");
            Assert.That(rules.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Waiting));
            Assert.That(rules.Rounds.Rounds.WinsToWin, Is.EqualTo(2));
            Assert.That(rules.Spec, Is.SameAs(settings.Execution));
            Assert.That(rules.Tick(false, 1f), Is.False, "the round does not start before the match plays");
            Assert.That(rules.DecideWinner(Team.Bravo), Is.Null, "the kill winner does not decide an execution match");

            var spec = rules.CreateSessionSpec(GameRules.Default.WithActions(ActionRules.Default.WithDowned(Downed.Revive).WithMelee(Melee.On)), settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(9f));
            Assert.That(spec.RespawnsByRound, Is.True);
            Assert.That(spec.DownsLastStanding, Is.True, "the last player standing waits for the execution");
        }

        [Test]
        public void TheDownedFinisherComesFromTheExecutionSpec()
        {
            var rules = new ExecutionRules(new NetMatchSettings(execution: new ExecutionSpec(2.5f, 3f)), _host);

            var move = rules.DownedFinisher;

            Assert.That(move, Is.Not.Null);
            Assert.That(move.Id, Is.EqualTo(ContextMeleeMove.ExecuteId));
            Assert.That(move.Required, Is.EqualTo(ContextMeleeConditions.TargetDowned));
            Assert.That(move.PerformSeconds, Is.EqualTo(2.5f));
            Assert.That(move.Reach, Is.EqualTo(3f));
            Assert.That(rules.DownedFinisher, Is.SameAs(move), "the same move each time, so the melee module does not register it again");
        }

        // オフラインの ExecutionRunner と同じく、ダウンを使わないルールでもダウンを使う
        [Test]
        public void RulesWithoutDownedArePlayedWithRevive()
        {
            var rules = new ExecutionRules(NetMatchSettings.Default, _host);
            var gameRules = GameRules.Default.WithActions(ActionRules.Default.WithDowned(Downed.Off).WithMelee(Melee.On));

            var spec = rules.CreateSessionSpec(gameRules, NetMatchSettings.Default);

            Assert.That(spec.Rules.Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(spec.Rules.Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(_log.ToString(), Does.Contain("[Execution] rules downed=Off is played as Revive"));
        }

        // 近接が使えないルールではとどめを刺す手が無いので、ダウン中の敵も撃って倒せるようにする
        [Test]
        public void WithoutMeleeThereIsNoFinisherAndTheLogSaysSo()
        {
            var rules = new ExecutionRules(NetMatchSettings.Default, _host);
            var gameRules = GameRules.Default.WithActions(ActionRules.Default.WithDowned(Downed.Revive).WithMelee(Melee.Off));

            rules.CreateSessionSpec(gameRules, NetMatchSettings.Default);

            Assert.That(rules.DownedFinisher, Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[Execution] rules melee=Off; no one can execute"));

            rules.CreateSessionSpec(gameRules.WithActions(gameRules.Actions.WithMelee(Melee.On)), NetMatchSettings.Default);

            Assert.That(rules.DownedFinisher, Is.Not.Null, "the next session's rules turn the finisher back on");
        }

        [Test]
        public void TheSnapshotCarriesTheRoundWinsAndTheRoundsToWin()
        {
            var rules = new ExecutionRules(new NetMatchSettings(roundWinsToWin: 4), _host);
            var snapshot = new MatchSnapshot { AlphaPoints = 7, BravoPoints = 8, HasLastRound = true, HasLastRoundWinner = true };

            rules.FillMatch(ref snapshot);

            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Execution));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(snapshot.RoundNumber, Is.Zero, "no round has started");
            Assert.That(snapshot.RoundsToWin, Is.EqualTo(4));
            Assert.That(snapshot.HasLastRound, Is.False);
            Assert.That(snapshot.HasLastRoundWinner, Is.False);
        }

        [TestCase(true, null, RoundEndReason.TimeUp)]
        [TestCase(true, 0, RoundEndReason.TimeUp)]
        [TestCase(false, null, RoundEndReason.BothEliminated)]
        [TestCase(false, 0, RoundEndReason.DefendersEliminated)]
        [TestCase(false, 1, RoundEndReason.AttackersEliminated)]
        public void TheRoundEndReasonNamesTheSideThatWasWipedOut(bool timeUp, int? winnerId, RoundEndReason expected)
        {
            var winner = winnerId.HasValue ? (winnerId.Value == 0 ? Side.Alpha : Side.Bravo) : (Side?)null;

            Assert.That(ExecutionRules.EndReason(winner, timeUp), Is.EqualTo(expected));
        }

        [Test]
        public void EventsBeforeAnyoneJoinsAreIgnored()
        {
            var rules = new ExecutionRules(NetMatchSettings.Default, _host);

            Assert.DoesNotThrow(() => rules.PlayerJoined(1), "there is no Session yet");
            Assert.DoesNotThrow(() => rules.PlayerKilled(1, 2));
            Assert.DoesNotThrow(() => rules.PlayerLeft(2));
            Assert.That(rules.Rounds.PlayerCount, Is.Zero);
        }

        [Test]
        public void StartingLogsTheRoundsAndTheExecutionTuning()
        {
            var settings = new NetMatchSettings(
                roundWinsToWin: 2, roundPreparationSeconds: 4f, roundTimeLimitSeconds: 90f, execution: new ExecutionSpec(2.5f, 3f));
            var rules = new ExecutionRules(settings, _host);

            rules.Start();
            rules.Start();
            rules.Stop();

            Assert.That(_log.ToString(), Is.EqualTo(
                "[Execution] net winsToWin=2 preparation=4s roundTimeLimit=90s executeSeconds=2.5 executeRange=3" + Environment.NewLine));
        }

        [Test]
        public void TheSettingsAndTheHostAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new ExecutionRules(null, _host));
            Assert.Throws<ArgumentNullException>(() => new ExecutionRules(NetMatchSettings.Default, null));
            var rules = new ExecutionRules(NetMatchSettings.Default, _host);
            Assert.Throws<ArgumentNullException>(() => rules.CreateSessionSpec(null, NetMatchSettings.Default));
            Assert.Throws<ArgumentNullException>(() => rules.CreateSessionSpec(GameRules.Default, null));
            Assert.Throws<ArgumentNullException>(() => rules.FillControlPoints(null));
            Assert.Throws<ArgumentNullException>(() => rules.FillObjectiveItems(null));
        }

        private sealed class LogHost : IMatchObjectiveHost
        {
            public LogHost(TextWriter log)
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
