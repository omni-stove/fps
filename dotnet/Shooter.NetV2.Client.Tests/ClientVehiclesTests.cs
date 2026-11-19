using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientVehiclesTests
    {
        private const int _playerId = 3;
        private const int _driverSeat = 0;
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly Vector3 _spawn = new Vector3(0f, 1.1f, 0f);

        private FakeTransport _transport;
        private MatchClient _client;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeTransport();
            _client = new MatchClient(_transport, "0.2.0", "device1", "tester", _ => { });
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _transport.Dispose();
        }

        [Test]
        public void AVehicleDrivenByOthersIsInterpolatedBetweenSnapshots()
        {
            var vehicles = new ClientVehicles(_playerId);
            vehicles.Apply(Snapshot(10, new Vector3(0f, 0f, 0f)));
            vehicles.Apply(Snapshot(12, new Vector3(0f, 0f, 4f)));

            Assert.That(vehicles.TrySample(0, 11f, out var position, out _), Is.True);
            Assert.That(position.Z, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(vehicles.TrySample(1, 11f, out _, out _), Is.False);
        }

        [Test]
        public void TheStageIsToldOnFirstSightAndWhenItChanges()
        {
            var vehicles = new ClientVehicles(_playerId);
            var stages = new List<VehicleDamageStage>();
            vehicles.StageChanged += (_, stage) => stages.Add(stage);

            vehicles.Apply(Snapshot(1, Vector3.Zero, VehicleDamageStage.Destroyed));
            vehicles.Apply(Snapshot(2, Vector3.Zero, VehicleDamageStage.Destroyed));

            Assert.That(stages, Is.EqualTo(new[] { VehicleDamageStage.Destroyed }), "a wreck seen on joining is shown once");
            Assert.That(vehicles.TryGetState(0, out var health, out var stage), Is.True);
            Assert.That(health, Is.EqualTo(500));
            Assert.That(stage, Is.EqualTo(VehicleDamageStage.Destroyed));
        }

        private static readonly int[] ToldOnFirstSightAndChange = { 0, 120, 300 };

        [Test]
        public void ThePartsAreToldOnFirstSightAndWhenTheyChange()
        {
            var vehicles = new ClientVehicles(_playerId);
            var told = new List<int>();
            vehicles.PartsChanged += (_, parts) => told.Add(parts.TakenOf("wing_left"));

            vehicles.Apply(Snapshot(1, Vector3.Zero));
            vehicles.Apply(Snapshot(2, Vector3.Zero));
            vehicles.Apply(Snapshot(3, Vector3.Zero, parts: Taken("wing_left", 120)));
            vehicles.Apply(Snapshot(4, Vector3.Zero, parts: Taken("wing_left", 120)));
            vehicles.Apply(Snapshot(5, Vector3.Zero, parts: Taken("wing_left", 300)));

            Assert.That(told, Is.EqualTo(ToldOnFirstSightAndChange), "the same damage in a new snapshot is not told again");
            Assert.That(vehicles.TryGetParts(0, out var latest), Is.True);
            Assert.That(latest.TakenOf("wing_left"), Is.EqualTo(300));
            Assert.That(vehicles.TryGetParts(1, out var unknown), Is.False);
            Assert.That(unknown.Count, Is.Zero);
        }

        [Test]
        public void AVehicleThatLeavesTheSnapshotIsForgotten()
        {
            var vehicles = new ClientVehicles(_playerId);
            vehicles.Apply(Snapshot(1, Vector3.Zero));

            vehicles.Apply(new SnapshotMessage { Tick = 2 });

            Assert.That(vehicles.Indices, Is.Empty);
        }

        [Test]
        public void TheLocalSeatComesFromTheOwnRow()
        {
            var vehicles = new ClientVehicles(_playerId);
            var changes = new List<VehicleSeatSync>();
            vehicles.LocalSeatChanged += changes.Add;

            vehicles.Apply(Snapshot(1, Vector3.Zero, seat: new VehicleSeatSync(0, _driverSeat)));
            vehicles.Apply(Snapshot(2, Vector3.Zero, seat: new VehicleSeatSync(0, _driverSeat)));
            vehicles.Apply(Snapshot(3, Vector3.Zero));

            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[0].SameAs(new VehicleSeatSync(0, _driverSeat)), Is.True);
            Assert.That(changes[1].IsRiding, Is.False);
        }

        [Test]
        public void OnlyTheDriverSendsItsPoseAndCarriesTheLastCorrection()
        {
            var vehicles = new ClientVehicles(_playerId);
            var move = new VehicleMoveMessage();
            Assert.That(vehicles.FillMove(move, _driverSeat, Vector3.One, Quaternion.Identity, Vector3.Zero), Is.False, "not riding");

            vehicles.Apply(Snapshot(1, Vector3.Zero, seat: new VehicleSeatSync(0, 1)));
            Assert.That(vehicles.FillMove(move, _driverSeat, Vector3.One, Quaternion.Identity, Vector3.Zero), Is.False, "a passenger does not drive");

            vehicles.Apply(Snapshot(2, Vector3.Zero, seat: new VehicleSeatSync(0, _driverSeat)));
            var corrected = new List<Vector3>();
            vehicles.Corrected += (_, position, _, _) => corrected.Add(position);
            vehicles.Correct(new VehicleCorrectedMessage { Vehicle = 0, Generation = 4, Position = new Vector3(1f, 2f, 3f), Rotation = Quaternion.Identity });

            Assert.That(vehicles.FillMove(move, _driverSeat, Vector3.One, Quaternion.Identity, Vector3.UnitZ), Is.True);
            Assert.That(move.Vehicle, Is.EqualTo(0));
            Assert.That(move.Sequence, Is.EqualTo(0));
            Assert.That(move.CorrectionGeneration, Is.EqualTo(4));
            Assert.That(move.Velocity, Is.EqualTo(Vector3.UnitZ));
            Assert.That(corrected, Is.EqualTo(new[] { new Vector3(1f, 2f, 3f) }));
            vehicles.FillMove(move, _driverSeat, Vector3.One, Quaternion.Identity, Vector3.Zero);
            Assert.That(move.Sequence, Is.EqualTo(1));
        }

        [Test]
        public void WhileRidingTheMatchSendsNoWalkingAndFollowsTheServersSeat()
        {
            var match = Join();
            var seat = new Vector3(4f, 2f, 0f);
            Deliver(2, seat, riding: true, processed: -1);
            Assert.That(match.Vehicles.LocalSeat.IsRiding, Is.True);

            for (var i = 0; i < 5; i++)
            {
                match.Step(new Vector2(0f, 1f), 0.5f, InputButtons.Sprint | InputButtons.Jump | InputButtons.Aim);
            }

            match.SendInput();
            var input = _transport.LastInput;
            Assert.That(input.Count, Is.EqualTo(5));
            Assert.That(input[0].Move, Is.EqualTo(Vector2.Zero));
            Assert.That(input[0].Buttons, Is.EqualTo(InputButtons.Aim), "only aiming counts while seated");
            Assert.That(input[0].Yaw, Is.EqualTo(0.5f));

            var moved = seat + new Vector3(0f, 0f, 3f);
            var corrections = match.Corrections;
            Deliver(3, moved, riding: true, processed: 2);

            Assert.That(match.LocalState.Position, Is.EqualTo(moved), "the seat moves with the vehicle, not with the input");
            Assert.That(match.Corrections, Is.EqualTo(corrections), "following the seat is not a misprediction");
            match.SendInput();
            Assert.That(_transport.LastInput.Count, Is.EqualTo(2), "inputs the server has not processed are still sent");
        }

        private ClientMatch Join()
        {
            _client.Connect("127.0.0.1", 7777);
            _transport.Connected();
            _client.Poll();
            _transport.Deliver(new ConnectAcceptedMessage
            {
                PlayerId = _playerId,
                ServerVersion = "0.2.0",
                TickRate = 60,
                ServerTick = 0,
                Map = "NetArena01",
                MaxHealth = 100,
                Motor = NetMotorSettings.Default,
                KillPlaneY = ConnectAcceptedMessage.DefaultKillPlaneY,
            });
            _client.Poll();
            var world = new ClientWorld(
                "Test",
                new WorldMap(new[]
                {
                    WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(50f, 0.5f, 50f), Quaternion.Identity),
                }),
                NetMapLadders.None);
            Deliver(1, _spawn, riding: false, processed: -1);
            var match = new ClientMatch(_client, world.CreateSimulation(_client.Motor, _client.TickRate, _client.KillPlaneY));
            Assert.That(match.IsPredicting, Is.True);
            return match;
        }

        private void Deliver(int tick, Vector3 position, bool riding, int processed)
        {
            var snapshot = Snapshot(tick, Vector3.Zero, seat: riding ? new VehicleSeatSync(0, 1) : VehicleSeatSync.None);
            snapshot.HasOwner = true;
            snapshot.Owner.LastProcessedInputTick = processed;
            ref var row = ref snapshot.Players[0];
            row.Position = position;
            row.Health = 100;
            row.IsGrounded = true;
            row.IsCrouched = riding;
            row.WeaponId = "";
            _transport.Deliver(snapshot);
            _client.Poll();
        }

        private static VehiclePartDamageSync Taken(string partId, int damage)
        {
            Assert.That(VehiclePartDamageSync.None.TryAdd(partId, damage, out var parts), Is.True);
            return parts;
        }

        private static SnapshotMessage Snapshot(
            int tick,
            Vector3 position,
            VehicleDamageStage stage = VehicleDamageStage.Intact,
            VehicleSeatSync seat = default,
            VehiclePartDamageSync parts = null)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            ref var player = ref snapshot.Players.Add();
            player.PlayerId = _playerId;
            player.Vehicle = seat;
            ref var vehicle = ref snapshot.Vehicles.Add();
            vehicle.Index = 0;
            vehicle.Position = position;
            vehicle.Rotation = Quaternion.Identity;
            vehicle.Health = 500;
            vehicle.Stage = stage;
            vehicle.Parts = parts;
            return snapshot;
        }
    }
}
