using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 床の上 (上面 y = 0.5) に立ち、+Z の先 z = 7.5 に上の縁が y = 3 の壁 (当たりの Id 2) がある。
    // 目 (y ≈ 2.18) から水平に撃つと縁から 1.5 m 以内に掛かるので縁へ寄り、止まる位置は (0, 3 − 0.8, 7.5 − 0.55)。
    public sealed class ServerMovementGrappleTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _settleTicks = 30;
        private const int _maxPullTicks = TickClock.DefaultTickRate * 2;
        private const string _grappleId = "grapple";

        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);
        private static readonly Vector3 _hang = new Vector3(0f, 3f - GrappleSpec.DefaultHangHeight, 7.5f - (0.5f + GrappleSpec.SurfaceGap));
        private static readonly ConnectionId _owner = new ConnectionId(1);
        private static readonly ConnectionId _watcher = new ConnectionId(2);

        private readonly MessageTable _clientTable = MatchMessages.CreateClientTable();

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMovement _movement;
        private bool _equipped;
        private int _nextInputTick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _equipped = true;
        }

        [TearDown]
        public void TearDown()
        {
            _transport?.Dispose();
            _log.Dispose();
        }

        [Test]
        public void TheHookCatchesTheWallEdgeAndPullsThePlayerThereForEveryoneToSee()
        {
            Start();
            Settle();
            _movement.TryGetState(1, out var before);

            Send(InputButtons.Grapple);

            Assert.That(_movement.IsGrappling(1), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] attached surface=collider-2 distance=7.50 edge=true owner=1"));
            Assert.That(_movement.TryGetGrappleCharges(1, out var charges), Is.True);
            Assert.That(charges, Is.EqualTo(1), "attaching spends one of the two charges");

            var state = PullUntilHanging();
            Assert.That(state.Position.Z, Is.GreaterThan(before.Position.Z + 6f));
            AssertNear(state.Position, _hang, 0.1f);
            Assert.That(state.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(_log.ToString(), Does.Contain("[Grapple] arrived ").And.Contain(" wall=true owner=1"));

            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            var seen = SnapshotFor(_watcher);
            var row = seen.Players[seen.Players.IndexOf(1)];
            Assert.That(row.Position, Is.EqualTo(state.Position), "the other client sees the pulled player");
            Assert.That(row.IsGrounded, Is.False);
        }

        [Test]
        public void TheHangEndsAfterTheHangSecondsAndThePlayerFalls()
        {
            Start();
            Settle();
            Send(InputButtons.Grapple);
            var hanging = PullUntilHanging();

            var ticks = 0;
            while (_movement.IsGrappling(1) && ticks < _maxPullTicks)
            {
                Send(InputButtons.None);
                ticks++;
            }

            Assert.That(ticks * _tickSeconds, Is.EqualTo(GrappleSpec.DefaultHangSeconds).Within(_tickSeconds * 1.5f));
            Assert.That(_log.ToString(), Does.Contain("[Grapple] released reason=Dropped speed=0.00 owner=1"));
            for (var i = 0; i < 10; i++)
            {
                Send(InputButtons.None);
            }

            _movement.TryGetState(1, out var after);
            Assert.That(after.Position.Y, Is.LessThan(hanging.Position.Y));
        }

        [Test]
        public void JumpingWhileHangingHopsOff()
        {
            Start();
            Settle();
            Send(InputButtons.Grapple);
            PullUntilHanging();

            Send(InputButtons.Jump);

            Assert.That(_movement.IsGrappling(1), Is.False);
            _movement.TryGetState(1, out var state);
            var hop = MathF.Sqrt(2f * MotorSpec.DefaultGravity * MotorSpec.DefaultJumpHeight);
            Assert.That(state.Velocity.Y, Is.EqualTo(hop).Within(1e-3f));
            Assert.That(_log.ToString(), Does.Contain("[Grapple] released reason=Jumped"));
        }

        [Test]
        public void JumpingWhilePulledLetsGoWithTheMomentum()
        {
            Start();
            Settle();
            Send(InputButtons.Grapple);
            for (var i = 0; i < 10; i++)
            {
                Send(InputButtons.None);
            }

            Send(InputButtons.Jump);

            Assert.That(_movement.IsGrappling(1), Is.False);
            _movement.TryGetState(1, out var state);
            Assert.That(state.Velocity.Z, Is.GreaterThan(5f), "the pull's speed toward the wall is kept");
            Assert.That(_log.ToString(), Does.Contain("[Grapple] released reason=Jumped"));
        }

        [Test]
        public void AMissIsLoggedAndSpendsNoCharge()
        {
            Start();
            Settle();

            Send(InputButtons.Grapple, MathF.PI);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] miss reason=OutOfRange surface=- distance=- owner=1"));
            _movement.TryGetGrappleCharges(1, out var charges);
            Assert.That(charges, Is.EqualTo(2));
        }

        [Test]
        public void ASurfaceTheDataDoesNotLetGrabIsMissed()
        {
            Start(grapple: Grapple(grabbableLayers: 1 << 4));
            Settle();

            Send(InputButtons.Grapple);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] miss reason=NotGrabbable surface=collider-2 distance=7.50 owner=1"));
        }

        [Test]
        public void TheRangeComesFromTheData()
        {
            Start(grapple: Grapple(maxRange: 5f));
            Settle();

            Send(InputButtons.Grapple);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] miss reason=OutOfRange"));
        }

        [Test]
        public void AimingUpUsesThePitch()
        {
            Start();
            Settle();

            // 真上には何も無い
            Send(InputButtons.Grapple, 0f, InputFrame.MaxPitch);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] miss reason=OutOfRange"));
        }

        [Test]
        public void HoldingTheButtonDoesNotFireAgainAndTheChargesRunOut()
        {
            Start(grapple: Grapple(maxCharges: 1, rechargeSeconds: 100f, cooldownSeconds: 0f));
            Settle();
            Send(InputButtons.Grapple);
            PullUntilHanging(InputButtons.Grapple);
            while (_movement.IsGrappling(1))
            {
                Send(InputButtons.Grapple);
            }

            Send(InputButtons.Grapple);
            Assert.That(_movement.IsGrappling(1), Is.False, "the held button is not a new press");
            Assert.That(_log.ToString(), Does.Not.Contain("[Grapple] refused "));

            Send(InputButtons.None);
            Send(InputButtons.Grapple);
            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] refused reason=NoCharges owner=1"));

            Assert.That(_movement.RefillGrapple(1), Is.True);
            Assert.That(_movement.RefillGrapple(9), Is.False);
            _movement.Teleport(1, _spawn);
            Settle();
            Send(InputButtons.Grapple);
            Assert.That(_movement.IsGrappling(1), Is.True, "a refilled slot fires again");
        }

        [Test]
        public void TheCooldownComesFromTheData()
        {
            Start(grapple: Grapple(maxCharges: 3, cooldownSeconds: 10f));
            Settle();
            Send(InputButtons.Grapple);
            _movement.Teleport(1, _spawn);
            Settle();

            Send(InputButtons.Grapple);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] refused reason=CoolingDown owner=1"));
        }

        [Test]
        public void TheRulesMustTurnAbilitiesOn()
        {
            Start(GameRules.Default);
            Settle();

            Send(InputButtons.Grapple);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] refused reason=RuleOff owner=1"));
        }

        [Test]
        public void APlayerWithoutTheGrappleEquippedCannotFireIt()
        {
            _equipped = false;
            Start();
            Settle();

            Send(InputButtons.Grapple);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] refused reason=NotEquipped owner=1"));
        }

        [Test]
        public void WithoutGrappleDataTheButtonDoesNothing()
        {
            Start(useGrapple: false);
            Settle();
            _movement.TryGetState(1, out var before);

            Send(InputButtons.Grapple);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_movement.Grapple, Is.Null);
            Assert.That(_movement.TryGetGrappleCharges(1, out _), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Grapple]"));
            _movement.TryGetState(1, out var after);
            Assert.That(after.Position.Z, Is.EqualTo(before.Position.Z).Within(1e-4f));
        }

        [Test]
        public void TeleportingLetsGo()
        {
            Start();
            Settle();
            Send(InputButtons.Grapple);

            Assert.That(_movement.Teleport(1, _spawn), Is.True);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] released reason=Interrupted"));
        }

        [Test]
        public void GoingDownLetsGo()
        {
            Start();
            Settle();
            Send(InputButtons.Grapple);

            _movement.SetDowned(1, true);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] released reason=Interrupted"));
        }

        [Test]
        public void LosingTheGrappleMidPullLetsGo()
        {
            Start();
            Settle();
            Send(InputButtons.Grapple);

            _equipped = false;
            Send(InputButtons.None);

            Assert.That(_movement.IsGrappling(1), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Grapple] released reason=Interrupted"));
        }

        [Test]
        public void UseGrappleNeedsTheDataAndTheEquipQuery()
        {
            Start(useGrapple: false);

            Assert.Throws<ArgumentNullException>(() => _movement.UseGrapple(null, _ => true));
            Assert.Throws<ArgumentNullException>(() => _movement.UseGrapple(Grapple(), null));
        }

        [Test]
        public void TheGrappleIsReadFromTheTuningDirectory()
        {
            using var directory = new TempDirectory();
            var tuning = Path.Combine(directory.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerMovement.GrappleFileName), "{\"id\": \"hook\", \"maxRange\": 12, \"maxCharges\": 3}");

            Assert.That(ServerMovement.TryLoadGrapple(new[] { directory.Path }, out var grapple, out var path, out var error), Is.True, error);
            Assert.That(grapple.Id, Is.EqualTo("hook"));
            Assert.That(grapple.Spec.MaxRange, Is.EqualTo(12f));
            Assert.That(grapple.Ability.MaxCharges, Is.EqualTo(3));
            Assert.That(path, Is.EqualTo(Path.GetFullPath(Path.Combine(tuning, ServerMovement.GrappleFileName))));
        }

        [Test]
        public void AMissingGrappleFileOffersNoGrappleAndABrokenOneStopsTheServer()
        {
            using var empty = new TempDirectory();
            Assert.That(ServerMovement.TryLoadGrapple(new[] { empty.Path }, out var none, out var nonePath, out var noneError), Is.True);
            Assert.That(none, Is.Null);
            Assert.That(nonePath, Is.Null);
            Assert.That(noneError, Is.Null);

            using var broken = new TempDirectory();
            var tuning = Path.Combine(broken.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerMovement.GrappleFileName), "{\"id\": \"hook\", \"minRange\": 50}");

            Assert.That(ServerMovement.TryLoadGrapple(new[] { broken.Path }, out var grapple, out _, out var error), Is.False);
            Assert.That(grapple, Is.Null);
            Assert.That(error, Does.StartWith("cannot read grapple tuning: ").And.Contain("minRange"));
        }

        [Test]
        public void TheCommittedGrappleMatchesTheDemoAbility()
        {
            Assert.That(ServerMovement.TryLoadGrapple(new[] { RepositoryRoot() }, out var grapple, out _, out var error), Is.True, error);

            // Demo の AbilityAssetBuilder (GrappleAbilityId・GrappleMaxCharges・GrappleRechargeSeconds・GrappleCooldownSeconds・GrappleGrabbableLayers) と同じ値
            Assert.That(grapple.Id, Is.EqualTo("grapple"));
            Assert.That(grapple.Ability.MaxCharges, Is.EqualTo(2));
            Assert.That(grapple.Ability.RechargeSeconds, Is.EqualTo(8f));
            Assert.That(grapple.Ability.CooldownSeconds, Is.EqualTo(1f));
            Assert.That(grapple.GrabbableLayers, Is.EqualTo(1));
            Assert.That(grapple.Spec.MaxRange, Is.EqualTo(GrappleSpec.DefaultMaxRange));
            Assert.That(grapple.Spec.HangSeconds, Is.EqualTo(GrappleSpec.DefaultHangSeconds));
        }

        [Test]
        public void TheLogLinesNameThePlayerAndTick()
        {
            var grapple = Grapple();
            var target = new GrappleTarget(4, 0, Vector3.Zero, -Vector3.UnitZ, 3.25f, true);

            Assert.That(ServerGrappleLog.Attached(2, 7, target), Is.EqualTo("[Grapple] attached surface=collider-4 distance=3.25 edge=true owner=2 tick=7"));
            Assert.That(
                ServerGrappleLog.Miss(2, 7, GrappleMiss.OutOfRange, target), Is.EqualTo("[Grapple] miss reason=OutOfRange surface=- distance=- owner=2 tick=7"));
            Assert.That(
                ServerGrappleLog.Miss(2, 7, GrappleMiss.TooClose, target), Is.EqualTo("[Grapple] miss reason=TooClose surface=collider-4 distance=3.25 owner=2 tick=7"));
            Assert.That(ServerGrappleLog.Arrived(2, 7, 0.5f, true), Is.EqualTo("[Grapple] arrived seconds=0.50 wall=true owner=2 tick=7"));
            Assert.That(ServerGrappleLog.Released(2, 7, GrappleEnd.Dropped, 0f), Is.EqualTo("[Grapple] released reason=Dropped speed=0.00 owner=2 tick=7"));
            Assert.That(ServerGrappleLog.Refused(2, 7, ServerGrappleRefusal.NoCharges), Is.EqualTo("[Grapple] refused reason=NoCharges owner=2 tick=7"));
            Assert.That(ServerGrappleLog.Loaded(grapple, null), Is.EqualTo("[Grapple] grapple id=grapple range=2-30 charges=2 from -"));
        }

        private CharacterMotorState PullUntilHanging(InputButtons held = InputButtons.None)
        {
            for (var i = 0; i < _maxPullTicks && !_movement.IsHanging(1); i++)
            {
                Assert.That(_movement.IsGrappling(1), Is.True, "the pull ended before it arrived");
                Send(held);
            }

            Assert.That(_movement.IsHanging(1), Is.True, "the player never arrived at the wall");
            _movement.TryGetState(1, out var state);
            return state;
        }

        private static GrappleType Grapple(
            float maxRange = GrappleSpec.DefaultMaxRange,
            int grabbableLayers = GrappleType.DefaultGrabbableLayers,
            int maxCharges = 2,
            float rechargeSeconds = 8f,
            float cooldownSeconds = 0f)
        {
            return new GrappleType(
                _grappleId,
                new GrappleSpec(maxRange: maxRange),
                new AbilitySpec(maxCharges, rechargeSeconds, cooldownSeconds, 0f),
                grabbableLayers);
        }

        private void Start(GameRules rules = null, GrappleType grapple = null, bool useGrapple = true)
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
                rules ?? GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On)),
                defaults.KillPlaneY,
                _spawn);
            _movement = new ServerMovement(settings, signals: new ServerCheatSignals());
            if (useGrapple)
            {
                _movement.UseGrapple(grapple ?? Grapple(), _ => _equipped);
            }

            var snapshots = new ServerSnapshots(settings.Rules, new ISnapshotContributor[] { _movement }, TestAntiCheat.Off);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(snapshots);
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort);
            var map = new ServerMapData("NetTest", World(), TestMaps.NavMesh(), "memory");
            var context = new ServerContext(options, "0.2.0", _log, map, _match, new ServerMessenger(_transport), new StubControl());
            _host.Start(context);
            _match.Join(_owner, "owner");
            _host.OnConnected(_owner);
            _match.Join(_watcher, "watcher");
            _host.OnConnected(_watcher);
            _nextInputTick = 0;
        }

        private static WorldMap World()
        {
            return new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(20f, 0.5f, 20f), Quaternion.Identity),
                WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(0f, 1.75f, 8f), new Vector3(10f, 1.25f, 0.5f), Quaternion.Identity),
            });
        }

        private void Settle()
        {
            for (var i = 0; i < _settleTicks; i++)
            {
                Send(InputButtons.None);
            }

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsGrounded, Is.True, "the player should stand on the floor before the test");
        }

        // 1 tick ずつ送り、そのたびに 1 tick 進めて入力の予算と回数の時計を進める (クライアントが tick の速さで送るのと同じ)
        private void Send(InputButtons buttons, float yaw = 0f, float pitch = 0f)
        {
            var message = new InputMessage { AckedSnapshotTick = -1 };
            message.Add(new InputFrame(_nextInputTick, Vector2.Zero, yaw, buttons, pitch));
            _movement.OnMessage(_owner, message);
            _nextInputTick++;
            _movement.Tick(_nextInputTick, _tickSeconds);
        }

        private SnapshotMessage SnapshotFor(ConnectionId connection)
        {
            foreach (var sent in _transport.Sent)
            {
                if (sent.Connection == connection && _clientTable.Read(new ArraySegment<byte>(sent.Payload)) is SnapshotMessage snapshot)
                {
                    return snapshot;
                }
            }

            Assert.Fail("no snapshot was sent to client " + connection.Value);
            return null;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, float tolerance)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tolerance));
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName)))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
