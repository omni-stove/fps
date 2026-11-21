using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動のモジュールに入力を絞らせ、走り続けると尽きて走れなくなること、よろけると遅く歩くこと、自分の値がスナップショットで届くことを確かめる
    public sealed class ServerPlayerStatusTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _settleTicks = 30;

        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);
        private static readonly ConnectionId _connection = new ConnectionId(1);
        private static readonly Vector2 _forward = new Vector2(0f, 1f);

        // 毎秒 20 減るので 0.5 秒で尽き、戻りは 0.5 秒待ってから毎秒 10
        private static readonly StaminaSpec _smallStamina = new StaminaSpec(
            max: 10f, sprintCostPerSecond: 20f, slideCost: 5f, dodgeCost: 8f, regenPerSecond: 10f, regenDelaySeconds: 0.5f, recoverAmount: 5f);

        private readonly MessageTable _clientTable = MatchMessages.CreateClientTable();

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMovement _movement;
        private ServerPlayerStatus _status;
        private ServerContext _context;
        private int _nextInputTick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport?.Dispose();
            _log.Dispose();
        }

        [Test]
        public void SprintingUntilStaminaRunsOutSlowsThePlayerToAWalkAndTheOwnerIsTold()
        {
            Start(On());
            Settle();

            for (var i = 0; i < 20; i++)
            {
                Send(_forward, InputButtons.Sprint);
            }

            _movement.TryGetState(1, out var sprinting);
            Assert.That(sprinting.Velocity.Z, Is.EqualTo(MotorSpec.DefaultSprintSpeed).Within(0.01f), "the player sprints while stamina lasts");
            Assert.That(LastOwner().Stamina.Value.Depleted, Is.False);

            for (var i = 0; i < 20; i++)
            {
                Send(_forward, InputButtons.Sprint);
            }

            _movement.TryGetState(1, out var walking);
            Assert.That(walking.Velocity.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed).Within(0.01f), _log.ToString());
            var owner = LastOwner();
            Assert.That(owner.Stamina.Value.Depleted, Is.True);
            Assert.That(owner.Stamina.Value.Current, Is.Zero);
            Assert.That(owner.Stamina.Value.Max, Is.EqualTo(10f));
            Assert.That(_log.ToString(), Does.Contain("[Stamina] depleted player=1 current=0.00 max=10.00"));
        }

        [Test]
        public void StaminaComesBackAndThePlayerSprintsAgain()
        {
            Start(On());
            Settle();
            for (var i = 0; i < 40; i++)
            {
                Send(_forward, InputButtons.Sprint);
            }

            // 0.5 秒待ってから毎秒 10 戻り、5 で走れるようになるので、1 秒と少しで戻る
            for (var i = 0; i < 70; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }

            Assert.That(_log.ToString(), Does.Contain("[Stamina] recovered player=1"));
            for (var i = 0; i < 10; i++)
            {
                Send(_forward, InputButtons.Sprint);
            }

            _movement.TryGetState(1, out var state);
            Assert.That(state.Velocity.Z, Is.EqualTo(MotorSpec.DefaultSprintSpeed).Within(0.01f));
        }

        [Test]
        public void WithStaminaOffThePlayerSprintsOnAndNoStaminaIsSent()
        {
            Start(ActionRules.Default);
            Settle();

            for (var i = 0; i < 60; i++)
            {
                Send(_forward, InputButtons.Sprint);
            }

            _movement.TryGetState(1, out var state);
            Assert.That(state.Velocity.Z, Is.EqualTo(MotorSpec.DefaultSprintSpeed).Within(0.01f));
            Assert.That(LastOwner().Stamina, Is.Null);
            Assert.That(_log.ToString(), Does.Not.Contain("[Stamina]"));
            Assert.That(_log.ToString(), Does.Contain("[Status] rules stamina=Off stagger=Off suppression=Off"));
        }

        [Test]
        public void AStaggeredPlayerWalksSlowlyWithoutSprintingUntilItEnds()
        {
            Start(On());
            Settle();

            _status.Damaged(1, null, DamageKind.Melee, 30);
            Send(_forward, InputButtons.Sprint);
            Send(_forward, InputButtons.Sprint);

            Assert.That(LastOwner().Stagger, Is.EqualTo(StaggerKind.Stumble));
            _movement.TryGetState(1, out var staggered);
            Assert.That(
                staggered.Velocity.Z,
                Is.EqualTo(MotorSpec.DefaultWalkSpeed * StaggerSpec.DefaultPlayerMoveMultiplier).Within(0.05f),
                "a stumble walks at the move multiplier and does not sprint");
            Assert.That(_log.ToString(), Does.Contain("[Stagger] staggered player=1 kind=Stumble seconds=0.40 zone=none damage=Melee"));

            for (var i = 0; i < 30; i++)
            {
                Send(_forward, InputButtons.Sprint);
            }

            Assert.That(LastOwner().Stagger, Is.EqualTo(StaggerKind.None));
            Assert.That(_log.ToString(), Does.Contain("[Stagger] recovered player=1 kind=Stumble"));
            _movement.TryGetState(1, out var recovered);
            Assert.That(recovered.Velocity.Z, Is.EqualTo(MotorSpec.DefaultSprintSpeed).Within(0.01f));
        }

        [Test]
        public void WithStaggerOffDamageDoesNotStagger()
        {
            Start(ActionRules.Default.WithStamina(Stamina.On));
            Settle();

            _status.Damaged(1, null, DamageKind.Explosion, 50);
            Send(_forward, InputButtons.None);

            Assert.That(LastOwner().Stagger, Is.EqualTo(StaggerKind.None));
            Assert.That(_log.ToString(), Does.Not.Contain("[Stagger]"));
        }

        [Test]
        public void AShotPassingCloseSuppressesAndTheLevelIsSentThenFades()
        {
            Start(On());
            Settle();
            Assert.That(_status.TryGetMeters(1, out _), Is.True);
            var none = new HashSet<int>();
            var center = BodyCenter(1);

            for (var i = 0; i < 3; i++)
            {
                _status.ShotPassed(2, center + new Vector3(-20f, 0f, 0.1f), Vector3.UnitX, 40f, none);
            }

            Send(Vector2.Zero, InputButtons.None);
            var owner = LastOwner();
            Assert.That(owner.SuppressionLevel, Is.GreaterThan(SuppressionSpec.DefaultSuppressedLevel));
            Assert.That(owner.Suppressed, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Suppression] id=1 suppressed=true"));

            // 0.75 秒待ってから毎秒 0.4 抜けるので、2 秒あれば 0.2 を下回って解ける
            for (var i = 0; i < 2 * TickClock.DefaultTickRate; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }

            Assert.That(LastOwner().Suppressed, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Suppression] id=1 suppressed=false"));
        }

        [Test]
        public void TheShooterAndTheStruckPlayerAreNotSuppressed()
        {
            Start(On());
            Settle();
            var center = BodyCenter(1);

            _status.ShotPassed(1, center + new Vector3(-20f, 0f, 0f), Vector3.UnitX, 40f, new HashSet<int>());
            _status.ShotPassed(2, center + new Vector3(-20f, 0f, 0f), Vector3.UnitX, 40f, new HashSet<int> { 1 });

            Assert.That(_status.TryGetMeters(1, out var meters), Is.True);
            Assert.That(meters.SuppressionLevel, Is.Zero);
        }

        [Test]
        public void AShotThatStopsShortDoesNotReachThePlayer()
        {
            Start(On());
            Settle();
            var center = BodyCenter(1);

            // 弾の止まった点から 3m 先にいるので、半径 1.5m の外
            _status.ShotPassed(2, center + new Vector3(-20f, 0f, 0f), Vector3.UnitX, 17f, new HashSet<int>());

            Assert.That(_status.TryGetMeters(1, out var meters), Is.True);
            Assert.That(meters.SuppressionLevel, Is.Zero);
        }

        [Test]
        public void TheGivenTuningIsUsed()
        {
            Start(On(), new ServerPlayerStatusTuning(stamina: new StaminaSpec(max: 40f, recoverAmount: 10f, slideCost: 10f, dodgeCost: 10f)));
            Settle();

            Assert.That(LastOwner().Stamina.Value.Max, Is.EqualTo(40f));
        }

        private static ServerPlayerStatusTuning SmallStamina() => new ServerPlayerStatusTuning(stamina: _smallStamina);

        private static ActionRules On()
        {
            return ActionRules.Default.WithStamina(Stamina.On).WithStagger(Stagger.On).WithSuppression(Suppression.On);
        }

        private void Start(ActionRules actions, ServerPlayerStatusTuning tuning = null)
        {
            _transport?.Dispose();
            _transport = new RecordingTransport();
            _match = new ServerMatch(5f, _log, new ServerEventWriter(false, _log, _log), () => _time);
            var rules = GameRules.Default.WithActions(actions);
            var defaults = ServerMotorSettings.Default;
            var settings = new ServerMotorSettings(
                defaults.Motor, defaults.Stance, defaults.Mover, defaults.Prone, defaults.Swim, defaults.WallRun, rules, defaults.KillPlaneY, _spawn);
            _movement = new ServerMovement(settings);
            _status = new ServerPlayerStatus(rules, tuning ?? SmallStamina());
            _movement.InputGate = _status;
            var snapshots = new ServerSnapshots(rules, new ISnapshotContributor[] { _movement, _status }, TestAntiCheat.Off);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_status);
            _host.Register(snapshots);
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort);
            var map = new ServerMapData("NetTest", TestMaps.World(), TestMaps.NavMesh(), "memory");
            _context = new ServerContext(options, "0.2.0", _log, map, _match, new ServerMessenger(_transport), new StubControl());
            _host.Start(_context);

            // 体はルールのモジュールが出現させるときに作るので、ルールのモジュールを置かないここでは先に置いておく
            _context.Bodies.Add(1);
            _context.Bodies.Teleport(1, _spawn, 0f, 0);
            _match.Join(_connection, "key");
            _host.OnConnected(_connection);
            _nextInputTick = 0;
        }

        private Vector3 BodyCenter(int playerId)
        {
            Assert.That(_context.Bodies.TryGet(playerId, out var body), Is.True);
            Assert.That(body.HasSpawned, Is.True, "the player stands on the floor before the shot");
            return body.Center;
        }

        private void Settle()
        {
            for (var i = 0; i < _settleTicks; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }
        }

        // 1 tick ずつ送り、そのたびにサーバーを 1 tick 進める (クライアントが tick の速さで送るのと同じ)
        private void Send(Vector2 move, InputButtons buttons)
        {
            var message = new InputMessage { AckedSnapshotTick = -1 };
            message.Add(new InputFrame(_nextInputTick, move, 0f, buttons));
            _host.OnMessage(_connection, message);
            _nextInputTick++;
            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
        }

        private OwnerSnapshot LastOwner()
        {
            Assert.That(_transport.Sent, Is.Not.Empty, "a snapshot is sent every tick");
            var sent = _transport.Sent[_transport.Sent.Count - 1];
            var snapshot = (SnapshotMessage)_clientTable.Read(new ArraySegment<byte>(sent.Payload));
            Assert.That(snapshot.HasOwner, Is.True);
            return snapshot.Owner;
        }
    }
}
