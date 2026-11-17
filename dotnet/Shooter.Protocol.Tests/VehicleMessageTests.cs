using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class VehicleMessageTests
    {
        private static readonly Quaternion _turned = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.75f);

        [Test]
        public void ARequestReadsBack()
        {
            var got = RoundTrip<VehicleRequestMessage>(
                new VehicleRequestMessage { Action = VehicleAction.SwitchSeat, Vehicle = 3, Seat = 1 }, MatchMessages.CreateServerTable());

            Assert.That(got.Action, Is.EqualTo(VehicleAction.SwitchSeat));
            Assert.That(got.Vehicle, Is.EqualTo(3));
            Assert.That(got.Seat, Is.EqualTo(1));
        }

        [Test]
        public void ARequestForAnySeatReadsBack()
        {
            var got = RoundTrip<VehicleRequestMessage>(new VehicleRequestMessage { Action = VehicleAction.Board, Vehicle = 0 }, MatchMessages.CreateServerTable());

            Assert.That(got.Seat, Is.EqualTo(VehicleRequestMessage.AnySeat));
        }

        [Test]
        public void AMoveReadsBack()
        {
            var got = RoundTrip<VehicleMoveMessage>(
                new VehicleMoveMessage
                {
                    Vehicle = 1,
                    Sequence = 42,
                    CorrectionGeneration = 2,
                    Position = new Vector3(1f, 2f, 3f),
                    Rotation = _turned,
                    Velocity = new Vector3(0f, 0f, 12f),
                },
                MatchMessages.CreateServerTable());

            Assert.That(got.Vehicle, Is.EqualTo(1));
            Assert.That(got.Sequence, Is.EqualTo(42));
            Assert.That(got.CorrectionGeneration, Is.EqualTo(2));
            Assert.That(got.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(got.Rotation, Is.EqualTo(_turned));
            Assert.That(got.Velocity, Is.EqualTo(new Vector3(0f, 0f, 12f)));
        }

        [Test]
        public void ACorrectionReadsBack()
        {
            var got = RoundTrip<VehicleCorrectedMessage>(
                new VehicleCorrectedMessage { Vehicle = 2, Generation = 5, Position = Vector3.One, Rotation = _turned, Velocity = Vector3.UnitX },
                MatchMessages.CreateClientTable());

            Assert.That(got.Vehicle, Is.EqualTo(2));
            Assert.That(got.Generation, Is.EqualTo(5));
            Assert.That(got.Position, Is.EqualTo(Vector3.One));
            Assert.That(got.Rotation, Is.EqualTo(_turned));
            Assert.That(got.Velocity, Is.EqualTo(Vector3.UnitX));
        }

        [Test]
        public void AMoveWithAnInfinitePositionIsRefused()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new VehicleMoveMessage { Position = new Vector3(float.PositiveInfinity, 0f, 0f), Rotation = Quaternion.Identity });

            Assert.That(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()), Throws.InstanceOf<MessageReadException>());
        }

        [Test]
        public void TheClientCannotSendACorrectionAndTheServerCannotSendAMove()
        {
            Assert.That(MatchMessages.CreateServerTable().TryGet(MessageIds.VehicleCorrected, out _), Is.False);
            Assert.That(MatchMessages.CreateClientTable().TryGet(MessageIds.VehicleMove, out _), Is.False);
            Assert.That(MatchMessages.CreateClientTable().TryGet(MessageIds.VehicleRequest, out _), Is.False);
        }

        [Test]
        public void TheSnapshotCarriesVehiclesAndWhoSitsWhere()
        {
            var sent = new SnapshotMessage { Tick = 10 };
            ref var vehicle = ref sent.Vehicles.Add();
            vehicle.Index = 0;
            vehicle.Position = new Vector3(4f, 0.5f, -2f);
            vehicle.Rotation = _turned;
            vehicle.Velocity = new Vector3(0f, 0f, 8f);
            vehicle.Health = 240;
            vehicle.Stage = VehicleDamageStage.Burning;
            ref var rider = ref sent.Players.Add();
            rider.PlayerId = 7;
            rider.Vehicle = new VehicleSeatSync(0, 2);
            ref var walker = ref sent.Players.Add();
            walker.PlayerId = 8;

            var got = Send(sent, null);

            Assert.That(got.Vehicles.Count, Is.EqualTo(1));
            Assert.That(got.Vehicles[0].Position, Is.EqualTo(new Vector3(4f, 0.5f, -2f)));
            Assert.That(got.Vehicles[0].Rotation, Is.EqualTo(_turned));
            Assert.That(got.Vehicles[0].Velocity, Is.EqualTo(new Vector3(0f, 0f, 8f)));
            Assert.That(got.Vehicles[0].Health, Is.EqualTo(240));
            Assert.That(got.Vehicles[0].Stage, Is.EqualTo(VehicleDamageStage.Burning));
            Assert.That(got.Players[0].Vehicle.Vehicle, Is.EqualTo(0));
            Assert.That(got.Players[0].Vehicle.Seat, Is.EqualTo(2));
            Assert.That(got.Players[0].Vehicle.IsRiding, Is.True);
            Assert.That(got.Players[1].Vehicle.IsRiding, Is.False, "a row nobody filled is not riding vehicle 0");
        }

        [Test]
        public void AVehicleThatOnlyMovedSendsOnlyItsTransform()
        {
            var baseline = new SnapshotMessage { Tick = 10 };
            ref var before = ref baseline.Vehicles.Add();
            before.Index = 0;
            before.Health = 1000;
            before.Rotation = Quaternion.Identity;
            var current = new SnapshotMessage { Tick = 11 };
            ref var after = ref current.Vehicles.Add();
            after.Index = 0;
            after.Health = 1000;
            after.Rotation = Quaternion.Identity;
            after.Position = new Vector3(0f, 0f, 1f);
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.Transform));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Vehicles[0].Health, Is.EqualTo(1000));
            Assert.That(got.Vehicles[0].Position, Is.EqualTo(new Vector3(0f, 0f, 1f)));
        }

        [Test]
        public void ASeatChangeSendsOnlyTheVehiclePart()
        {
            var baseline = new SnapshotMessage { Tick = 10 };
            baseline.Players.Add().PlayerId = 7;
            var current = new SnapshotMessage { Tick = 11 };
            ref var row = ref current.Players.Add();
            row.PlayerId = 7;
            row.Vehicle = new VehicleSeatSync(1, 0);
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Vehicle));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Players[0].Vehicle.Vehicle, Is.EqualTo(1));
        }

        [Test]
        public void ANegativeVehicleHealthIsNotWritten()
        {
            var sent = new SnapshotMessage { Tick = 1 };
            ref var vehicle = ref sent.Vehicles.Add();
            vehicle.Rotation = Quaternion.Identity;
            vehicle.Health = -1;

            Assert.That(() => new MessageWriter().WriteMessage(sent), Throws.InvalidOperationException);
        }

        private static T RoundTrip<T>(IMessage message, MessageTable table)
            where T : class, IMessage
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            var read = table.Read(writer.AsSegment());
            Assert.That(read, Is.InstanceOf<T>());
            return (T)read;
        }

        private static SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment()));
            return copy;
        }
    }
}
