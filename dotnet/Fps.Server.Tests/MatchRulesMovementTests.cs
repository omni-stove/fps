using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // ルールと移動を同じホストで動かし、ルールが判定に使う位置が移動の結果と同じになることを確かめる
    public sealed class MatchRulesMovementTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

        // 床 (TestMaps の箱) の上面は y=0.5、体の原点はカプセルの中心 (高さ 2) なので、足を床に付けるには y=1.5 より少し上に置く
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerSnapshots _snapshots;
        private int _inputTick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _inputTick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void JoiningPlacesTheMoverAtTheSpawnPointAndMovingUpdatesTheSharedBody()
        {
            Start(Rules());
            Join(1);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.Position, Is.EqualTo(_alphaSpawn));

            Walk(1, forward: true, ticks: 60);

            Assert.That(_movement.TryGetState(1, out state), Is.True);
            Assert.That(state.Position.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed).Within(0.5f));
            Assert.That(_context.Bodies.TryGet(1, out var body), Is.True);
            Assert.That(body.Position, Is.EqualTo(state.Position));
        }

        [Test]
        public void RespawnMovesTheMoverBack()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Walk(1, forward: true, ticks: 60);

            _rules.ApplyDamage(2, 1, 1000);
            Step(NetMatchSettings.DefaultRespawnSeconds + 0.2f);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(Vector3.Distance(state.Position, _alphaSpawn), Is.LessThan(0.2f));
        }

        [Test]
        public void DownedPlayersCannotWalkAndARevivedOneCanAgain()
        {
            Start(Rules(downed: true));
            Join(1);
            Join(2);
            Join(3);
            _rules.ApplyDamage(2, 1, 1000);
            Assert.That(_context.Bodies.TryGet(1, out var body) && body.IsDowned, Is.True);
            _movement.TryGetState(1, out var before);

            Walk(1, forward: true, ticks: 30);

            _movement.TryGetState(1, out var after);
            Assert.That(after.Position.Z, Is.EqualTo(before.Position.Z).Within(1e-3f));
        }

        [Test]
        public void ReviveUsesThePositionsTheReviverWalkedTo()
        {
            Start(Rules(downed: true));
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            _rules.ApplyDamage(2, 1, 1000);

            Walk(3, forward: true, ticks: 60);
            _rules.OnMessage(new ConnectionId(3), new ReviveRequestMessage { TargetPlayerId = 1 });
            Assert.That(_log.ToString(), Does.Contain("[Net] rejected revive reviver=3 target=1 reason=TooFar"));

            Walk(3, forward: false, ticks: 60);
            for (var i = 0; i < 80; i++)
            {
                _host.OnMessage(new ConnectionId(3), new ReviveRequestMessage { TargetPlayerId = 1 });
                Tick();
            }

            Assert.That(_log.ToString(), Does.Contain("[Net] revived reviver=3 target=1"));
            Assert.That(_context.Bodies.TryGet(1, out var body) && body.IsDowned, Is.False);
        }

        [Test]
        public void PingFindsTheEnemyWhereItWalkedTo()
        {
            Start(Rules(ping: true));
            Join(1);
            Join(2);
            Walk(2, forward: true, ticks: 36);
            Assert.That(_context.Bodies.TryGet(1, out var pinger), Is.True);
            Assert.That(_context.Bodies.TryGet(2, out var enemy), Is.True);
            Assert.That(enemy.Position.Z, Is.GreaterThan(2.5f));

            var toOldSpot = new Vector3(_bravoSpawn.X, 0.5f, _bravoSpawn.Z) - pinger.EyePosition;
            Assert.That(_rules.ServerPing(1, toOldSpot), Is.EqualTo(PingRefusal.None));
            Step(1f);
            Assert.That(_rules.ServerPing(1, enemy.Center - pinger.EyePosition), Is.EqualTo(PingRefusal.None));

            var placed = _json.ToString().Split('\n').Where(line => line.Contains("\"type\":\"ping_placed\"", StringComparison.Ordinal)).ToList();
            Assert.That(placed, Has.Count.EqualTo(2));
            Assert.That(placed[0], Does.Not.Contain("\"kind\":\"Enemy\""));
            Assert.That(placed[1], Does.Contain("\"kind\":\"Enemy\"").And.Contain("\"targetId\":\"2\""));
        }

        [Test]
        public void SnapshotsCarryTheRulesStateAndTheMovementState()
        {
            Start(Rules().WithObjective(MatchObjective.Domination), controlPoints: true);
            Join(1);
            Join(2);
            _rules.ApplyDamage(1, 2, 30);
            Walk(1, forward: true, ticks: 30);

            var snapshot = new SnapshotMessage();
            snapshot.Players.Add().PlayerId = 1;
            snapshot.Players.Add().PlayerId = 2;
            Assert.That(_match.TryGetPlayer(1, out var receiver), Is.True);
            _movement.Contribute(snapshot, receiver);
            _rules.Contribute(snapshot, receiver);

            Assert.That(snapshot.Match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(snapshot.Match.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(snapshot.Match.RemainingSeconds, Is.GreaterThan(0));
            Assert.That(snapshot.Players[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.Players[0].Position.Z, Is.GreaterThan(1f));
            Assert.That(snapshot.Players[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(snapshot.Players[1].Health, Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30));
            Assert.That(snapshot.ControlPoints.Count, Is.EqualTo(1));

            _snapshots.Tick(_host.TickCount + 1, _tickSeconds);
            Assert.That(_transport.Sent.Count(sent => sent.Connection.Value == 1), Is.GreaterThan(0));
        }

        private static GameRules Rules(bool downed = false, bool ping = false)
        {
            var actions = ActionRules.Default
                .WithDowned(downed ? Downed.Revive : Downed.Off)
                .WithReviveSeconds(1f)
                .WithPing(ping ? Ping.On : Ping.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(GameRules rules, bool controlPoints = false)
        {
            var points = new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                controlPoints ? new[] { new ControlPointArea(0, new Vector3(0f, 0.5f, 8f), 2f, 3f) } : Array.Empty<ControlPointArea>());
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null);
            var settings = ServerModules.MotorSettingsFrom(data);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", points),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(settings);
            _rules = new MatchRulesModule();
            _snapshots = new ServerSnapshots(rules, new ISnapshotContributor[] { _movement, _rules });
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(_snapshots);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private void Walk(int id, bool forward, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                var input = new InputMessage();
                input.Add(new InputFrame(++_inputTick, new Vector2(0f, forward ? 1f : -1f), 0f, InputButtons.None));
                _host.OnMessage(new ConnectionId((ulong)id), input);
                Tick();
            }
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }
    }
}
