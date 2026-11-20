using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // ルール・移動・スナップショットを同じホストで動かし、受け取る人のチームから見えない敵の行だけが隠れることを確かめる
    public sealed class ServerSnapshotsConcealTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _weaponId = "rifle";
        private static readonly int[] _allPlayerIds = { 1, 2, 3 };
        private static readonly DateTime _time = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

        // 床 (上面 y=0.5) の上に立つ位置。Alpha と Bravo の間 (x=0) に壁を立てると、互いの目から体が隠れる
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly Vector3 _velocity = new Vector3(1f, 0f, 2f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
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
        public void AnEnemyBehindAWallIsConcealedWithItsMotionEmptied()
        {
            Start(wall: true, conceal: true);
            JoinAll();

            Tick();
            var rows = LatestRows(1);

            Assert.That(rows.Keys, Is.EquivalentTo(_allPlayerIds));
            var enemy = rows[2];
            Assert.That(enemy.Concealed, Is.True);
            Assert.That(enemy.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(enemy.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(enemy.Yaw, Is.Zero);
            Assert.That(enemy.IsGrounded, Is.False);
            Assert.That(enemy.IsCrouched, Is.False);
            Assert.That(enemy.WeaponId, Is.Empty);
            Assert.That(enemy.Team, Is.EqualTo(Team.Bravo));
            Assert.That(enemy.Health, Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void TheReceiverAndAlliesAreNeverConcealed()
        {
            Start(wall: true, conceal: true);
            JoinAll();

            Tick();
            var rows = LatestRows(1);

            foreach (var id in new[] { 1, 3 })
            {
                Assert.That(rows[id].Concealed, Is.False, "player " + id);
                Assert.That(rows[id].Position, Is.EqualTo(PositionOf(id)), "player " + id);
                Assert.That(rows[id].Velocity, Is.EqualTo(_velocity), "player " + id);
                Assert.That(rows[id].WeaponId, Is.EqualTo(_weaponId), "player " + id);
            }
        }

        [Test]
        public void TheEnemyTeamDoesNotSeeThroughTheWallEither()
        {
            Start(wall: true, conceal: true);
            JoinAll();

            Tick();
            var rows = LatestRows(2);

            Assert.That(rows[1].Concealed, Is.True);
            Assert.That(rows[3].Concealed, Is.True);
            Assert.That(rows[1].Position, Is.EqualTo(Vector3.Zero));
            Assert.That(rows[2].Concealed, Is.False);
            Assert.That(rows[2].Position, Is.EqualTo(PositionOf(2)));
        }

        [Test]
        public void AnEnemyInTheOpenIsSentAsIs()
        {
            Start(wall: false, conceal: true);
            JoinAll();

            Tick();
            var enemy = LatestRows(1)[2];

            Assert.That(enemy.Concealed, Is.False);
            Assert.That(enemy.Position, Is.EqualTo(PositionOf(2)));
            Assert.That(enemy.Velocity, Is.EqualTo(_velocity));
            Assert.That(enemy.WeaponId, Is.EqualTo(_weaponId));
        }

        [Test]
        public void TurnedOffConcealingLeavesAnEnemyBehindAWallAsIs()
        {
            Start(wall: true, conceal: false);
            JoinAll();

            Tick();
            var enemy = LatestRows(1)[2];

            Assert.That(enemy.Concealed, Is.False);
            Assert.That(enemy.Position, Is.EqualTo(PositionOf(2)));
            Assert.That(enemy.Velocity, Is.EqualTo(_velocity));
            Assert.That(enemy.WeaponId, Is.EqualTo(_weaponId));
        }

        [Test]
        public void AConcealedEnemyKeepsItsScoreAndDeath()
        {
            Start(wall: true, conceal: true);
            JoinAll();

            Assert.That(_rules.ApplyDamage(1, 2, 1000), Is.EqualTo(DamageOutcome.Killed));
            _transport.Sent.Clear();
            Tick();
            var rows = LatestRows(1);

            Assert.That(rows[2].Concealed, Is.True);
            Assert.That(rows[2].Health, Is.Zero);
            Assert.That(rows[2].Deaths, Is.EqualTo(1));
            Assert.That(rows[2].HasKiller, Is.True);
            Assert.That(rows[2].KillerId, Is.EqualTo(1));
            Assert.That(rows[1].Kills, Is.EqualTo(1));
        }

        [Test]
        public void ConcealingNeedsTheRulesModule()
        {
            Assert.Throws<ArgumentException>(
                () => new ServerSnapshots(GameRules.Default, Array.Empty<ISnapshotContributor>(), Tuning(true)));
            Assert.DoesNotThrow(() => new ServerSnapshots(GameRules.Default, Array.Empty<ISnapshotContributor>(), Tuning(false)));
        }

        private static AntiCheatTuning Tuning(bool conceal)
        {
            var defaults = AntiCheatTuning.Default;
            return new AntiCheatTuning(
                new AntiCheatTuning.ConcealSettings(
                    conceal,
                    AntiCheatTuning.ConcealSettings.DefaultMaxDistance,
                    AntiCheatTuning.ConcealSettings.DefaultMarginDistance,
                    AntiCheatTuning.ConcealSettings.DefaultMarginSeconds),
                defaults.Violations,
                defaults.Stats,
                defaults.Evidence);
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off);
            return GameRules.Default
                .WithActions(actions)
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        private static WorldMap World(bool wall)
        {
            var floor = WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(10f, 0.5f, 10f), Quaternion.Identity);
            if (!wall)
            {
                return new WorldMap(new[] { floor });
            }

            // 横と上の余裕 (1.5) を足しても越えられない広さと高さにする
            var between = WorldCollider.Box(
                2, 0, WorldCollider.StaticGroup, new Vector3(0f, 5f, 0f), new Vector3(0.2f, 4.5f, 9.5f), Quaternion.Identity);
            return new WorldMap(new[] { floor, between });
        }

        private void Start(bool wall, bool conceal)
        {
            var rules = Rules();
            var points = new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                Array.Empty<ControlPointArea>());
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null);
            var settings = ServerModules.MotorSettingsFrom(data);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                new ServerMapData("NetTest", World(wall), TestMaps.NavMesh(), "memory", points),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(settings);
            _rules = new MatchRulesModule();
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(new ServerSnapshots(
                rules, new ISnapshotContributor[] { _movement, _rules, new MotionWriter() }, Tuning(conceal), _rules));
            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる。1 と 3 が Alpha、2 が Bravo に入る
        private void JoinAll()
        {
            foreach (var id in _allPlayerIds)
            {
                var connection = new ConnectionId((ulong)id);
                _match.Join(connection, "device" + id);
                _host.OnConnected(connection);
            }

            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private Vector3 PositionOf(int id)
        {
            Assert.That(_movement.TryGetState(id, out var state), Is.True);
            return state.Position;
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }

        // 表は読むたびに同じスナップショットを上書きするので、読んだその場で行を写す
        private Dictionary<int, PlayerSnapshot> LatestRows(int id)
        {
            var rows = new Dictionary<int, PlayerSnapshot>();
            var snapshots = _transport.Read<SnapshotMessage>(id, snapshot =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                rows.Clear();
                for (var i = 0; i < snapshot.Players.Count; i++)
                {
                    rows[snapshot.Players[i].PlayerId] = snapshot.Players[i];
                }

                return snapshot;
            });
            Assert.That(snapshots, Is.Not.Empty);
            return rows;
        }

        // 隠す処理がどのコントリビューターより後に動くことを確かめるため、移動のモジュールが書かない速度と、戦闘が書く武器を書いておく
        private sealed class MotionWriter : ISnapshotContributor
        {
            public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
            {
                for (var i = 0; i < snapshot.Players.Count; i++)
                {
                    ref var row = ref snapshot.Players[i];
                    row.Velocity = _velocity;
                    row.WeaponId = _weaponId;
                }
            }
        }
    }
}
