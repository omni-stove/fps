using System;
using System.IO;
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

        [Test]
        public void CaptureTheFlagIsNotOfferedByTheFactoryYet()
        {
            var e = Assert.Throws<InvalidOperationException>(
                () => ServerObjectiveFactory.Create(MatchObjective.CaptureTheFlag, new NetMatchSettings(), _host));

            Assert.That(e.Message, Is.EqualTo("the dedicated server has no rules for the match objective CaptureTheFlag"));
        }

        [Test]
        public void TheFactoryNeedsSettingsAndAHost()
        {
            Assert.Throws<ArgumentNullException>(() => ServerObjectiveFactory.Create(MatchObjective.None, null, _host));
            Assert.Throws<ArgumentNullException>(() => ServerObjectiveFactory.Create(MatchObjective.None, new NetMatchSettings(), null));
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
