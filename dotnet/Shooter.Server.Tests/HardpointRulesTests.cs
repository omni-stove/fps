using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class HardpointRulesTests
    {
        // TestRules.Points の拠点の中心 (A は z=-15、B は z=0、C は z=15)。体の高さ 1 は拠点の高さ 3 の内側
        private static readonly Vector3 _pointA = new Vector3(0f, 1f, -15f);
        private static readonly Vector3 _pointB = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _away = new Vector3(30f, 1f, 30f);

        private StringWriter _log;
        private MatchHost _host;
        private HardpointRules _rules;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _rules?.Stop();
            _log.Dispose();
        }

        [Test]
        public void TheObjectiveScoresByThePointAndKeepsTheSessionFromEndingFirst()
        {
            var settings = Settings(respawnSeconds: 7f);
            var rules = new HardpointRules(settings, new MatchHost(_log, TestRules.Points(controlPoints: 3)));

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Hardpoint));
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.False);
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.CurrentRoundPhase(), Is.Null);
            Assert.That(rules.Hardpoint, Is.Null);
            Assert.That(rules.RemainingSeconds, Is.Null);
            Assert.That(rules.DecideWinner(Team.Bravo), Is.EqualTo(Team.Bravo), "without a map the kill winner stands");
            Assert.That(rules.Tick(true, 1f), Is.False);
            var spec = rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(7f));
            Assert.That(spec.RespawnsByRound, Is.False);

            var snapshot = new MatchSnapshot { AlphaPoints = 4, HasActivePoint = true, ActivePointIndex = 2, ObjectiveRemainingSeconds = 9 };
            rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Hardpoint));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.HasActivePoint, Is.False);
            Assert.That(snapshot.ActivePointIndex, Is.Zero);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.Zero);
        }

        [Test]
        public void TheMapBuildsThePointsFromTheConfiguredTuningAndLogsWhenStarted()
        {
            var rules = new HardpointRules(Settings(), new MatchHost(_log, TestRules.Points(controlPoints: 3)));

            rules.UseMap(TestMaps.Data("NetTest", TestRules.Points(controlPoints: 3)), TestRules.Points(controlPoints: 3));
            rules.Start();
            _rules = rules;

            var spec = rules.Hardpoint.Spec;
            Assert.That(spec.PointCount, Is.EqualTo(3));
            Assert.That(spec.RotateSeconds, Is.EqualTo(3f));
            Assert.That(spec.PointsPerSecond, Is.EqualTo(10f));
            Assert.That(spec.ScoreLimit, Is.EqualTo(25));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(100f));
            Assert.That(_log.ToString(), Does.Contain(
                "[Hardpoint] net points=3 rotateSeconds=3 pointsPerSecond=10 scoreLimit=25 timeLimit=100 respawn=10"));
            Assert.That(rules.RemainingSeconds, Is.EqualTo(100f));
            var points = new SnapshotList<ControlPointSnapshot>(SnapshotMessage.MaxControlPoints, "control point");
            rules.FillControlPoints(points);
            Assert.That(points.Count, Is.EqualTo(3));
            Assert.That(points[2].Index, Is.EqualTo(2));
            Assert.That(points[0].HasOwner, Is.False);
            Assert.That(points[0].Contested, Is.False);
            var snapshot = default(MatchSnapshot);
            rules.FillMatch(ref snapshot);
            Assert.That(snapshot.HasActivePoint, Is.True);
            Assert.That(snapshot.ActivePointIndex, Is.Zero);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.EqualTo(3));
            Assert.That(rules.DecideWinner(Team.Alpha), Is.Null, "an even point score is a draw whoever has more kills");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void AMapWithTooFewPointsIsRefused(int controlPoints)
        {
            var rules = new HardpointRules(Settings(), new MatchHost(_log, TestRules.Points()));
            var map = TestMaps.Data("Small", TestRules.Points(controlPoints));

            var check = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, TestRules.Points(controlPoints)));
            Assert.That(check.Message, Is.EqualTo(
                "cannot change to map Small for the Hardpoint match: it has " + controlPoints + " control point(s) but needs at least 2"));
            var use = Assert.Throws<InvalidOperationException>(() => rules.UseMap(map, TestRules.Points(controlPoints)));
            Assert.That(use.Message, Is.EqualTo(
                "cannot start the Hardpoint match on map Small: it has " + controlPoints + " control point(s) but needs at least 2"));
            Assert.Throws<InvalidOperationException>(() => rules.Start(), "Start needs a map");
        }

        [Test]
        public void AMapWithMorePointsThanASnapshotCarriesIsRefused()
        {
            var rules = new HardpointRules(Settings(), new MatchHost(_log, TestRules.Points()));
            var points = TestRules.Points(SnapshotMessage.MaxControlPoints + 1);

            var e = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(TestMaps.Data("Large", points), points));

            Assert.That(e.Message, Is.EqualTo("cannot change to map Large for the Hardpoint match: it has 17 control points but at most 16 can be sent"));
            Assert.DoesNotThrow(() => rules.CheckMap(TestMaps.Data("Fits", TestRules.Points(3)), TestRules.Points(3)));
        }

        // 1 秒 A を押さえ、取り合い、Alpha が倒れて Bravo が押さえ、拠点が B に移り、B で Bravo が上限に届く
        [Test]
        public void AHardpointMatchIsPlayedToTheScoreLimit()
        {
            Begin(Settings());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            Assert.That(_host.Session.State, Is.EqualTo(GameModeState.Playing));

            _host.Place(1, _pointA);
            _host.Steps(60);

            Assert.That(_rules.Hardpoint.Status, Is.EqualTo(HardpointStatus.Held));
            Assert.That(_rules.Hardpoint.Holder, Is.EqualTo(Side.Alpha));
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] point=A status=Held holder=Alpha"));
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(10));
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(snapshot.ActivePointIndex, Is.Zero);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.EqualTo(2));
            var points = new SnapshotList<ControlPointSnapshot>(SnapshotMessage.MaxControlPoints, "control point");
            _rules.FillControlPoints(points);
            Assert.That(points[0].HasOwner, Is.True);
            Assert.That(points[0].Owner, Is.EqualTo(Team.Alpha));
            Assert.That(points[0].ControlPercent, Is.EqualTo(100));
            Assert.That(points[0].Contested, Is.False);
            Assert.That(points[1].HasOwner, Is.False);
            Assert.That(points[1].ControlPercent, Is.Zero);

            _host.Place(2, _pointA);
            _host.Steps(30);

            Assert.That(_rules.Hardpoint.Status, Is.EqualTo(HardpointStatus.Contested));
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] point=A status=Contested holder=-"));
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(10), "no one scores while the point is contested");
            _rules.FillControlPoints(points);
            Assert.That(points[0].HasOwner, Is.False);
            Assert.That(points[0].Contested, Is.True);

            Assert.That(_host.Session.ApplyDamage(2, 1, _host.Session.MaxHealth), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_rules.Hardpoint.Status, Is.EqualTo(HardpointStatus.Held), "the fallen are taken off the point at once");
            Assert.That(_rules.Hardpoint.Holder, Is.EqualTo(Side.Bravo));
            _rules.FillControlPoints(points);
            Assert.That(points[0].Owner, Is.EqualTo(Team.Bravo));
            Assert.That(points[0].ControlPercent, Is.EqualTo(-100));
            _host.Steps(1);
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] point=A status=Held holder=Bravo"));

            _host.Steps(90);

            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] moved from=A to=B"));
            Assert.That(_rules.Hardpoint.ActivePoint, Is.EqualTo(1));
            Assert.That(_rules.Hardpoint.Status, Is.EqualTo(HardpointStatus.Empty), "player 2 is still on A");
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.ActivePointIndex, Is.EqualTo(1));
            Assert.That(snapshot.BravoPoints, Is.InRange(14, 15));
            Assert.That(_host.Ended, Is.False);

            _host.Place(2, _pointB);
            _host.Steps(120);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(_host.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_rules.Hardpoint.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_rules.DecideWinner(Team.Alpha), Is.EqualTo(Team.Bravo), "the points decide, not the kills");
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(10));
            Assert.That(snapshot.BravoPoints, Is.EqualTo(25));
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] state=Ended point=B A=10 B=25 remaining="));
            Assert.That(_log.ToString(), Does.Contain(" winner=Bravo"));
        }

        [Test]
        public void TimeRunningOutGoesToTheTeamWithMorePoints()
        {
            Begin(Settings(timeLimitSeconds: 2f));
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(2, _pointA);

            _host.Steps(130);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(_rules.RemainingSeconds, Is.Zero);
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.BravoPoints, Is.InRange(19, 20));
        }

        [Test]
        public void TimeRunningOutWithEvenPointsIsADraw()
        {
            Begin(Settings(timeLimitSeconds: 1f));
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(1, _away);
            _host.Place(2, _away);
            _host.Session.ApplyDamage(1, 2, _host.Session.MaxHealth);

            _host.Steps(70);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.Null, "a kill lead does not break an even point score");
            Assert.That(_log.ToString(), Does.Contain(" winner=draw"));
        }

        [Test]
        public void TheNextMatchStartsFromThePointAAndNoPointsAndFollowsTeamChanges()
        {
            Begin(Settings(scoreLimit: 5));
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(1, _pointA);
            _host.Steps(40);
            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Alpha));

            Assert.That(_host.Session.ChangeTeam(1, Team.Bravo), Is.True);
            _rules.PlayerJoined(1);
            Assert.That(_rules.Hardpoint.Session.GetSide(1), Is.EqualTo(Side.Bravo));
            _host.Session.Restart();

            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] restart"));
            Assert.That(_rules.Hardpoint.ActivePoint, Is.Zero);
            Assert.That(_rules.Hardpoint.Session.State, Is.EqualTo(GameModeState.Waiting));
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.Zero);
        }

        [Test]
        public void APlayerWhoLeavesNoLongerHoldsThePoint()
        {
            Begin(Settings());
            _host.Join(1, Team.Alpha);
            _host.Join(2, Team.Bravo);
            _host.Place(1, _pointA);
            _host.Steps(1);
            Assert.That(_rules.Hardpoint.Holder, Is.EqualTo(Side.Alpha));

            _rules.PlayerLeft(1);

            Assert.That(_rules.Hardpoint.Session.HasPlayer(1), Is.False);
            Assert.That(_rules.Hardpoint.Status, Is.EqualTo(HardpointStatus.Empty));
            Assert.DoesNotThrow(() => _host.Steps(1), "the body left behind is not counted");
        }

        [Test]
        public void ChangingTheMapRebuildsThePointsAndKeepsTheRulesStarted()
        {
            Begin(Settings());

            _rules.UseMap(TestMaps.Data("Other", TestRules.Points(controlPoints: 2)), TestRules.Points(controlPoints: 2));

            Assert.That(_rules.Hardpoint.PointCount, Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] net points=2"));
        }

        private static NetMatchSettings Settings(
            float rotateSeconds = 3f, float pointsPerSecond = 10f, int scoreLimit = 25, float timeLimitSeconds = 100f, float respawnSeconds = 10f)
        {
            return new NetMatchSettings(hardpoint: new HardpointSpec(
                HardpointSpec.MinPointCount, rotateSeconds, pointsPerSecond, scoreLimit, timeLimitSeconds, respawnSeconds));
        }

        private void Begin(NetMatchSettings settings)
        {
            var points = TestRules.Points(controlPoints: 3);
            _host = new MatchHost(_log, points);
            _rules = new HardpointRules(settings, _host);
            _rules.UseMap(TestMaps.Data("NetTest", points), points);
            _host.Begin(_rules, Plain(), settings);
            _rules.Start();
        }

        private static GameRules Plain()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        // ServerObjectiveFactory にまだ載せていないので、MatchRulesModule が目的を回す順 (Session の Tick の後に目的の Tick、
        // 目的が終えたら DecideWinner の勝者で Session を Finish) と出来事の渡し方をここでなぞる
        private sealed class MatchHost : IMatchObjectiveHost
        {
            private const float _tickSeconds = 1f / 60f;

            private readonly NetMapPoints _points;
            private IMatchObjectiveRules _rules;
            private long _tick;

            public MatchHost(TextWriter log, NetMapPoints points)
            {
                Log = log;
                _points = points;
                Bodies = new ServerBodies(NetMotorSettings.Default.Stance);
            }

            public TeamDeathmatchSession Session { get; private set; }

            public ServerBodies Bodies { get; }

            public TextWriter Log { get; }

            public bool Ended { get; private set; }

            public Team? Winner { get; private set; }

            public void Begin(IMatchObjectiveRules rules, GameRules gameRules, NetMatchSettings settings)
            {
                _rules = rules;
                Session = new TeamDeathmatchSession(
                    rules.CreateSessionSpec(gameRules, settings),
                    settings.MaxHealth,
                    _points.SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    PositionOf,
                    null,
                    settings.ReconnectGraceSeconds);
                Session.Spawned += (playerId, point) => Bodies.Teleport(playerId, point.Position, 0f, _tick);
                Session.Killed += (killerId, victimId, _) => _rules.PlayerKilled(killerId, victimId);
                Session.Restarted += () =>
                {
                    Ended = false;
                    Winner = null;
                    _rules.MatchRestarted();
                };
                Session.Ended += winner =>
                {
                    Ended = true;
                    Winner = _rules.DecideWinner(winner);
                    _rules.MatchEnded();
                };
            }

            public void Join(int playerId, Team team)
            {
                Bodies.Add(playerId);
                Session.Join(playerId, "device" + playerId, team);
                _rules.PlayerJoined(playerId);
            }

            public void Place(int playerId, Vector3 position)
            {
                Assert.That(Bodies.TryGet(playerId, out var body), Is.True);
                body.SetPose(position, 0f, PlayerStance.Standing);
            }

            public void Steps(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _tick++;
                    Session.Tick(_tickSeconds);
                    if (_rules.Tick(Session.State == GameModeState.Playing, _tickSeconds))
                    {
                        Session.Finish(_rules.DecideWinner(Session.Winner));
                    }
                }
            }

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }

            private Vector3 PositionOf(int playerId)
            {
                return Bodies.TryGet(playerId, out var body) ? body.Position : Vector3.Zero;
            }
        }
    }
}
