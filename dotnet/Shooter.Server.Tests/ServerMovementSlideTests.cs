using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerMovementSlideTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _settleTicks = 30;
        private const int _sprintTicks = 5;

        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        // 床 (TestMaps.World) の上面は y=0.5 で、z は -10 から 10 まで。体の原点はカプセルの中心で、足元はそれより 1 m 下
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, -8f);
        private static readonly Vector2 _forward = new Vector2(0f, 1f);
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
        public void CrouchingWhileSprintingWithTheSlideBitSlides()
        {
            Start(Slide.On);
            SprintForward();

            Send(_forward, InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsSliding, Is.True);
            Assert.That(_movement.IsSliding(1), Is.True);
            Assert.That(state.IsCrouched, Is.True);
            Assert.That(state.Velocity.Z, Is.GreaterThan(MotorSpec.DefaultSprintSpeed), "it slides faster than the sprint");
            Assert.That(
                _log.ToString(),
                Does.Contain("[Net] slide owner=1 tick=" + (_settleTicks + _sprintTicks) + " start speed=11.00 direction=(0.00, 1.00)"));
        }

        [Test]
        public void TheSnapshotRowShowsTheSlideToOtherPlayers()
        {
            Start(Slide.On);
            SprintForward();
            Send(_forward, InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide);

            var row = Row();

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(row.IsCrouched, Is.True);
            Assert.That(row.Position, Is.EqualTo(state.Position));
            Assert.That(row.Velocity, Is.EqualTo(state.Velocity));
            Assert.That(row.Velocity.Z, Is.GreaterThan(MotorSpec.DefaultSprintSpeed));
        }

        [Test]
        public void TheSlideEndsAndIsLogged()
        {
            Start(Slide.On);
            SprintForward();
            Send(_forward, InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide);
            for (var i = 0; i < 120 && _movement.IsSliding(1); i++)
            {
                Send(_forward, InputButtons.Crouch);
            }

            Assert.That(_movement.IsSliding(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain(" end reason=Stopped speed="));
            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsCrouched, Is.True, "the held crouch keeps the player down after the slide");
        }

        [Test]
        public void WithTheRuleOffTheSlideBitOnlyCrouches()
        {
            Start(Slide.Off);
            SprintForward();

            Send(_forward, InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsSliding, Is.False);
            Assert.That(state.IsCrouched, Is.True);
            Assert.That(state.Velocity.Z, Is.EqualTo(MotorSpec.DefaultCrouchSpeed).Within(1e-4f));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] slide"));
        }

        [Test]
        public void TheServerDoesNotSlideFromAStandstill()
        {
            Start(Slide.On);

            Send(_forward, InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide);

            Assert.That(_movement.IsSliding(1), Is.False, "the client asks, but the speed the server knows is too low");
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] slide"));
        }

        [Test]
        public void TheTuningComesFromTheMotorSettings()
        {
            Start(Slide.On, new SlideTuning(startSpeed: 14f));
            SprintForward();

            Send(_forward, InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide);

            Assert.That(_movement.Settings.Slide.StartSpeed, Is.EqualTo(14f));
            Assert.That(_log.ToString(), Does.Contain(" start speed=14.00 "));
        }

        [Test]
        public void TeleportingDropsTheSlide()
        {
            Start(Slide.On);
            SprintForward();
            Send(_forward, InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide);
            Assert.That(_movement.IsSliding(1), Is.True);

            _movement.Teleport(1, _spawn);

            Assert.That(_movement.IsSliding(1), Is.False);
        }

        [Test]
        public void TheSlideRulesAndTuningSurviveARulesChange()
        {
            var tuning = new SlideTuning(startSpeed: 12f);
            var settings = Settings(Slide.On, tuning);

            var changed = settings.WithRules(GameRules.Default.WithActions(ActionRules.Default.WithSlide(Slide.Off)));

            Assert.That(changed.Slide, Is.SameAs(tuning));
            Assert.That(changed.Rules.Actions.Slide, Is.EqualTo(Slide.Off));
            Assert.That(changed.WithSpawnPosition(Vector3.Zero).Slide, Is.SameAs(tuning));
            Assert.That(ServerMotorSettings.Default.Slide, Is.SameAs(SlideTuning.Default));
            Assert.Throws<ArgumentNullException>(() => new ServerMotorSettings(
                settings.Motor, settings.Stance, settings.Mover, settings.Prone, settings.Swim, settings.WallRun, settings.Parachute, null,
                settings.Rules, settings.KillPlaneY, settings.SpawnPosition));
        }

        [Test]
        public void TheLogLinesMatchTheOfflineFormat()
        {
            Assert.That(
                ServerMovementLog.SlideStart(3, 42, 11f, new Vector2(0.6f, 0.8f)),
                Is.EqualTo("[Net] slide owner=3 tick=42 start speed=11.00 direction=(0.60, 0.80)"));
            Assert.That(
                ServerMovementLog.SlideEnd(3, 50, SlideEnd.Jumped, 9.5f, 0.25f, 2.5f),
                Is.EqualTo("[Net] slide owner=3 tick=50 end reason=Jumped speed=9.50 seconds=0.25 distance=2.50"));
        }

        private void SprintForward()
        {
            for (var i = 0; i < _sprintTicks; i++)
            {
                Send(_forward, InputButtons.Sprint);
            }

            Assert.That(_movement.TryGetState(1, out var running), Is.True);
            Assert.That(running.Velocity.Z, Is.EqualTo(MotorSpec.DefaultSprintSpeed).Within(1e-4f), "the player should be sprinting before the test");
        }

        private static ServerMotorSettings Settings(Slide rule, SlideTuning slide)
        {
            var defaults = ServerMotorSettings.Default;
            return new ServerMotorSettings(
                defaults.Motor,
                defaults.Stance,
                defaults.Mover,
                defaults.Prone,
                defaults.Swim,
                defaults.WallRun,
                defaults.Parachute,
                slide ?? SlideTuning.Default,
                GameRules.Default.WithActions(ActionRules.Default.WithSlide(rule)),
                defaults.KillPlaneY,
                _spawn);
        }

        private void Start(Slide rule, SlideTuning slide = null)
        {
            _transport?.Dispose();
            _transport = new RecordingTransport();
            _match = new ServerMatch(5f, _log, new ServerEventWriter(false, _log, _log), () => _time);
            _movement = new ServerMovement(Settings(rule, slide));
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
