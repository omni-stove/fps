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
    public sealed class MatchRulesPayloadTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

        // 台車の 1m 横・高さ 1m (TestRules.PayloadPoints の範囲は半径 3m・高さ 4m)
        private static readonly Vector3 _besideTheCart = new Vector3(1f, 1f, 0f);
        private static readonly Vector3 _away = new Vector3(30f, 1f, 30f);
        private static readonly float[] _sameSpeedForAnyPushers = { 1f };

        private readonly List<int> _following = new List<int>();
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
            _following.Clear();
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
        public void PushingTheCartToTheEndWinsForTheAttackers()
        {
            Start(Settings(), Routed("NetArena01"));
            Join(1);
            Join(2);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(_module.Objective, Is.TypeOf<PayloadRules>());
            Assert.That(_log.ToString(), Does.Contain("[Payload] net attackers=Alpha length=20.0m checkpoints=1 radius=3 cartSpeed=5"));
            Place(2, _away);
            Step();

            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Payload));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(30), "the time comes from the Payload, not the unreachable Session limit");
            Assert.That(snapshot.PayloadProgress, Is.Zero);
            var message = new SnapshotMessage();
            _module.FillObjectiveItems(message.ObjectiveItems);
            Assert.That(message.ObjectiveItems.Count, Is.EqualTo(1));
            Assert.That(message.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.Cart));
            Assert.That(message.ObjectiveItems[0].Position, Is.EqualTo(new Vector3(0f, 0f, -10f)));

            _following.Add(1);
            Advance(1f);

            snapshot = Match();
            Assert.That(snapshot.PayloadProgress, Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(snapshot.PayloadPushers, Is.EqualTo(1));
            Assert.That(snapshot.PayloadCheckpointsPassed, Is.Zero);
            _module.FillObjectiveItems(message.ObjectiveItems);
            Assert.That(message.ObjectiveItems[0].Position.Z, Is.EqualTo(-5f).Within(0.1f));

            Advance(1.1f);

            snapshot = Match();
            Assert.That(snapshot.PayloadCheckpointsPassed, Is.EqualTo(1));
            Assert.That(snapshot.RemainingSeconds, Is.GreaterThan(30), "the checkpoint adds 10 s");
            Assert.That(_log.ToString(), Does.Contain("[Payload] checkpoint=1/1 progress=0.50 bonus=10.0s"));

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.PayloadProgress, Is.EqualTo(1f));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Alpha\""));
            Assert.That(_log.ToString(), Does.Contain("[Payload] state=Ended delivered=true progress=1.00 checkpoints=1/1"));
        }

        [Test]
        public void HoldingTheCartUntilTimeUpWinsForTheDefenders()
        {
            Start(Settings(timeLimitSeconds: 2f), Routed("NetArena01"));
            Join(1);
            Join(2);
            Step();
            Assert.That(Match().RemainingSeconds, Is.EqualTo(2));

            _following.Add(1);
            _following.Add(2);
            Advance(2.2f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = Match();
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(snapshot.RemainingSeconds, Is.Zero);
            Assert.That(snapshot.PayloadProgress, Is.Zero, "a defender beside the cart keeps it from moving");
            Assert.That(_log.ToString(), Does.Contain("[Payload] cart=contested pushers=1 defenders=1"));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Bravo\""));
        }

        [Test]
        public void TheNextMatchStartsTheCartFromTheBeginning()
        {
            Start(Settings(timeLimitSeconds: 1f), Routed("NetArena01"));
            Join(1);
            Join(2);
            Step();
            Place(2, _away);
            _following.Add(1);
            Advance(1.1f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Match().PayloadProgress, Is.GreaterThan(0f));
            _following.Clear();

            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_log.ToString(), Does.Contain("[Payload] restart"));
            var rules = (PayloadRules)_module.Objective;
            Assert.That(rules.Payload.Distance, Is.Zero);
            Assert.That(rules.Payload.Session.HasPlayer(1), Is.True);
            Assert.That(rules.Payload.Session.HasPlayer(2), Is.True);
            Assert.That(Match().PayloadProgress, Is.Zero);
        }

        [Test]
        public void TheNextMapRebuildsTheCartOnItsRoute()
        {
            var other = new NetMapPoints(
                TestRules.Points().SpawnPoints,
                Array.Empty<ControlPointArea>(),
                null,
                new NetMapPayload(Team.Alpha, new[] { new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 30f) }, null, 3f, 4f));
            Start(Settings(timeLimitSeconds: 1f), Routed("NetArena01"), TestMaps.Data("Other", other));
            Join(1);
            Join(2);
            Assert.That(_match.RequestNextMap("Other"), Is.True);
            Step();
            Advance(1.1f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));

            Advance(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo("Other"));
            var rules = (PayloadRules)_module.Objective;
            Assert.That(rules.Payload.Route.Length, Is.EqualTo(30d));
            Assert.That(rules.CartPosition, Is.EqualTo(new Vector3(5f, 0f, 0f)));
            Assert.That(rules.Payload.Session.HasPlayer(1), Is.True);
            Assert.That(rules.Payload.Session.HasPlayer(2), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Payload] net attackers=Alpha length=30.0m checkpoints=0"));
        }

        [Test]
        public void AMapWithoutARouteIsRefusedAtStart()
        {
            var e = Assert.Throws<InvalidOperationException>(() => Start(Settings(), TestMaps.Data("NetTest", TestRules.Points(3))));

            Assert.That(e.Message, Is.EqualTo(
                "cannot start the Payload match on map NetTest: it has no payload route; "
                + "place a PayloadCart with its route in the scene and export the map points with make export-server-rules"));
        }

        [Test]
        public void AnotherMapWithoutARouteIsRefusedAtStart()
        {
            var e = Assert.Throws<InvalidOperationException>(
                () => Start(Settings(), Routed("NetArena01"), TestMaps.Data("NetTest", TestRules.Points(3))));

            Assert.That(e.Message, Does.StartWith("cannot change to map NetTest for the Payload match: it has no payload route"));
        }

        private static NetMatchSettings Settings(float timeLimitSeconds = 30f)
        {
            return new NetMatchSettings(
                restartDelaySeconds: 1f,
                respawnSeconds: 0.5f,
                payload: new PayloadSpec(5f, 1, _sameSpeedForAnyPushers, 1f, 2f, 10f, timeLimitSeconds));
        }

        private static ServerMapData Routed(string name)
        {
            return TestMaps.Data(name, TestRules.PayloadPoints());
        }

        private static GameRules Rules()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(MatchObjective.Payload);
        }

        private void Start(NetMatchSettings settings, ServerMapData current, ServerMapData other = null)
        {
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time);
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
            var module = new MatchRulesModule();
            module.Start(_context);
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

        // 台車について歩く人は、毎 tick 台車の横に立たせてから進める
        private void Step()
        {
            var rules = (PayloadRules)_module.Objective;
            foreach (var id in _following)
            {
                Place(id, rules.CartPosition.Value + _besideTheCart);
            }

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
