using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerMovementMantleTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _settleTicks = 30;
        private const int _mostMantleTicks = 300;
        private const float _floorTop = 0.5f;

        // 体 (半径 0.5) の正面 0.8 m に段の面がある。奥行き 4 m は、上に立つのに要る 1.1 m (直径 + WallGap) より広い
        private const float _faceZ = 0.8f;
        private const float _ledgeDepth = 4f;

        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        // 床 (上面 0.5) の上。体の原点はカプセルの中心で、足元はそれより 1 m 下
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);
        private static readonly ConnectionId _connection = new ConnectionId(1);

        private static readonly GameRules _mantleOn = GameRules.Default.WithActions(ActionRules.Default.WithMantle(Mantle.On));

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMovement _movement;
        private int _nextInputTick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _nextInputTick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _transport?.Dispose();
            _log.Dispose();
        }

        [Test]
        public void JumpingAtAChestHighLedgeClimbsOntoItWhenTheRulesTurnMantleOn()
        {
            Start(_mantleOn, Ledge(1.5f));
            var startTick = _nextInputTick;

            Send(InputButtons.Jump);

            Assert.That(_movement.IsMantling(1), Is.True);
            // 足元は床の上面から外皮 (DefaultSkinWidth 0.08) だけ浮いているので、測る高さは 1.5 - 0.08
            Assert.That(_log.ToString(), Does.Contain("[Net] mantle owner=1 tick=" + startTick + " probe=Mantle height=1.42 depth=inf"));
            Assert.That(_log.ToString(), Does.Contain("[Net] mantle owner=1 tick=" + startTick + " mantling=true"));
            _movement.TryGetState(1, out var rising);
            Assert.That(rising.Velocity.Y, Is.GreaterThan(0f), "the body follows the path upward");
            Assert.That(rising.IsVaulting, Is.False, "the mantle is not sent as a vault");

            var ticks = MantleToTheEnd();
            var endTick = _nextInputTick - 1;
            Assert.That(ticks, Is.LessThan(_mostMantleTicks));
            Assert.That(_log.ToString(), Does.Contain("[Net] mantle owner=1 tick=" + endTick + " mantling=false"));
            _movement.TryGetState(1, out var topped);
            Assert.That(topped.Velocity, Is.EqualTo(Vector3.Zero), "the climb does not carry its speed onto the ledge");

            SendIdle(_settleTicks);
            _movement.TryGetState(1, out var state);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.Position.Y, Is.EqualTo(_floorTop + 1.5f + 1f + CapsuleMoverSpec.DefaultSkinWidth).Within(0.02f), "stands on the ledge");
            Assert.That(state.Position.Z, Is.GreaterThan(_faceZ + 0.5f), "the whole body is past the face");
        }

        [Test]
        public void TheClimbTakesLongerAtASlowerConfiguredSpeed()
        {
            Start(_mantleOn, Ledge(1.5f));
            Send(InputButtons.Jump);
            var normal = MantleToTheEnd();

            Start(_mantleOn, Ledge(1.5f), new MotorSpec(4f, 8f, 1.5f, 9.81f, 0.5f, 2f, mantleSpeed: MotorSpec.DefaultMantleSpeed / 2f));
            Send(InputButtons.Jump);
            var slow = MantleToTheEnd();

            Assert.That(slow, Is.GreaterThan(normal * 1.8f));
        }

        [Test]
        public void JumpingAtTheLedgeWithMantleOffIsAPlainJump()
        {
            Start(GameRules.Default, Ledge(1.5f));

            Send(InputButtons.Jump);
            Assert.That(_movement.IsMantling(1), Is.False);
            _movement.TryGetState(1, out var jumped);
            Assert.That(jumped.Velocity.Y, Is.GreaterThan(0f), "the jump button jumps");

            // 既定のジャンプ (1.2 m・重力 20 m/s²) は約 0.7 秒で床へ戻るので、2 秒待つ
            SendIdle(TickClock.DefaultTickRate * 2);
            _movement.TryGetState(1, out var state);
            Assert.That(state.Position.Y, Is.EqualTo(_floorTop + 1f + CapsuleMoverSpec.DefaultSkinWidth).Within(0.02f), "still on the floor");
            Assert.That(state.Position.Z, Is.LessThan(_faceZ));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] mantle "));
        }

        [Test]
        public void ALedgeAboveTheReachIsLoggedAndNotClimbed()
        {
            Start(_mantleOn, Ledge(3f));
            var tick = _nextInputTick;

            Send(InputButtons.Jump);

            Assert.That(_movement.IsMantling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] mantle owner=1 tick=" + tick + " probe=TooHigh height=2.50 depth=inf"));
            Assert.That(_log.ToString(), Does.Not.Contain("mantling=true"));
        }

        [Test]
        public void AWallLowEnoughToVaultIsNotMantled()
        {
            Start(_mantleOn, Ledge(1f));
            var tick = _nextInputTick;

            Send(InputButtons.Jump);

            Assert.That(_movement.IsMantling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] mantle owner=1 tick=" + tick + " probe=TooLow"));
        }

        [Test]
        public void AVaultableWallIsVaultedWhenBothRulesAreOn()
        {
            var rules = GameRules.Default.WithActions(ActionRules.Default.WithMantle(Mantle.On).WithVault(Vault.On));
            Start(rules, Wall(1f, 0.4f));

            Send(InputButtons.Jump);

            Assert.That(_movement.IsMantling(1), Is.False);
            _movement.TryGetState(1, out var state);
            Assert.That(state.IsVaulting, Is.True);
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] mantle "));
        }

        [Test]
        public void AStepTooShallowToStandOnIsNotClimbed()
        {
            Start(_mantleOn, Wall(1.5f, 0.4f));
            var tick = _nextInputTick;

            Send(InputButtons.Jump);

            Assert.That(_movement.IsMantling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] mantle owner=1 tick=" + tick + " probe=TooNarrow height=1.42 depth=0.40"));
        }

        [Test]
        public void ACrouchedPlayerDoesNotMantle()
        {
            Start(_mantleOn, Ledge(1.5f));
            for (var i = 0; i < 5; i++)
            {
                Send(InputButtons.Crouch);
            }

            _movement.TryGetState(1, out var crouched);
            Assert.That(crouched.IsCrouched, Is.True);

            Send(InputButtons.Crouch | InputButtons.Jump);

            Assert.That(_movement.IsMantling(1), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] mantle "));
        }

        [Test]
        public void InputWhileMantlingDoesNotCrouchOrJump()
        {
            Start(_mantleOn, Ledge(1.5f));
            Send(InputButtons.Jump);

            Send(InputButtons.Crouch | InputButtons.Jump | InputButtons.Sprint, new Vector2(1f, 0f));

            Assert.That(_movement.IsMantling(1), Is.True);
            _movement.TryGetState(1, out var state);
            Assert.That(state.IsCrouched, Is.False);
            Assert.That(state.Position.X, Is.EqualTo(0f).Within(1e-4f), "the path, not the stick, moves the body");
        }

        [Test]
        public void GoingDownStopsTheClimb()
        {
            Start(_mantleOn, Ledge(1.5f));
            Send(InputButtons.Jump);
            Send(InputButtons.None);

            Assert.That(_movement.SetDowned(1, true), Is.True);
            Send(InputButtons.None);

            Assert.That(_movement.IsMantling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] mantle owner=1 tick=" + (_nextInputTick - 1) + " mantling=false"));
        }

        [Test]
        public void TeleportingStopsTheClimb()
        {
            Start(_mantleOn, Ledge(1.5f));
            Send(InputButtons.Jump);
            var tick = _nextInputTick - 1;

            Assert.That(_movement.Teleport(1, new Vector3(0f, 1.7f, -3f)), Is.True);

            Assert.That(_movement.IsMantling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] mantle owner=1 tick=" + tick + " mantling=false"));
            Send(InputButtons.None);
            _movement.TryGetState(1, out var state);
            Assert.That(state.Position.Z, Is.EqualTo(-3f).Within(1e-3f), "the old path does not move the teleported body");
        }

        // 正面 0.8 m から奥へ 4 m 続く、床から height の段
        private static WorldMap Ledge(float height)
        {
            return Wall(height, _ledgeDepth);
        }

        private static WorldMap Wall(float height, float depth)
        {
            return new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(10f, _floorTop, 10f), Quaternion.Identity),
                WorldCollider.Box(
                    2,
                    0,
                    WorldCollider.StaticGroup,
                    new Vector3(0f, _floorTop + (height / 2f), _faceZ + (depth / 2f)),
                    new Vector3(3f, height / 2f, depth / 2f),
                    Quaternion.Identity),
            });
        }

        private int MantleToTheEnd()
        {
            var ticks = 0;
            while (_movement.IsMantling(1) && ticks < _mostMantleTicks)
            {
                Send(InputButtons.None);
                ticks++;
            }

            Assert.That(_movement.IsMantling(1), Is.False, "the climb ends");
            return ticks;
        }

        private void Start(GameRules rules, WorldMap world, MotorSpec motor = null)
        {
            _transport?.Dispose();
            _transport = new RecordingTransport();
            _match = new ServerMatch(5f, _log, new ServerEventWriter(false, _log, _log), () => _time);
            var defaults = ServerMotorSettings.Default;
            var settings = new ServerMotorSettings(
                motor ?? defaults.Motor,
                defaults.Stance,
                defaults.Mover,
                defaults.Prone,
                defaults.Swim,
                defaults.WallRun,
                rules,
                defaults.KillPlaneY,
                _spawn);
            _movement = new ServerMovement(settings);
            _host = new ServerHost();
            _host.Register(_movement);
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort);
            var map = new ServerMapData("NetTest", world, TestMaps.NavMesh(), "memory");
            var context = new ServerContext(options, "0.2.0", _log, map, _match, new ServerMessenger(_transport), new StubControl());
            _host.Start(context);
            _match.Join(_connection, "key");
            _host.OnConnected(_connection);
            _nextInputTick = 0;
            SendIdle(_settleTicks);

            Assert.That(_movement.TryGetState(1, out var settled), Is.True);
            Assert.That(settled.IsGrounded, Is.True, "the player should stand on the floor before the test");
        }

        private void SendIdle(int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                Send(InputButtons.None);
            }
        }

        // 1 tick ずつ送り、そのたびに 1 tick 進めて入力の予算を戻す (クライアントが tick の速さで送るのと同じ)
        private void Send(InputButtons buttons, Vector2 move = default)
        {
            var message = new InputMessage { AckedSnapshotTick = -1 };
            message.Add(new InputFrame(_nextInputTick, move, 0f, buttons));
            _movement.OnMessage(_connection, message);
            _nextInputTick++;
            _movement.Tick(_nextInputTick, _tickSeconds);
        }
    }
}
