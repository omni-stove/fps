using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesHardpointTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        // TestRules.Points の拠点 A の中心 (z=-15)。体の高さ 1 は拠点の高さ 3 の内側
        private static readonly Vector3 _pointA = new Vector3(0f, 1f, -15f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
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
        public void HoldingThePointPlaysTheMatchToTheScoreLimit()
        {
            Start(Settings(scoreLimit: 10), Map("NetTest", 3));
            Join(1);
            Join(2);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(_module.Objective, Is.TypeOf<HardpointRules>());
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] net points=3 rotateSeconds=3 pointsPerSecond=10 scoreLimit=10 timeLimit=100"));
            Step();

            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Hardpoint));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(100), "the time comes from the Hardpoint, not the unreachable Session limit");
            Assert.That(snapshot.HasActivePoint, Is.True);
            Assert.That(snapshot.ActivePointIndex, Is.Zero);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.EqualTo(3));

            Place(1, _pointA);
            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(10));
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Alpha\""));
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] state=Ended point=A A=10 B=0 remaining="));
        }

        [Test]
        public void TimeUpGoesToTheTeamWithMorePoints()
        {
            Start(Settings(timeLimitSeconds: 2f), Map("NetTest", 3));
            Join(1);
            Join(2);
            Step();
            Assert.That(Match().RemainingSeconds, Is.EqualTo(2));

            Place(2, _pointA);
            Advance(2.2f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(snapshot.RemainingSeconds, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.InRange(19, 20));
        }

        [Test]
        public void APlayerBalancedToTheOtherTeamHoldsThePointForItInTheNextMatch()
        {
            Start(Settings(scoreLimit: 5), Map("NetTest", 3), maxTeamSizeDifference: 1);
            for (var id = 1; id <= 8; id++)
            {
                Join(id);
            }

            // 奇数が Alpha、偶数が Bravo に入る。Bravo を 2 だけ残して 4 対 1 にする
            Leave(4);
            Leave(6);
            Leave(8);
            Step();
            Place(1, _pointA);
            Advance(0.7f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Match().Winner, Is.EqualTo(Team.Alpha));

            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(TeamOf(7), Is.EqualTo(Team.Bravo));
            var rules = (HardpointRules)_module.Objective;
            Assert.That(rules.Hardpoint.Session.GetSide(7), Is.EqualTo(Side.Bravo));
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] restart"));
            Assert.That(Match().AlphaPoints, Is.Zero);

            Place(7, _pointA);
            Step();

            Assert.That(rules.Hardpoint.Status, Is.EqualTo(HardpointStatus.Held));
            Assert.That(rules.Hardpoint.Holder, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void TheNextMapRebuildsThePointsAndThePlayersPlayOnThem()
        {
            Start(Settings(scoreLimit: 5), Map("NetTest", 3), Map("Other", 2));
            Join(1);
            Join(2);
            Assert.That(_match.RequestNextMap("Other"), Is.True);
            Step();
            Place(1, _pointA);
            Advance(0.7f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));

            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo("Other"));
            var rules = (HardpointRules)_module.Objective;
            Assert.That(rules.Hardpoint.PointCount, Is.EqualTo(2));
            Assert.That(rules.Hardpoint.Session.HasPlayer(1), Is.True);
            Assert.That(rules.Hardpoint.Session.HasPlayer(2), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Hardpoint] net points=2"));
            var points = new SnapshotMessage();
            _module.FillControlPoints(points.ControlPoints);
            Assert.That(points.ControlPoints.Count, Is.EqualTo(2));

            Place(2, _pointA);
            Advance(0.7f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = Match();
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(snapshot.BravoPoints, Is.EqualTo(5));
        }

        [Test]
        public void AnotherMapWithTooFewPointsIsRefusedAtStart()
        {
            var e = Assert.Throws<InvalidOperationException>(() => Start(Settings(), Map("NetTest", 3), Map("Small", 1)));

            Assert.That(e.Message, Is.EqualTo("cannot change to map Small for the Hardpoint match: it has 1 control point(s) but needs at least 2"));
        }

        private static NetMatchSettings Settings(int scoreLimit = 25, float timeLimitSeconds = 100f)
        {
            return new NetMatchSettings(
                restartDelaySeconds: 1f,
                hardpoint: new HardpointSpec(HardpointSpec.MinPointCount, 3f, 10f, scoreLimit, timeLimitSeconds, 0.5f));
        }

        private static ServerMapData Map(string name, int controlPoints)
        {
            return TestMaps.Data(name, TestRules.Points(controlPoints));
        }

        private static GameRules Rules()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(MatchObjective.Hardpoint);
        }

        private void Start(NetMatchSettings settings, ServerMapData current, ServerMapData other = null, int maxTeamSizeDifference = 0)
        {
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time, maxTeamSizeDifference);
            _match.UseMaps(current, other != null ? new[] { current, other } : null);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                current,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(Rules(), settings));
            var module = new MatchRulesModule((objective, s, host) => objective == MatchObjective.Hardpoint
                ? new HardpointRules(s, host)
                : ServerObjectiveFactory.Create(objective, s, host));
            module.Start(_context);
            _module = module;
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private void Leave(int id)
        {
            _match.Leave(new ConnectionId((ulong)id));
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private void Place(int id, Vector3 position)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            body.SetPose(position, 0f, PlayerStance.Standing);
        }

        private MatchSnapshot Match()
        {
            var snapshot = default(MatchSnapshot);
            _module.FillMatch(ref snapshot);
            return snapshot;
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
