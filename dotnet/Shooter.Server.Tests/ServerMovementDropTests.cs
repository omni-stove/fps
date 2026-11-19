using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerMovementDropTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _settleTicks = 30;

        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

        // 床 (TestMaps.World) の上面は y=0.5。体の原点はカプセルの中心で、足元はそれより 1 m 下
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);
        private static readonly ConnectionId _connection = new ConnectionId(1);

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
        public void DropPlacesThePlayerHighAndOpensTheParachute()
        {
            Start(Parachute.On);

            Assert.That(_movement.Drop(1, new Vector3(2f, 30f, -3f)), Is.True);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsParachuting, Is.True);
            Assert.That(state.Position, Is.EqualTo(new Vector3(2f, 30f, -3f)));
            Assert.That(state.IsGrounded, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] parachute owner=1 tick=" + (_settleTicks - 1) + " opened height=28.50"));
        }

        [Test]
        public void TheDroppedPlayerDescendsAndLandsWithoutFallDamage()
        {
            Start(Parachute.On);
            var landedSpeed = -1f;
            _movement.Landed += (player, tick, speed) => landedSpeed = speed;
            _movement.Drop(1, new Vector3(0f, 12f, 0f));

            Send(Vector2.Zero, InputButtons.None);
            Assert.That(_movement.TryGetState(1, out var gliding), Is.True);
            Assert.That(gliding.IsParachuting, Is.True);
            Assert.That(gliding.Velocity.Y, Is.EqualTo(-ParachuteMotion.DefaultDescentSpeed));
            Assert.That(12f - gliding.Position.Y, Is.EqualTo(ParachuteMotion.DefaultDescentSpeed * _tickSeconds).Within(1e-4f));

            for (var i = 0; i < 300 && landedSpeed < 0f; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }

            Assert.That(_movement.TryGetState(1, out var landed), Is.True);
            Assert.That(landed.IsParachuting, Is.False);
            Assert.That(landed.IsGrounded, Is.True);
            Assert.That(landedSpeed, Is.EqualTo(ParachuteMotion.DefaultDescentSpeed));
            Assert.That(_movement.FallDamage(landedSpeed, NetMatchSettings.DefaultMaxHealth), Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Net] parachute owner=1 tick=").And.Contain(" closed reason=Landed"));
        }

        [Test]
        public void TheSnapshotRowCarriesTheOpenParachute()
        {
            Start(Parachute.On);
            _movement.Drop(1, new Vector3(0f, 20f, 0f));
            Send(new Vector2(1f, 0f), InputButtons.None);

            var row = Row();

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(row.IsParachuting, Is.True);
            Assert.That(row.Velocity, Is.EqualTo(state.Velocity));
            Assert.That(new Vector2(row.Velocity.X, row.Velocity.Z), Is.EqualTo(state.Parachute.HorizontalVelocity), "the client rebuilds the glide from it");
        }

        [Test]
        public void WithTheRuleOffTheDroppedPlayerFallsWithoutAParachute()
        {
            Start(Parachute.Off);

            Assert.That(_movement.Drop(1, new Vector3(0f, 30f, 0f)), Is.True);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsParachuting, Is.False);
            Assert.That(state.Position, Is.EqualTo(new Vector3(0f, 30f, 0f)));
            Assert.That(_log.ToString(), Does.Contain("[Net] parachute owner=1 tick=" + (_settleTicks - 1) + " open=RuleOff height=28.50"));
            Assert.That(Row().IsParachuting, Is.False);
        }

        [Test]
        public void DroppingTooLowLeavesItClosed()
        {
            Start(Parachute.On);

            _movement.Drop(1, new Vector3(0f, 4f, 0f));

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsParachuting, Is.False);
            Assert.That(_log.ToString(), Does.Contain(" open=TooLow height=2.50"));
        }

        [Test]
        public void DropRejectsAnUnknownPlayerAndANonFinitePosition()
        {
            Start(Parachute.On);

            Assert.That(_movement.Drop(99, new Vector3(0f, 30f, 0f)), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => _movement.Drop(1, new Vector3(0f, float.NaN, 0f)));
        }

        [Test]
        public void TheInputBitsOpenAndCloseItInTheAir()
        {
            Start(Parachute.On);
            _movement.Teleport(1, new Vector3(0f, 30f, 0f));

            Send(Vector2.Zero, InputButtons.Jump | InputButtons.OpenParachute);
            Assert.That(_movement.TryGetState(1, out var opened), Is.True);
            Assert.That(opened.IsParachuting, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Net] parachute owner=1 tick=" + _settleTicks + " opened height="));

            Send(Vector2.Zero, InputButtons.CloseParachute);
            Assert.That(_movement.TryGetState(1, out var closed), Is.True);
            Assert.That(closed.IsParachuting, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] parachute owner=1 tick=" + (_settleTicks + 1) + " closed reason=Released"));
        }

        [Test]
        public void TheOpenBitIsIgnoredWhenTheRuleIsOff()
        {
            Start(Parachute.Off);
            _movement.Teleport(1, new Vector3(0f, 30f, 0f));

            Send(Vector2.Zero, InputButtons.Jump | InputButtons.OpenParachute);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsParachuting, Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] parachute"));
        }

        [Test]
        public void TheTuningComesFromTheMotorSettings()
        {
            Start(Parachute.On, new ParachuteTuning(descentSpeed: 2f));
            _movement.Drop(1, new Vector3(0f, 20f, 0f));

            Send(Vector2.Zero, InputButtons.None);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.Velocity.Y, Is.EqualTo(-2f));
            Assert.That(_movement.Settings.Parachute.DescentSpeed, Is.EqualTo(2f));
        }

        private void Start(Parachute rule, ParachuteTuning parachute = null)
        {
            _transport?.Dispose();
            _transport = new RecordingTransport();
            _match = new ServerMatch(5f, _log, new ServerEventWriter(false, _log, _log), () => _time);
            var defaults = ServerMotorSettings.Default;
            var settings = new ServerMotorSettings(
                defaults.Motor,
                defaults.Stance,
                defaults.Mover,
                defaults.Prone,
                defaults.Swim,
                defaults.WallRun,
                parachute ?? ParachuteTuning.Default,
                GameRules.Default.WithActions(ActionRules.Default.WithParachute(rule)),
                defaults.KillPlaneY,
                _spawn);
            _movement = new ServerMovement(settings);
            _host = new ServerHost();
            _host.Register(_movement);
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort);
            var map = new ServerMapData("NetTest", TestMaps.World(), TestMaps.NavMesh(), "memory");
            var context = new ServerContext(options, "0.2.0", _log, map, _match, new ServerMessenger(_transport), new StubControl());
            _host.Start(context);
            _match.Join(_connection, "key");
            _host.OnConnected(_connection);
            _nextInputTick = 0;
            for (var i = 0; i < _settleTicks; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }

            Assert.That(_movement.TryGetState(1, out var settled), Is.True);
            Assert.That(settled.IsGrounded, Is.True, "the player should stand on the floor before the test");
        }

        // 1 tick ずつ送り、そのたびに 1 tick 進めて入力の予算を戻す (クライアントが tick の速さで送るのと同じ)
        private void Send(Vector2 move, InputButtons buttons)
        {
            var message = new InputMessage { AckedSnapshotTick = -1 };
            message.Add(new InputFrame(_nextInputTick, move, 0f, buttons));
            _movement.OnMessage(_connection, message);
            _nextInputTick++;
            _movement.Tick(_nextInputTick, _tickSeconds);
        }

        private PlayerSnapshot Row()
        {
            var snapshot = new SnapshotMessage();
            snapshot.Players.Add().PlayerId = 1;
            Assert.That(_match.TryGetPlayer(1, out var receiver), Is.True);
            _movement.Contribute(snapshot, receiver);
            return snapshot.Players[0];
        }
    }
}
