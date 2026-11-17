using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // knockback.json をルールの横に置いて読み込み、移動・ルール・戦闘を同じホストで動かして爆発で飛ぶことを確かめる
    public sealed class ServerExplosionKnockbackTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

        // 床 (y=0.5 が上面) の上の体の原点 (カプセルの中心、高さ 2)
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        // 中心から 2 m までは強さ 1、4 m で 0。倒さないようダメージは小さくする
        private static readonly ExplosionSpec _explosion = new ExplosionSpec(new ExplosionDamage(10, 4f, 2f));

        // 爆発の速さ 12・上向き 0.5・重さの係数 1、プレイヤーの重さ 2 で、強さ 1 なら 6 m/s で 45 度上へ飛ぶ
        private const string _knockbackJson =
            "{\"explosionSpeed\": 12, \"explosionUpwardRatio\": 0.5, \"explosionWeightFactor\": 1, \"playerWeight\": 2, \"gravity\": 10}";

        private TempDirectory _root;
        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private int _inputTick;

        [SetUp]
        public void SetUp()
        {
            _root = new TempDirectory();
            _log = new StringWriter();
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
            _root.Dispose();
        }

        [Test]
        public void TheKnockbackTuningIsReadBesideTheRules()
        {
            var data = Load(_knockbackJson);

            Assert.That(data.Knockback.Explosion.Speed, Is.EqualTo(12f));
            Assert.That(data.Knockback.Explosion.UpwardRatio, Is.EqualTo(0.5f));
            Assert.That(data.Knockback.PlayerWeight, Is.EqualTo(2f));
            Assert.That(data.Knockback.Gravity, Is.EqualTo(10f));
            Assert.That(data.Knockback.MinAirSeconds, Is.EqualTo(KnockbackSpec.DefaultMinAirSeconds), "unwritten items keep the defaults");
            Assert.That(data.KnockbackPath, Does.EndWith(ServerRulesData.KnockbackTuningFileName));
            Assert.That(data.LogLine, Does.Contain(" knockback=" + data.KnockbackPath + " "));
        }

        [Test]
        public void WithoutTheKnockbackTuningTheDefaultsAreUsed()
        {
            TestRules.Write(_root.Path, "TeamDeathmatch");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", new[] { _root.Path }, out var data, out var error), Is.True, error);

            Assert.That(data.Knockback, Is.SameAs(KnockbackSpec.Default));
            Assert.That(data.KnockbackPath, Is.Null);
        }

        [Test]
        public void BrokenKnockbackTuningStopsTheLoad()
        {
            TestRules.Write(_root.Path, "TeamDeathmatch");
            WriteKnockback("{\"explosionUpwardRatio\": 2}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", new[] { _root.Path }, out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("cannot read knockback tuning: ").And.Contain("explosionUpwardRatio"));
        }

        [Test]
        public void APlayerNextToAnExplosionFliesAtTheTunedSpeed()
        {
            Start(Load(_knockbackJson));
            var victim = Body(2);
            var center = victim.Position + new Vector3(-(victim.Radius + 0.5f), 0f, 0f);

            _combat.Explode(center, _explosion, 1, ServerCombat.GrenadeWeaponId);

            Assert.That(_movement.TryGetState(2, out var state), Is.True);
            Assert.That(state.IsKnockedBack, Is.True, _log.ToString());
            var expected = 6f * MathF.Sqrt(0.5f);
            Assert.That(state.Knockback.Velocity.X, Is.EqualTo(expected).Within(1e-4f));
            Assert.That(state.Knockback.Velocity.Y, Is.EqualTo(expected).Within(1e-4f));
            Assert.That(state.Knockback.Velocity.Z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(state.Knockback.ElapsedSeconds, Is.EqualTo(0f));
            Assert.That(_log.ToString(), Does.Contain("[Net] knockback owner=2 tick=" + _inputTick + " launched velocity=(4.2, 4.2, 0.0)"));
        }

        [Test]
        public void TheOwnerSnapshotCarriesTheFlightSoThePredictionCanReplayIt()
        {
            Start(Load(_knockbackJson));
            var victim = Body(2);
            _combat.Explode(victim.Position + new Vector3(-(victim.Radius + 0.5f), 0f, 0f), _explosion, 1, ServerCombat.GrenadeWeaponId);

            var own = Snapshot(2);
            var other = Snapshot(1);

            Assert.That(_movement.TryGetState(2, out var state), Is.True);
            Assert.That(own.Owner.Knockback, Is.EqualTo(state.Knockback));
            Assert.That(own.Owner.LastProcessedInputTick, Is.EqualTo(_inputTick));
            Assert.That(other.Owner.Knockback.IsFlying, Is.False, "only the one who flies is told");
        }

        [Test]
        public void TheFlightCarriesThePlayerAwayAndEndsOnLanding()
        {
            Start(Load(_knockbackJson));
            var victim = Body(2);
            var start = victim.Position;
            _combat.Explode(start + new Vector3(-(victim.Radius + 0.5f), 0f, 0f), _explosion, 1, ServerCombat.GrenadeWeaponId);

            Idle(2, 10);
            Assert.That(_movement.TryGetState(2, out var rising), Is.True);
            Assert.That(rising.IsKnockedBack, Is.True);
            Assert.That(rising.Position.Y, Is.GreaterThan(start.Y + 0.1f), "it rises off the floor");
            Assert.That(rising.Position.X, Is.GreaterThan(start.X + 0.5f), "away from the explosion");

            // 6 m/s を 45 度上へ、重力 10 なら約 0.85 秒で床に戻り、横へ約 3.6 m 進む (入力が無いので空中で寄せない)
            Idle(2, 90);
            Assert.That(_movement.TryGetState(2, out var landed), Is.True);
            Assert.That(landed.IsKnockedBack, Is.False, _log.ToString());
            Assert.That(landed.IsGrounded, Is.True);
            Assert.That(landed.Position.X - start.X, Is.EqualTo(6f * MathF.Sqrt(0.5f) * (2f * 6f * MathF.Sqrt(0.5f) / 10f)).Within(0.3f));
            Assert.That(_log.ToString(), Does.Contain("[Net] knockback owner=2 tick=").And.Contain(" landed at="));
        }

        [Test]
        public void TheEdgeOfTheBlastPushesLessThanTheMinimumAndDoesNotLaunch()
        {
            Start(Load(_knockbackJson));
            var victim = Body(2);

            // 3.6 m は強さ 0.2 で 1.2 m/s。最低の速さ (1.5 m/s) に届かない
            _combat.Explode(victim.Position + new Vector3(-(victim.Radius + 3.6f), 0f, 0f), _explosion, 1, ServerCombat.GrenadeWeaponId);

            Assert.That(_rules.Session.GetHealth(2), Is.LessThan(NetMatchSettings.DefaultMaxHealth), "it still hurts");
            Assert.That(_movement.TryGetState(2, out var state), Is.True);
            Assert.That(state.IsKnockedBack, Is.False);
        }

        [Test]
        public void AnExplosionThatKillsDoesNotLaunchTheBody()
        {
            Start(Load(_knockbackJson));
            var victim = Body(2);

            _combat.Explode(
                victim.Position + new Vector3(-(victim.Radius + 0.5f), 0f, 0f),
                new ExplosionSpec(new ExplosionDamage(NetMatchSettings.DefaultMaxHealth * 2, 4f, 2f)),
                1,
                ServerCombat.GrenadeWeaponId);

            Assert.That(_rules.Session.IsAlive(2), Is.False);
            Assert.That(_movement.TryGetState(2, out var state), Is.True);
            Assert.That(state.IsKnockedBack, Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] knockback owner=2"));
        }

        private ServerRulesData Load(string knockbackJson)
        {
            TestRules.Write(_root.Path, "TeamDeathmatch", TestRules.File(Rules()));
            WriteKnockback(knockbackJson);
            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", new[] { _root.Path }, out var data, out var error), Is.True, error);
            return data;
        }

        private void WriteKnockback(string json)
        {
            var tuning = Path.Combine(_root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.KnockbackTuningFileName), json);
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        // 2 人を入れ、床に足が着くまで何もしない入力で落ち着かせる
        private void Start(ServerRulesData data)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(false, TextWriter.Null, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, _movement);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Start(_context);

            Join(1);
            Join(2);
            Idle(2, 30);
            Assert.That(_movement.TryGetState(2, out var settled), Is.True);
            Assert.That(settled.IsGrounded, Is.True, "the victim stands on the floor before the blast");
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
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
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }

        private SnapshotMessage Snapshot(int receiverId)
        {
            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(receiverId, out var receiver), Is.True);
            _movement.Contribute(snapshot, receiver);
            return snapshot;
        }
    }
}
