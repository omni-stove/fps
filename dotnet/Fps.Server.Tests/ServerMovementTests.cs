using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerMovementTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const int _settleTicks = 30;
        private const float _floorTop = 0.5f;

        private static readonly DateTime _time = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);
        private static readonly ConnectionId _connection = new ConnectionId(1);

        private readonly MessageTable _clientTable = MatchMessages.CreateClientTable();

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMovement _movement;
        private ServerSnapshots _snapshots;
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

        private void Start(GameRules rules = null)
        {
            _transport?.Dispose();
            _transport = new RecordingTransport();
            _match = new ServerMatch(5f, _log, new ServerEventWriter(false, _log, _log), () => _time);
            var settings = ServerMotorSettings.Default.WithSpawnPosition(_spawn).WithRules(rules ?? GameRules.Default);
            _movement = new ServerMovement(settings);
            _snapshots = new ServerSnapshots(settings.Rules, new ISnapshotContributor[] { _movement });
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_snapshots);
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort);
            _host.Start(new ServerContext(options, "0.2.0", _log, TestMaps.Data(), _match, new ServerMessenger(_transport)));
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
