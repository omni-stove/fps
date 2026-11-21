using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerMovementZiplineTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _settleTicks = 30;
        private const int _mostRideTicks = 600;
        private const float _floorTop = 0.5f;

        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        // 床 (上面 0.5) の上。体の原点はカプセルの中心で、足元はそれより 1 m 下。立つと中心は 0.5 + 1 + 外皮 0.08 = 1.58
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);
        private static readonly ConnectionId _connection = new ConnectionId(1);

        // 手 (中心 1.58 + 既定のぶら下がり 1.3 = 2.88) から 1.12 m 上を通り、奥へ 20 m・0.5 m 下る線。吊られた体の足元は床より上に残る
        private static readonly Vector3 _lineStart = new Vector3(0f, 4f, 0f);
        private static readonly Vector3 _lineEnd = new Vector3(0f, 3.5f, 20f);

        private static readonly GameRules _ziplineOn = GameRules.Default.WithActions(ActionRules.Default.WithZipline(Zipline.On));

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
        public void JumpingUnderTheLineGrabsItSlidesToTheEndAndLetsGo()
        {
            Start(_ziplineOn, OneLine());
            var grabTick = _nextInputTick;

            Send(InputButtons.Jump);

            Assert.That(_movement.IsZiplining(1), Is.True);
            Assert.That(_movement.ZiplineIndexOf(1), Is.EqualTo(0));
            // 手に一番近い所は線の始まりの際なので、端の余白 (0.5 m) の所を掴み、長さ 20.006 m から両端の余白を引いた分が残る
            Assert.That(_log.ToString(), Does.Contain("[Net] zipline owner=1 tick=" + grabTick + " grab line=0 at=0.50 remaining=19.01 speed=4.00"));
            _movement.TryGetState(1, out var hanging);
            var line = new ZiplineMotion(_lineStart, _lineEnd, ZiplineSpec.Default);
            var expected = line.PointAt(ZiplineSpec.DefaultEndMargin + (ZiplineSpec.DefaultMinSpeed * _tickSeconds))
                - new Vector3(0f, ZiplineTuning.DefaultHangOffset, 0f);
            Assert.That(Vector3.Distance(hanging.Position, expected), Is.LessThan(0.01f), "the grab tick hangs the body under the line, not a jump");
            Assert.That(hanging.Velocity.Z, Is.GreaterThan(0f), "it slides down toward the low end");

            var ticks = RideToTheEnd();
            var endTick = _nextInputTick - 1;
            Assert.That(ticks, Is.LessThan(_mostRideTicks));
            Assert.That(_log.ToString(), Does.Contain("[Net] zipline owner=1 tick=" + endTick + " release reason=end"));
            _movement.TryGetState(1, out var arrived);
            var stop = line.PointAt(line.Length - ZiplineSpec.DefaultEndMargin) - new Vector3(0f, ZiplineTuning.DefaultHangOffset, 0f);
            Assert.That(Vector3.Distance(arrived.Position, stop), Is.LessThan(0.01f), "it stops the end margin short of the end");
            Assert.That(arrived.Velocity.Z, Is.GreaterThan(ZiplineSpec.DefaultMinSpeed - 0.01f), "letting go keeps the slide speed (as offline)");

            SendIdle(TickClock.DefaultTickRate * 2);
            _movement.TryGetState(1, out var landed);
            Assert.That(landed.IsGrounded, Is.True, "it drops to the floor after the end");
            Assert.That(landed.Position.Z, Is.GreaterThan(stop.Z - 0.01f), "it does not snap back along the line");
            Assert.That(_movement.IsZiplining(1), Is.False);
        }

        [Test]
        public void InputWhileRidingDoesNotMoveCrouchSprintOrAimOffTheLine()
        {
            Start(_ziplineOn, OneLine());
            Send(InputButtons.Jump);

            Send(InputButtons.Crouch | InputButtons.Sprint | InputButtons.Aim, new Vector2(1f, 0f));

            Assert.That(_movement.IsZiplining(1), Is.True);
            _movement.TryGetState(1, out var state);
            Assert.That(state.IsCrouched, Is.False);
            Assert.That(state.Position.X, Is.EqualTo(0f).Within(1e-4f), "the line, not the stick, moves the body");
            Assert.That(_movement.IsSprinting(1), Is.False);
            Assert.That(_movement.IsAiming(1), Is.False, "one hand holds the line, so the sights are not raised (as offline)");
        }

        [Test]
        public void JumpingWhileRidingLetsGoWithTheSlideSpeedAndNoJump()
        {
            Start(_ziplineOn, OneLine());
            Send(InputButtons.Jump);
            SendIdle(10);
            _movement.TryGetState(1, out var riding);

            Send(InputButtons.Jump);

            Assert.That(_movement.IsZiplining(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] zipline owner=1 tick=" + (_nextInputTick - 1) + " release reason=jump"));
            _movement.TryGetState(1, out var released);
            Assert.That(released.Velocity.Z, Is.EqualTo(riding.Velocity.Z).Within(0.2f), "it keeps the speed along the line");
            Assert.That(released.Velocity.Y, Is.LessThanOrEqualTo(0f), "letting go is not a jump upward");
        }

        [Test]
        public void WithZiplineOffTheJumpButtonJumps()
        {
            Start(GameRules.Default.WithActions(ActionRules.Default.WithZipline(Zipline.Off)), OneLine());

            Send(InputButtons.Jump);

            Assert.That(_movement.IsZiplining(1), Is.False);
            _movement.TryGetState(1, out var jumped);
            Assert.That(jumped.Velocity.Y, Is.GreaterThan(0f));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] zipline "));
        }

        [Test]
        public void ALineOutOfReachIsNotGrabbed()
        {
            // 手 (2.88) から 3.12 m 上は、掴める距離 (1.5 m) の外
            Start(_ziplineOn, Line(new Vector3(0f, 6f, 0f), new Vector3(0f, 5.5f, 20f)));

            Send(InputButtons.Jump);

            Assert.That(_movement.IsZiplining(1), Is.False);
            _movement.TryGetState(1, out var jumped);
            Assert.That(jumped.Velocity.Y, Is.GreaterThan(0f), "the jump button jumps");
        }

        [Test]
        public void TheConfiguredGrabRadiusAndHangOffsetAreUsed()
        {
            // 掴める距離を 1 m に縮めると、手から 1.12 m 上の線は届かない
            Start(_ziplineOn, OneLine(), new ZiplineTuning(grabRadius: 1f));
            Send(InputButtons.Jump);
            Assert.That(_movement.IsZiplining(1), Is.False);

            // ぶら下がりを 1.8 m に伸ばすと手は 3.38 で、線まで 0.62 m なので届き、体は線の 1.8 m 下に吊られる
            Start(_ziplineOn, OneLine(), new ZiplineTuning(grabRadius: 1f, hangOffset: 1.8f));
            Send(InputButtons.Jump);
            Assert.That(_movement.IsZiplining(1), Is.True);
            _movement.TryGetState(1, out var hanging);
            var line = new ZiplineMotion(_lineStart, _lineEnd, ZiplineSpec.Default);
            var under = line.PointAt(line.ClosestDistance(hanging.Position + new Vector3(0f, 1.8f, 0f)));
            Assert.That(under.Y - hanging.Position.Y, Is.EqualTo(1.8f).Within(0.01f));
        }

        [Test]
        public void TheLineSpecFromTheMapSetsTheSpeed()
        {
            var fast = new ZiplineSpec(minSpeed: 10f, maxSpeed: 20f, gravity: ZiplineSpec.DefaultGravity, endMargin: ZiplineSpec.DefaultEndMargin);
            Start(_ziplineOn, Line(_lineStart, _lineEnd, fast));

            Send(InputButtons.Jump);

            Assert.That(_log.ToString(), Does.Contain(" grab line=0 at=0.50 remaining=19.01 speed=10.00"));
            _movement.TryGetState(1, out var hanging);
            Assert.That(hanging.Velocity.Length(), Is.EqualTo(10f).Within(0.05f));
        }

        [Test]
        public void GoingDownLetsGo()
        {
            Start(_ziplineOn, OneLine());
            Send(InputButtons.Jump);
            Send(InputButtons.None);

            Assert.That(_movement.SetDowned(1, true), Is.True);
            Send(InputButtons.None);

            Assert.That(_movement.IsZiplining(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] zipline owner=1 tick=" + (_nextInputTick - 1) + " release reason=downed"));
        }

        [Test]
        public void AKnockbackLetsGo()
        {
            Start(_ziplineOn, OneLine());
            Send(InputButtons.Jump);

            Assert.That(_movement.Launch(1, new Vector3(5f, 3f, 0f)), Is.True);
            Send(InputButtons.None);

            Assert.That(_movement.IsZiplining(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain(" release reason=knockback"));
            _movement.TryGetState(1, out var flying);
            Assert.That(flying.Position.X, Is.GreaterThan(0f), "the knockback, not the line, moves the body");
        }

        [Test]
        public void TeleportingLetsGoAndTheNextGrabWaitsTheRegrabSeconds()
        {
            Start(_ziplineOn, OneLine());
            Send(InputButtons.Jump);
            var tick = _nextInputTick - 1;

            Assert.That(_movement.Teleport(1, _spawn), Is.True);

            Assert.That(_movement.IsZiplining(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] zipline owner=1 tick=" + tick + " release reason=interrupted"));
            SendIdle(_settleTicks / 2);

            // 既定の待ち 0.5 秒 (30 tick) のうちなので、ジャンプはふつうに跳ぶ
            Send(InputButtons.Jump);
            Assert.That(_movement.IsZiplining(1), Is.False);

            // 既定のジャンプは約 0.7 秒で床へ戻るので、2 秒待って待ちも終える
            SendIdle(TickClock.DefaultTickRate * 2);
            Send(InputButtons.Jump);
            Assert.That(_movement.IsZiplining(1), Is.True);
        }

        [Test]
        public void JumpingWhileSlidingUnderTheLineDoesNotGrabIt()
        {
            // 掴める距離を 3 m に広げ、しゃがんで滑っている低い手からも線に届くようにする
            var rules = _ziplineOn.WithActions(_ziplineOn.Actions.WithSlide(Slide.On));
            Start(rules, OneLine(), new ZiplineTuning(grabRadius: 3f));
            for (var i = 0; i < 5; i++)
            {
                Send(InputButtons.Sprint, new Vector2(0f, 1f));
            }

            Send(InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide, new Vector2(0f, 1f));
            Assert.That(_movement.IsSliding(1), Is.True, "the player should be sliding before the jump");

            Send(InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide | InputButtons.Jump, new Vector2(0f, 1f));

            Assert.That(_movement.IsZiplining(1), Is.False, "a slide is refused like a dodge, so the slide state is not dropped unlogged");
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] zipline "));
        }

        [Test]
        public void ASlideEndedJustBeforeGrabbingIsLoggedOnceWhileRiding()
        {
            // 掴める距離を 3 m に広げ、滑り終えてしゃがんだままの低い手からも線に届くようにする
            var rules = _ziplineOn.WithActions(_ziplineOn.Actions.WithSlide(Slide.On));
            Start(rules, OneLine(), new ZiplineTuning(grabRadius: 3f));
            for (var i = 0; i < 5; i++)
            {
                Send(InputButtons.Sprint, new Vector2(0f, 1f));
            }

            Send(InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide, new Vector2(0f, 1f));
            Assert.That(_movement.IsSliding(1), Is.True, "the player should be sliding before the slide ends");
            for (var i = 0; i < _mostRideTicks && _movement.IsSliding(1); i++)
            {
                Send(InputButtons.Sprint | InputButtons.Crouch | InputButtons.Slide, new Vector2(0f, 1f));
            }

            Assert.That(_movement.IsSliding(1), Is.False, "the slide ends on its own");

            Send(InputButtons.Jump);
            Assert.That(_movement.IsZiplining(1), Is.True, "the player should grab the line after the slide");
            SendIdle(10);

            var ends = _log.ToString().Split('\n');
            var count = Array.FindAll(ends, line => line.Contains("[Net] slide owner=1") && line.Contains(" end reason=")).Length;
            Assert.That(count, Is.EqualTo(1));
        }

        private static NetMapZiplines OneLine()
        {
            return Line(_lineStart, _lineEnd);
        }

        private static NetMapZiplines Line(Vector3 start, Vector3 end, ZiplineSpec spec = null)
        {
            return new NetMapZiplines(new[] { new MapZipline(start, end, spec ?? ZiplineSpec.Default) });
        }

        // 線の終わり (z 20) の先で落ちても床があるよう、床は 60 m 四方
        private static WorldMap Floor()
        {
            return new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(30f, _floorTop, 30f), Quaternion.Identity),
            });
        }

        private static ServerMapData Map(WorldMap world, NetMapZiplines ziplines)
        {
            return new ServerMapData("NetTest", world, TestMaps.NavMesh(), "memory", ziplines: ziplines);
        }

        private int RideToTheEnd()
        {
            var ticks = 0;
            while (_movement.IsZiplining(1) && ticks < _mostRideTicks)
            {
                Send(InputButtons.None);
                ticks++;
            }

            Assert.That(_movement.IsZiplining(1), Is.False, "the ride ends");
            return ticks;
        }

        private void Start(GameRules rules, NetMapZiplines ziplines, ZiplineTuning zipline = null)
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
                defaults.Parachute,
                defaults.Slide,
                zipline ?? ZiplineTuning.Default,
                rules,
                defaults.KillPlaneY,
                _spawn);
            _movement = new ServerMovement(settings);
            _host = new ServerHost();
            _host.Register(_movement);
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort);
            var context = new ServerContext(
                options, "0.2.0", _log, Map(Floor(), ziplines), _match, new ServerMessenger(_transport), new StubControl());
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
