using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerMovementTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _settleTicks = 30;
        private const float _floorTop = 0.5f;

        private static readonly DateTime _time = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);
        private static readonly ConnectionId _connection = new ConnectionId(1);

        // 定数配列を直接渡すと CA1861 になるので、欄に置く
        private static readonly int[] _twoAirJumpsThenNone = { 1, 2, 2 };

        private readonly MessageTable _clientTable = MatchMessages.CreateClientTable();

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMovement _movement;
        private ServerSnapshots _snapshots;
        private ServerContext _context;
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
        public void TheSameInputsGiveTheSameFixedPositions()
        {
            var first = WalkForwardOneSecond();
            var second = WalkForwardOneSecond();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.IsGrounded, Is.True);
            Assert.That(first.Position.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(first.Position.Y, Is.EqualTo(_floorTop + 1f + CapsuleMoverSpec.DefaultSkinWidth).Within(1e-3f));
            Assert.That(first.Position.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed).Within(0.01f));
            Assert.That(first.Velocity.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed).Within(1e-4f));
        }

        [Test]
        public void ResentTicksAreProcessedOnlyOnce()
        {
            Start();
            var once = Message(-1, Frame(0, new Vector2(0f, 1f)), Frame(1, new Vector2(0f, 1f)), Frame(2, new Vector2(0f, 1f)));
            _movement.OnMessage(_connection, once);
            _movement.TryGetState(1, out var afterThree);

            var again = Message(-1, Frame(1, new Vector2(0f, 1f)), Frame(2, new Vector2(0f, 1f)), Frame(3, new Vector2(0f, 1f)));
            _movement.OnMessage(_connection, again);
            _movement.TryGetState(1, out var afterFour);

            Assert.That(_movement.LastProcessedInputTick(1), Is.EqualTo(3));
            Start();
            _movement.OnMessage(_connection, once);
            _movement.OnMessage(_connection, Message(-1, Frame(3, new Vector2(0f, 1f))));
            _movement.TryGetState(1, out var straight);
            Assert.That(afterFour, Is.EqualTo(straight));
            Assert.That(afterFour, Is.Not.EqualTo(afterThree));
        }

        [Test]
        public void InputFasterThanTheTickRateIsDropped()
        {
            Start();
            var budget = (int)ServerMovement.MaxInputBudget;
            for (var sent = 0; sent < budget + InputMessage.MaxFrames; sent += InputMessage.MaxFrames)
            {
                var message = new InputMessage();
                for (var i = 0; i < InputMessage.MaxFrames; i++)
                {
                    message.Add(Frame(sent + i, Vector2.Zero));
                }

                _movement.OnMessage(_connection, message);
            }

            Assert.That(_movement.LastProcessedInputTick(1), Is.EqualTo(budget - 1));
            Assert.That(_log.ToString(), Does.Contain("[Net] dropped input from client=1 tick=" + budget + ": sending faster than the tick rate"));

            _host.Tick(_tickSeconds);
            _movement.OnMessage(_connection, Message(-1, Frame(budget, Vector2.Zero), Frame(budget + 1, Vector2.Zero)));
            Assert.That(_movement.LastProcessedInputTick(1), Is.EqualTo(budget), "one tick refills one input");
        }

        [Test]
        public void ActionsTheRulesTurnedOffAreIgnored()
        {
            Start();
            Settle();

            Send(new Vector2(1f, 0f), InputButtons.Dodge);
            Send(Vector2.Zero, InputButtons.Cover);

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsDodging, Is.False);
            Assert.That(state.Cover.IsInCover, Is.False);
        }

        [Test]
        public void DodgeIsAllowedWhenTheRulesTurnItOn()
        {
            Start(GameRules.Default.WithActions(ActionRules.Default.WithDodge(Dodge.On)));
            Settle();

            Send(new Vector2(1f, 0f), InputButtons.Dodge);

            _movement.TryGetState(1, out var state);
            Assert.That(state.IsDodging, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Net] dodge owner=1 tick=" + (_nextInputTick - 1) + " dodging=true"));
        }

        [Test]
        public void CrouchingIsLoggedLikeTheUnityServer()
        {
            Start();
            Settle();

            Send(Vector2.Zero, InputButtons.Crouch);

            Assert.That(_log.ToString(), Does.Contain("[Net] stance owner=1 tick=" + (_nextInputTick - 1) + " crouched=true"));
        }

        [Test]
        public void ProneIsIgnoredWhenTheRulesTurnItOff()
        {
            Start();
            Settle();

            Send(Vector2.Zero, InputButtons.Prone);

            _movement.TryGetState(1, out var state);
            Assert.That(state.IsProne, Is.False);
        }

        [Test]
        public void ProneLowersThePlayerAndIsLoggedAndSent()
        {
            Start(GameRules.Default.WithActions(ActionRules.Default.WithProne(Prone.On)));
            Settle();

            Send(Vector2.Zero, InputButtons.Prone);
            var proneTick = _nextInputTick - 1;
            _movement.TryGetState(1, out var state);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Prone));
            Assert.That(_log.ToString(), Does.Contain("[Net] prone owner=1 tick=" + proneTick + " prone=true"));

            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            var row = OnlySnapshot().Players[0];
            Assert.That(row.IsProne, Is.True);
            Assert.That(row.IsCrouched, Is.False);
            Assert.That(row.Prone.IsActive, Is.True);

            Send(Vector2.Zero, InputButtons.Prone);
            _movement.TryGetState(1, out state);
            Assert.That(state.Stance, Is.EqualTo(PlayerStance.Standing));
            Assert.That(_log.ToString(), Does.Contain("[Net] prone owner=1 tick=" + (_nextInputTick - 1) + " prone=false"));
        }

        [Test]
        public void WalkingIntoTheMapLadderClimbsItAndIsLoggedAndSent()
        {
            Start(GameRules.Default.WithActions(ActionRules.Default.WithLadder(Ladder.On)), TestMaps.OneLadder());
            Settle();

            CharacterMotorState state = default;
            for (var i = 0; i < 120 && !state.IsClimbing; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.None);
                _movement.TryGetState(1, out state);
            }

            Assert.That(state.IsClimbing, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Net] ladder owner=1 tick=" + (_nextInputTick - 1) + " climbing=true ladder=0"));

            var startY = state.Position.Y;
            for (var i = 0; i < 20; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.None);
            }

            _movement.TryGetState(1, out state);
            Assert.That(state.Position.Y - startY, Is.EqualTo(LadderSpec.DefaultClimbSpeed * 20 * _tickSeconds).Within(1e-3f));

            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            var row = OnlySnapshot().Players[0];
            Assert.That(row.Ladder, Is.EqualTo(state.Ladder));

            Send(Vector2.Zero, InputButtons.Jump);
            _movement.TryGetState(1, out state);
            Assert.That(state.IsClimbing, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] ladder owner=1 tick=" + (_nextInputTick - 1) + " climbing=false"));
        }

        [Test]
        public void TheLeanBitsTiltThePlayerAndAreLoggedAndSent()
        {
            Start();
            Settle();

            var rightTick = _nextInputTick;
            for (var i = 0; i < 20; i++)
            {
                Send(Vector2.Zero, InputButtons.LeanRight);
            }

            _movement.TryGetState(1, out var state);
            Assert.That(state.Lean.Amount, Is.EqualTo(1f));
            Assert.That(_log.ToString(), Does.Contain("[Net] lean owner=1 tick=" + rightTick + " side=right"));
            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            Assert.That(OnlySnapshot().Players[0].Lean, Is.EqualTo(1f));

            for (var i = 0; i < 40; i++)
            {
                Send(Vector2.Zero, InputButtons.LeanLeft);
            }

            _movement.TryGetState(1, out state);
            Assert.That(state.Lean.Amount, Is.EqualTo(-1f));
            Assert.That(_log.ToString(), Does.Contain(" side=left"));

            for (var i = 0; i < 20; i++)
            {
                Send(Vector2.Zero, InputButtons.LeanLeft | InputButtons.LeanRight);
            }

            _movement.TryGetState(1, out state);
            Assert.That(state.Lean.Amount, Is.Zero, "both lean bits cancel out");
            Assert.That(_log.ToString(), Does.Contain(" side=none"));
        }

        [Test]
        public void WithoutTheLeanBitsThePlayerStaysUpright()
        {
            Start();
            Settle();

            for (var i = 0; i < 20; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.Sprint);
            }

            _movement.TryGetState(1, out var state);
            Assert.That(state.Lean.Amount, Is.Zero);
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] lean "));
        }

        [Test]
        public void ThePlayerSwimsInTheMapWaterAndIsLoggedAndSent()
        {
            Start(water: TestMaps.OnePool());

            CharacterMotorState state = default;
            for (var i = 0; i < 10 && !state.IsSwimming; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
                _movement.TryGetState(1, out state);
            }

            Assert.That(state.IsSwimming, Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Net] swim owner=1 tick=" + (_nextInputTick - 1) + " swimming=true"));

            for (var i = 0; i < 2 * TickClock.DefaultTickRate; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }

            // 水面 3.5 が胸 (本体の 0.3 上) に来る高さに浮く
            _movement.TryGetState(1, out state);
            Assert.That(state.IsSwimming, Is.True);
            Assert.That(state.Position.Y, Is.EqualTo(3.2f).Within(1e-3f));
            Assert.That(state.Swim.IsUnderwater, Is.False);
            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            Assert.That(OnlySnapshot().Players[0].Swimming, Is.True);

            for (var i = 0; i < 30; i++)
            {
                Send(Vector2.Zero, InputButtons.Crouch);
            }

            _movement.TryGetState(1, out state);
            Assert.That(state.Swim.IsUnderwater, Is.True, "crouching dives");
            Assert.That(_log.ToString(), Does.Contain(" underwater=true"));
        }

        [Test]
        public void WithoutMapWaterThePlayerDoesNotSwim()
        {
            Start();
            Settle();

            _movement.TryGetState(1, out var state);
            Assert.That(state.IsSwimming, Is.False);
            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            Assert.That(OnlySnapshot().Players[0].Swimming, Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] swim "));
        }

        [Test]
        public void HoldingUnderwaterPastTheBreathReportsTheDrowningsAndTheirDamage()
        {
            Start(water: TestMaps.OnePool());
            var drowned = new List<int>();
            _movement.Drowned += (player, tick, count) => drowned.Add(count);

            var seconds = SwimTuning.DefaultBreathSeconds + (2.5f * SwimTuning.DefaultDrownIntervalSeconds);
            for (var i = 0; i < seconds * TickClock.DefaultTickRate; i++)
            {
                Send(Vector2.Zero, InputButtons.Crouch);
            }

            var total = 0;
            drowned.ForEach(count => total += count);
            Assert.That(total, Is.EqualTo(3), "one at the end of the breath, then one per interval");
            Assert.That(_log.ToString(), Does.Contain(" outOfBreath=1"));
            Assert.That(_movement.DrownDamage(total), Is.EqualTo(3 * SwimTuning.DefaultDrownDamage));
            Assert.That(_movement.DrownDamage(int.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => _movement.DrownDamage(-1));
        }

        [Test]
        public void AirJumpsAreAllowedAsManyTimesAsTheMotorSays()
        {
            Start(motor: new MotorSpec(
                MotorSpec.DefaultWalkSpeed, MotorSpec.DefaultSprintSpeed, MotorSpec.DefaultJumpHeight, MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl, MotorSpec.DefaultCrouchSpeed, airJumps: 2));
            Settle();

            Send(Vector2.Zero, InputButtons.Jump);
            var used = new List<int>();
            var speeds = new List<float>();
            for (var press = 0; press < 3; press++)
            {
                Send(Vector2.Zero, InputButtons.None);
                Send(Vector2.Zero, InputButtons.Jump);
                _movement.TryGetState(1, out var state);
                Assert.That(state.IsGrounded, Is.False);
                used.Add(state.AirJumpsUsed);
                speeds.Add(state.Velocity.Y);
            }

            Assert.That(used, Is.EqualTo(_twoAirJumpsThenNone));
            Assert.That(speeds[1], Is.EqualTo(MotorSpec.DefaultAirJumpSpeed).Within(0.5f), "the second air jump is taken");
            Assert.That(speeds[2], Is.LessThan(speeds[1]), "the third press finds no air jumps left");
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Net] airjump owner=1 tick=" + (_nextInputTick - 5) + " used=1"));
            Assert.That(log, Does.Contain("[Net] airjump owner=1 tick=" + (_nextInputTick - 3) + " used=2"));
            Assert.That(log, Does.Not.Contain(" used=3"));

            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            Assert.That(OnlySnapshot().Players[0].AirJumpsUsed, Is.EqualTo(2));

            for (var i = 0; i < 2 * TickClock.DefaultTickRate; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }

            _movement.TryGetState(1, out var landed);
            Assert.That(landed.IsGrounded, Is.True);
            Assert.That(landed.AirJumpsUsed, Is.Zero, "landing gives the air jumps back");
        }

        [Test]
        public void StandingFillsTheJumpGraceFromTheMotorAndTheSnapshotCarriesIt()
        {
            Start(motor: new MotorSpec(
                MotorSpec.DefaultWalkSpeed, MotorSpec.DefaultSprintSpeed, MotorSpec.DefaultJumpHeight, MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl, MotorSpec.DefaultCrouchSpeed, jumpGraceSeconds: 0.1f));
            Settle();

            _movement.TryGetState(1, out var standing);
            Assert.That(standing.IsGrounded, Is.True);
            Assert.That(standing.JumpGraceLeft, Is.EqualTo(0.1f));
            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            Assert.That(OnlySnapshot().Players[0].JumpGraceLeft, Is.EqualTo(0.1f));

            Send(Vector2.Zero, InputButtons.Jump);
            _movement.TryGetState(1, out var jumped);
            Assert.That(jumped.IsGrounded, Is.False);
            Assert.That(jumped.JumpGraceLeft, Is.Zero, "the jump uses up the grace");
        }

        [Test]
        public void WithoutAirJumpsASecondPressDoesNothing()
        {
            Start();
            Settle();

            Send(Vector2.Zero, InputButtons.Jump);
            Send(Vector2.Zero, InputButtons.None);
            _movement.TryGetState(1, out var before);
            Send(Vector2.Zero, InputButtons.Jump);
            _movement.TryGetState(1, out var after);

            Assert.That(after.AirJumpsUsed, Is.Zero);
            Assert.That(after.Velocity.Y, Is.LessThan(before.Velocity.Y));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] airjump "));
        }

        [Test]
        public void JumpingBesideAWallRunsAlongItAndIsLoggedAndSent()
        {
            // 体 (半径 0.5) の右 0.1 に、前 (+Z) へ続く壁を立てる
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(10f, 0.5f, 10f), Quaternion.Identity),
                WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(0.85f, 4f, 0f), new Vector3(0.25f, 4f, 10f), Quaternion.Identity),
            });
            Start(wallRun: new WallRunTuning(true), world: world);
            Settle();

            for (var i = 0; i < 10; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.Sprint);
            }

            Send(new Vector2(0f, 1f), InputButtons.Sprint | InputButtons.Jump);
            CharacterMotorState state = default;
            for (var i = 0; i < 10 && !state.IsWallRunning; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.Sprint);
                _movement.TryGetState(1, out state);
            }

            Assert.That(state.WallRun.Side, Is.EqualTo(Shooter.Core.WallRunSide.Right));
            Assert.That(_log.ToString(), Does.Contain("[Net] wallrun owner=1 tick=" + (_nextInputTick - 1) + " running=true side=right"));
            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            Assert.That(OnlySnapshot().Players[0].WallRunSide, Is.EqualTo(Shooter.Protocol.WallRunSide.Right));
        }

        [Test]
        public void WallRunningIsOffUnlessTheTuningTurnsItOn()
        {
            var world = new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(10f, 0.5f, 10f), Quaternion.Identity),
                WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(0.85f, 4f, 0f), new Vector3(0.25f, 4f, 10f), Quaternion.Identity),
            });
            Start(world: world);
            Settle();

            Send(new Vector2(0f, 1f), InputButtons.Sprint | InputButtons.Jump);
            for (var i = 0; i < 10; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.Sprint);
                _movement.TryGetState(1, out var state);
                Assert.That(state.IsWallRunning, Is.False);
            }

            Assert.That(_log.ToString(), Does.Not.Contain("[Net] wallrun "));
        }

        [Test]
        public void ADownedPlayerDoesNotWalk()
        {
            Start();
            Settle();
            _movement.TryGetState(1, out var before);

            Assert.That(_movement.SetDowned(1, true), Is.True);
            for (var i = 0; i < 10; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.Sprint | InputButtons.Aim);
            }

            _movement.TryGetState(1, out var after);
            Assert.That(after.Position.Z, Is.EqualTo(before.Position.Z).Within(1e-4f));
            Assert.That(after.IsCrouched, Is.True);
        }

        [Test]
        public void TheHeldWeaponsWeightSlowsTheWalkAndTheOwnerIsToldTheMultiplier()
        {
            var carry = new CarryLoadSpec(CarrySource.Held, CarrySource.None, 1f, 0.1f, 0f, CarryGaits.All);
            Start(motor: new MotorSpec(
                MotorSpec.DefaultWalkSpeed, MotorSpec.DefaultSprintSpeed, MotorSpec.DefaultJumpHeight, MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl, MotorSpec.DefaultCrouchSpeed, carryLoad: carry));
            Settle();
            var heavy = new WeaponSpec(TestRules.RifleId, null, WeaponCategory.AssaultRifle, weight: 6f);
            // 体はふつうルールのモジュールが出現で作るが、このテストは移動のモジュールだけで組んでいる
            _context.Bodies.Add(1).SetWeapons(heavy, new[] { heavy });
            _movement.TryGetState(1, out var before);

            for (var i = 0; i < TickClock.DefaultTickRate; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.None);
            }

            // 6 kg − 1 kg = 5 kg 超え → 1 − 0.5
            _movement.TryGetState(1, out var after);
            Assert.That(after.Position.Z - before.Position.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed * 0.5f).Within(0.01f));

            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            Assert.That(OnlySnapshot().Owner.CarrySpeedMultiplier, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void WithoutWeaponsThePlayerWalksAtFullSpeed()
        {
            var carry = new CarryLoadSpec(CarrySource.Carried, CarrySource.Carried, 0f, 0.1f, 0f, CarryGaits.All);
            Start(motor: new MotorSpec(
                MotorSpec.DefaultWalkSpeed, MotorSpec.DefaultSprintSpeed, MotorSpec.DefaultJumpHeight, MotorSpec.DefaultGravity,
                MotorSpec.DefaultAirControl, MotorSpec.DefaultCrouchSpeed, carryLoad: carry));
            Settle();
            _movement.TryGetState(1, out var before);

            for (var i = 0; i < TickClock.DefaultTickRate; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.None);
            }

            _movement.TryGetState(1, out var after);
            Assert.That(after.Position.Z - before.Position.Z, Is.EqualTo(MotorSpec.DefaultWalkSpeed).Within(0.01f));
        }

        [Test]
        public void LandingReportsTheFallSpeed()
        {
            Start();
            var landings = new List<float>();
            _movement.Landed += (player, tick, speed) => landings.Add(speed);
            Assert.That(_movement.Teleport(1, new Vector3(0f, 10f, 0f)), Is.True);

            for (var i = 0; i < 120 && landings.Count == 0; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }

            Assert.That(landings, Has.Count.EqualTo(1));
            var expected = MathF.Sqrt(2f * MotorSpec.DefaultGravity * (10f - (_floorTop + 1f)));
            Assert.That(landings[0], Is.EqualTo(expected).Within(0.5f));
            Assert.That(_movement.FallDamage(30f, 100), Is.Zero, "fall damage is off by default");
        }

        [Test]
        public void FallDamageFollowsTheRules()
        {
            Start(GameRules.Default.WithFallDamage(FallDamage.On));

            Assert.That(_movement.FallDamage(MotorSpec.DefaultSafeFallSpeed, 100), Is.Zero);
            Assert.That(_movement.FallDamage(MotorSpec.DefaultLethalFallSpeed, 100), Is.EqualTo(100));
        }

        [Test]
        public void TheSnapshotCarriesTheMovedPositionAndTheLastProcessedTick()
        {
            Start();
            Settle();
            for (var i = 0; i < 30; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.Aim);
            }

            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);

            var snapshot = OnlySnapshot();
            Assert.That(snapshot.BaselineTick, Is.EqualTo(SnapshotMessage.NoBaseline));
            Assert.That(snapshot.HasOwner, Is.True);
            Assert.That(snapshot.Owner.LastProcessedInputTick, Is.EqualTo(_nextInputTick - 1));
            Assert.That(snapshot.Players.Count, Is.EqualTo(1));
            var row = snapshot.Players[0];
            _movement.TryGetState(1, out var state);
            Assert.That(row.PlayerId, Is.EqualTo(1));
            Assert.That(row.Team, Is.EqualTo(Team.Alpha));
            Assert.That(row.Position, Is.EqualTo(state.Position));
            Assert.That(row.Position.Z, Is.GreaterThan(2f));
            Assert.That(row.Velocity, Is.EqualTo(state.Velocity));
            Assert.That(row.IsGrounded, Is.True);
            Assert.That(row.IsAiming, Is.True);
        }

        [Test]
        public void AnAcknowledgedSnapshotBecomesTheBaseline()
        {
            Start();
            Settle();
            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);
            var first = OnlySnapshot();
            var kept = new SnapshotMessage();
            kept.CopyFrom(first);

            _host.OnMessage(_connection, Message(first.Tick, Frame(_nextInputTick, new Vector2(0f, 1f))));
            _nextInputTick++;
            _transport.Sent.Clear();
            _host.Tick(_tickSeconds);

            var second = OnlySnapshot(false);
            Assert.That(second.BaselineTick, Is.EqualTo(kept.Tick));
            Assert.That(second.TryResolve(kept), Is.True);
            _movement.TryGetState(1, out var state);
            Assert.That(second.Players[0].Position, Is.EqualTo(state.Position));
            Assert.That(second.Players[0].Team, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void TeleportStandsThePlayerAtThePositionAndRespawnsThereAfterAFall()
        {
            Start();
            Assert.That(_movement.Teleport(1, new Vector3(30f, 1f, 0f)), Is.True);

            var fell = false;
            for (var i = 0; i < 400; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
                _movement.TryGetState(1, out var state);
                fell |= state.Position.Y < ServerMotorSettings.DefaultKillPlaneY / 2f;
                if (fell && state.Position == new Vector3(30f, 1f, 0f))
                {
                    Assert.That(state.Velocity, Is.EqualTo(Vector3.Zero));
                    return;
                }
            }

            Assert.Fail("the player never fell below the kill plane");
        }

        [Test]
        public void UnknownPlayersAreNotMoved()
        {
            Start();

            Assert.That(_movement.Teleport(9, Vector3.Zero), Is.False);
            Assert.That(_movement.SetDowned(9, true), Is.False);
            Assert.That(_movement.TryGetState(9, out _), Is.False);
            Assert.That(_movement.LastProcessedInputTick(9), Is.EqualTo(-1));

            _host.OnDisconnected(_connection);
            Assert.That(_movement.TryGetState(1, out _), Is.False);
        }

        private CharacterMotorState WalkForwardOneSecond()
        {
            Start();
            Settle();
            for (var i = 0; i < TickClock.DefaultTickRate; i++)
            {
                Send(new Vector2(0f, 1f), InputButtons.None);
            }

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            return state;
        }

        private void Start(
            GameRules rules = null,
            NetMapLadders ladders = null,
            MotorSpec motor = null,
            NetMapWater water = null,
            WallRunTuning wallRun = null,
            WorldMap world = null)
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
                wallRun ?? defaults.WallRun,
                rules ?? GameRules.Default,
                defaults.KillPlaneY,
                _spawn);
            _movement = new ServerMovement(settings);
            _snapshots = new ServerSnapshots(settings.Rules, new ISnapshotContributor[] { _movement });
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_snapshots);
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort);
            var map = new ServerMapData("NetTest", world ?? TestMaps.World(), TestMaps.NavMesh(), "memory", ladders: ladders, water: water);
            _context = new ServerContext(options, "0.2.0", _log, map, _match, new ServerMessenger(_transport), new StubControl());
            _host.Start(_context);
            _match.Join(_connection, "key");
            _host.OnConnected(_connection);
            _nextInputTick = 0;
        }

        private void Settle()
        {
            for (var i = 0; i < _settleTicks; i++)
            {
                Send(Vector2.Zero, InputButtons.None);
            }

            Assert.That(_movement.TryGetState(1, out var state), Is.True);
            Assert.That(state.IsGrounded, Is.True, "the player should stand on the floor before the test");
        }

        // 1 tick ずつ送り、そのたびに 1 tick 進めて入力の予算を戻す (クライアントが tick の速さで送るのと同じ)
        private void Send(Vector2 move, InputButtons buttons)
        {
            _movement.OnMessage(_connection, Message(-1, Frame(_nextInputTick, move, buttons)));
            _nextInputTick++;
            _movement.Tick(_nextInputTick, _tickSeconds);
        }

        private SnapshotMessage OnlySnapshot(bool complete = true)
        {
            Assert.That(_transport.Sent, Has.Count.EqualTo(1));
            var sent = _transport.Sent[0];
            Assert.That(sent.Connection, Is.EqualTo(_connection));
            Assert.That(sent.Channel, Is.EqualTo(TransportChannel.Unreliable));
            var snapshot = (SnapshotMessage)_clientTable.Read(new ArraySegment<byte>(sent.Payload));
            Assert.That(snapshot.IsComplete, Is.EqualTo(complete));
            return snapshot;
        }

        private static InputFrame Frame(int tick, Vector2 move, InputButtons buttons = InputButtons.None)
        {
            return new InputFrame(tick, move, 0f, buttons);
        }

        private static InputMessage Message(int ackedSnapshotTick, params InputFrame[] frames)
        {
            var message = new InputMessage { AckedSnapshotTick = ackedSnapshotTick };
            foreach (var frame in frames)
            {
                message.Add(frame);
            }

            return message;
        }
    }
}
