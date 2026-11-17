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
    // 2 つのマップの候補で起動し、次の試合でマップを替えると、出現地点・当たり判定・拠点・乗り物が新しいマップのものになることを確かめる
    public sealed class ServerMatchMapsTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const float _restartDelaySeconds = 1f;
        private const string _firstMap = "NetArena01";
        private const string _secondMap = "NetArena02";
        private const int _secondWallColliderId = 2;

        // 2 つ目のマップは床を 20m 下に置き、1 つ目のマップでは何も無い高さに壁を立てる。替わっていなければ立つ場所も当たる物も違う
        private const float _firstFloorTop = 0.5f;
        private const float _secondFloorTop = -19.5f;

        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _firstAlpha = new Vector3(-10f, _firstFloorTop + 1.1f, 0f);
        private static readonly Vector3 _firstBravo = new Vector3(10f, _firstFloorTop + 1.1f, 0f);
        private static readonly Vector3 _secondAlpha = new Vector3(-8f, _secondFloorTop + 1.1f, -6f);
        private static readonly Vector3 _secondBravo = new Vector3(8f, _secondFloorTop + 1.1f, -6f);
        private static readonly Vector3 _wallProbe = new Vector3(0f, _secondFloorTop + 2f, 0f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerVehicles _vehicles;
        private ServerCombat _combat;
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
        public void TheNextMatchSpawnsOnTheNewMapAndCollidesWithIt()
        {
            Start();
            Join(1);
            Join(2);
            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
            Assert.That(Body(1).Position, Is.EqualTo(_firstAlpha));
            Assert.That(HitsTheSecondWall(), Is.False, "the first map has nothing at the wall's height");

            EndTheMatch();
            Assert.That(_match.RequestNextMap(_secondMap), Is.True);
            Step(_restartDelaySeconds + _tickSeconds);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));
            Assert.That(_context.Map, Is.SameAs(_match.Map));
            Assert.That(Body(1).Position, Is.EqualTo(_secondAlpha));
            Assert.That(Body(2).Position, Is.EqualTo(_secondBravo));
            Assert.That(HitsTheSecondWall(), Is.True, "shots are traced against the new map");
            Assert.That(
                _context.World.Raycast(new Vector3(0f, 5f, 0f), -Vector3.UnitY, 50f, ServerHitboxes.RaycastLayers, out var floor), Is.True);
            Assert.That(floor.Point.Y, Is.EqualTo(_secondFloorTop).Within(1e-4f));

            Idle(1, 30);
            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsGrounded, Is.True, "the body stands on the new map's floor");
            Assert.That(state.Position.Y, Is.EqualTo(_secondFloorTop + 1f + CapsuleMoverSpec.DefaultSkinWidth).Within(1e-3f));
            Assert.That(state.Position.X, Is.EqualTo(_secondAlpha.X).Within(1e-3f));
        }

        [Test]
        public void ChangingTheMapIsLoggedWithTheEndedMatchId()
        {
            Start();
            Join(1);
            Join(2);
            var changed = new List<string>();
            var order = new List<string>();
            _match.MapChanged += map =>
            {
                changed.Add(map.Name);
                order.Add("map");
            };
            _match.Restarted += () => order.Add("restarted");
            EndTheMatch();
            var ended = _match.MatchId;

            _match.RequestNextMap(_secondMap);
            Step(_restartDelaySeconds + _tickSeconds);

            Assert.That(changed, Is.EqualTo(Names(_secondMap)));
            Assert.That(order, Is.EqualTo(Names("map", "restarted")));
            Assert.That(_log.ToString(), Does.Contain("[Match] map changed from=" + _firstMap + " to=" + _secondMap));
            var json = JsonOf(ServerEventLog.MapChangedType);
            Assert.That(json, Has.Count.EqualTo(1));
            Assert.That(json[0], Does.Contain("\"matchId\":\"" + ended + "\",\"fromMapId\":\"" + _firstMap + "\",\"toMapId\":\"" + _secondMap + "\""));
            Assert.That(_match.NextMap, Is.Null, "the request is used up by the match it chose");
        }

        [Test]
        public void WithoutARequestTheMapStaysTheSame()
        {
            Start();
            Join(1);
            Join(2);
            var changed = 0;
            _match.MapChanged += _ => changed++;

            EndTheMatch();
            Step(_restartDelaySeconds + _tickSeconds);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            EndTheMatch();
            _match.RequestNextMap(_firstMap);
            Step(_restartDelaySeconds + _tickSeconds);

            Assert.That(changed, Is.Zero);
            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
            Assert.That(Body(1).Position, Is.EqualTo(_firstAlpha));
            Assert.That(JsonOf(ServerEventLog.MapChangedType), Is.Empty);
            Assert.That(_log.ToString(), Does.Not.Contain("map changed"));
        }

        [Test]
        public void AMapThatWasNotLoadedCannotBeRequested()
        {
            Start();

            Assert.That(_match.RequestNextMap("NetArena09"), Is.False);
            Assert.That(_match.RequestNextMap(null), Is.False);
            Assert.That(_match.NextMap, Is.Null);
            Assert.That(_match.RequestNextMap(_secondMap), Is.True);
            Assert.That(_match.NextMap, Is.EqualTo(_secondMap));
            Assert.That(_match.RequestNextMap(_firstMap), Is.True, "the request can change until the next match starts");
            Assert.That(_match.NextMap, Is.EqualTo(_firstMap));
        }

        [Test]
        public void TheVehiclesOfTheOldMapAreLeftAndTheNewMapsAreUsed()
        {
            Start();
            Join(1);
            Join(2);
            Assert.That(_vehicles.Count, Is.EqualTo(1));
            Assert.That(_vehicles.Handle(1, VehicleAction.Board, 0, 0), Is.True);

            // 乗っている人が倒れると降りてしまうので、降ろすのがマップの替わりだと分かるよう、乗っていない側を倒して終える
            EndTheMatch(killer: 1, victim: 2);
            _match.RequestNextMap(_secondMap);
            Step(_restartDelaySeconds + _tickSeconds);

            Assert.That(_vehicles.Count, Is.Zero);
            Assert.That(_vehicles.TryGetSeat(1, out _, out _), Is.False);
            Assert.That(_movement.IsRiding(1), Is.False);
            Assert.That(JsonOf(ServerEventLog.VehicleLeftType), Has.Count.EqualTo(1));
            Assert.That(Body(1).Position, Is.EqualTo(_secondAlpha));
        }

        [Test]
        public void DominationUsesTheControlPointsOfTheNewMap()
        {
            Start(GameRules.Default.WithObjective(MatchObjective.Domination));
            Join(1);
            Join(2);
            Assert.That(_rules.Domination.Mode.PointCount, Is.EqualTo(3));

            Assert.That(_rules.Session.Finish(Team.Alpha), Is.True);
            Tick();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            _match.RequestNextMap(_secondMap);
            Step(_restartDelaySeconds + _tickSeconds);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_rules.Domination.Mode.PointCount, Is.EqualTo(1));
            Assert.That(_rules.Domination.HasPlayer(1), Is.True);
            Assert.That(_rules.Domination.HasPlayer(2), Is.True);
        }

        [Test]
        public void TheRulesRefuseToStartWhenACandidateCannotBePlayed()
        {
            var noBravo = new NetMapPoints(new[] { new SpawnPoint(_secondAlpha, Team.Alpha) }, Array.Empty<ControlPointArea>());
            var second = new ServerMapData(_secondMap, SecondWorld(), TestMaps.NavMesh(), "memory", noBravo);

            var error = Assert.Throws<InvalidOperationException>(() => Start(maps: second));

            Assert.That(error.Message, Does.Contain("cannot change to map " + _secondMap + " (1 spawn points): no spawn point is usable by team Bravo"));
        }

        [Test]
        public void DominationRefusesToStartWhenACandidateHasNoControlPoints()
        {
            var noPoints = new NetMapPoints(
                new[] { new SpawnPoint(_secondAlpha, Team.Alpha), new SpawnPoint(_secondBravo, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var second = new ServerMapData(_secondMap, SecondWorld(), TestMaps.NavMesh(), "memory", noPoints);

            var error = Assert.Throws<InvalidOperationException>(() => Start(GameRules.Default.WithObjective(MatchObjective.Domination), second));

            Assert.That(error.Message, Is.EqualTo("cannot change to map " + _secondMap + " for the Domination match: it has no control points"));
        }

        [Test]
        public void TheMapsCanBeSetOnlyOnceAndNamesMustNotRepeat()
        {
            var match = new ServerMatch(0f, _log, new ServerEventWriter(false, _json, _log), () => _time);
            var first = FirstMap();
            Assert.Throws<ArgumentException>(() => match.UseMaps(first, new[] { FirstMap() }));

            match.UseMaps(first, new[] { first, SecondMap() });

            Assert.That(match.Maps.Select(m => m.Name), Is.EqualTo(Names(_firstMap, _secondMap)));
            Assert.Throws<InvalidOperationException>(() => match.UseMaps(first));
        }

        [Test]
        public void TheContextFollowsTheMatchOnlyWhenTheMatchHasMaps()
        {
            var match = new ServerMatch(0f, _log, new ServerEventWriter(false, _json, _log), () => _time);
            var first = FirstMap();
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                first,
                match,
                new ServerMessenger(_transport),
                new StubControl());
            Assert.That(context.Map, Is.SameAs(first));

            var second = SecondMap();
            match.UseMaps(second, new[] { first });

            Assert.That(context.Map, Is.SameAs(second));
        }

        private void Start(GameRules rules = null, ServerMapData maps = null)
        {
            rules ??= GameRules.Default;
            rules = rules
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
            var settings = new NetMatchSettings(scoreLimit: 1, restartDelaySeconds: _restartDelaySeconds, mapPool: new List<string> { _firstMap, _secondMap });
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules, settings), PingSpec.Default, "memory", null);
            var first = FirstMap();
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _match.UseMaps(first, new[] { first, maps ?? SecondMap() });
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                first,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _vehicles = new ServerVehicles(_rules, _movement);
            _combat = new ServerCombat(_rules, _movement, _vehicles);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(_vehicles);
            _host.Register(_combat);
            _host.Start(_context);
        }

        private static ServerMapData FirstMap()
        {
            var points = new NetMapPoints(
                new[] { new SpawnPoint(_firstAlpha, Team.Alpha), new SpawnPoint(_firstBravo, Team.Bravo) },
                new[]
                {
                    new ControlPointArea(0, new Vector3(0f, _firstFloorTop, -15f), 4f, 3f),
                    new ControlPointArea(1, new Vector3(0f, _firstFloorTop, 0f), 4f, 3f),
                    new ControlPointArea(2, new Vector3(0f, _firstFloorTop, 15f), 4f, 3f),
                });
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            });
            var vehicles = NetMapVehicles.Parse(
                "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [-10, 0.5, 1.5], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ] } ] }",
                "memory");
            return new ServerMapData(_firstMap, world, TestMaps.NavMesh(), "memory", points, vehicles: vehicles);
        }

        private static ServerMapData SecondMap()
        {
            var points = new NetMapPoints(
                new[] { new SpawnPoint(_secondAlpha, Team.Alpha), new SpawnPoint(_secondBravo, Team.Bravo) },
                new[] { new ControlPointArea(0, new Vector3(0f, _secondFloorTop, 10f), 4f, 3f) });
            return new ServerMapData(_secondMap, SecondWorld(), TestMaps.NavMesh(), "memory", points);
        }

        private static WorldMap SecondWorld()
        {
            return new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, _secondFloorTop - 0.5f, 0f), new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
                WorldCollider.Box(
                    _secondWallColliderId, 0, WorldCollider.StaticGroup, new Vector3(0f, _secondFloorTop + 3f, 5f), new Vector3(5f, 3f, 0.25f), Quaternion.Identity),
            });
        }

        private bool HitsTheSecondWall()
        {
            var found = _combat.Hitboxes.Raycast(_wallProbe, Vector3.UnitZ, 20f, _host.TickCount, 0, _ => false, out var hit);
            return found && !hit.HitTarget && hit.ColliderId == _secondWallColliderId;
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void EndTheMatch(int killer = 2, int victim = 1)
        {
            Assert.That(_rules.ApplyDamage(killer, victim, 1000), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
        }

        private void Idle(int id, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                var input = new InputMessage();
                input.Add(new InputFrame(++_inputTick, Vector2.Zero, 0f, InputButtons.None));
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

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Names(params string[] names) => names;

        private List<string> JsonOf(string type)
        {
            return _json.ToString().Split('\n').Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal)).ToList();
        }
    }
}
