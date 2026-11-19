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
        public void TheSnapshotCarriesTheDamageEachPartTook()
        {
            var sent = new SnapshotMessage { Tick = 10 };
            ref var vehicle = ref sent.Vehicles.Add();
            vehicle.Rotation = Quaternion.Identity;
            vehicle.Health = 700;
            vehicle.Parts = Parts(("wing_left", 300), ("engine", 40));

            var got = Send(sent, null);

            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.All));
            var parts = got.Vehicles[0].Parts;
            Assert.That(parts.Count, Is.EqualTo(2));
            Assert.That(parts.PartIdAt(0), Is.EqualTo("wing_left"));
            Assert.That(parts.TakenAt(0), Is.EqualTo(300));
            Assert.That(parts.TakenOf("engine"), Is.EqualTo(40));
            Assert.That(parts.TakenOf("tail"), Is.Zero, "a part nobody shot is not listed");
        }

        [Test]
        public void AVehicleWithoutDamagedPartsSendsNoPartsEvenWithoutABaseline()
        {
            var sent = new SnapshotMessage { Tick = 10 };
            ref var vehicle = ref sent.Vehicles.Add();
            vehicle.Rotation = Quaternion.Identity;
            vehicle.Health = 1000;
            var withParts = new SnapshotMessage { Tick = 10 };
            ref var shot = ref withParts.Vehicles.Add();
            shot.Rotation = Quaternion.Identity;
            shot.Health = 1000;
            shot.Parts = Parts(("engine", 1));

            var plain = Bytes(sent);
            var got = Send(sent, null);

            Assert.That(Bytes(withParts) - plain, Is.EqualTo(1 + 2 + "engine".Length + 4), "a count, the id and the damage");
            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.All), "the missing parts are filled as none");
            Assert.That(got.Vehicles[0].Parts.Count, Is.Zero);
        }

        [Test]
        public void ANewlyShotPartSendsOnlyTheParts()
        {
            var baseline = new SnapshotMessage { Tick = 10 };
            ref var before = ref baseline.Vehicles.Add();
            before.Health = 1000;
            before.Rotation = Quaternion.Identity;
            before.Parts = Parts(("wing_left", 100));
            var current = new SnapshotMessage { Tick = 11 };
            ref var after = ref current.Vehicles.Add();
            after.Health = 1000;
            after.Rotation = Quaternion.Identity;
            after.Parts = Parts(("wing_left", 250));
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.Parts));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Vehicles[0].Parts.TakenOf("wing_left"), Is.EqualTo(250));
            Assert.That(got.Vehicles[0].Health, Is.EqualTo(1000));
        }

        [Test]
        public void UnchangedPartsAreTakenFromTheBaseline()
        {
            var baseline = new SnapshotMessage { Tick = 10 };
            ref var before = ref baseline.Vehicles.Add();
            before.Rotation = Quaternion.Identity;
            before.Parts = Parts(("tail", 60));
            var current = new SnapshotMessage { Tick = 11 };
            current.Vehicles.Add() = baseline.Vehicles[0];
            current.Vehicles[0].Position = Vector3.UnitZ;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.Transform));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Vehicles[0].Parts.TakenOf("tail"), Is.EqualTo(60));
        }

        [Test]
        public void APartListedTwiceIsRefused()
        {
            // 書き手は同じ部位を 2 度並べられないので、正しく書いた後で 2 つ目の Id を 1 つ目と同じ文字に書き換える
            var sent = new SnapshotMessage { Tick = 1 };
            ref var vehicle = ref sent.Vehicles.Add();
            vehicle.Rotation = Quaternion.Identity;
            vehicle.Parts = Parts(("aaaa", 1), ("bbbb", 2));
            var writer = new MessageWriter();
            writer.WriteMessage(sent);
            var segment = writer.AsSegment();
            var bytes = new byte[segment.Count];
            System.Array.Copy(segment.Array, segment.Offset, bytes, 0, segment.Count);
            var at = IndexOf(bytes, System.Text.Encoding.ASCII.GetBytes("bbbb"));
            Assert.That(at, Is.GreaterThan(0));
            System.Text.Encoding.ASCII.GetBytes("aaaa").CopyTo(bytes, at);

            Assert.That(
                () => MatchMessages.CreateClientTable().Read(new System.ArraySegment<byte>(bytes)),
                Throws.InstanceOf<MessageReadException>().With.Message.Contains("listed twice"));
        }

        [Test]
        public void AddingDamageMakesANewListAndLeavesTheOldOneAlone()
        {
            var first = Parts(("wing_left", 100));

            Assert.That(first.TryAdd("wing_left", 50, out var second), Is.True);
            Assert.That(second.TryAdd("engine", 20, out var third), Is.True);

            Assert.That(first.TakenOf("wing_left"), Is.EqualTo(100));
            Assert.That(second.TakenOf("wing_left"), Is.EqualTo(150));
            Assert.That(third.Count, Is.EqualTo(2));
            Assert.That(first.SameAs(Parts(("wing_left", 100))), Is.True);
            Assert.That(first.SameAs(second), Is.False);
            Assert.That(VehiclePartDamageSync.None.Count, Is.Zero);
            Assert.That(() => first.TryAdd("", 1, out _), Throws.ArgumentException);
            Assert.That(() => first.TryAdd("tail", -1, out _), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void AFullListTakesNoNewPartButKeepsAddingToItsOwn()
        {
            var parts = VehiclePartDamageSync.None;
            for (var i = 0; i < VehiclePartDamageSync.MaxParts; i++)
            {
                Assert.That(parts.TryAdd("part" + i, 1, out parts), Is.True);
            }

            Assert.That(parts.IsFull, Is.True);
            Assert.That(parts.TryAdd("one_too_many", 5, out var unchanged), Is.False);
            Assert.That(unchanged, Is.SameAs(parts));
            Assert.That(parts.TryAdd("part0", 5, out var added), Is.True);
            Assert.That(added.TakenOf("part0"), Is.EqualTo(6));
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

        private static VehiclePartDamageSync Parts(params (string Id, int Taken)[] parts)
        {
            var sync = VehiclePartDamageSync.None;
            foreach (var (id, taken) in parts)
            {
                Assert.That(sync.TryAdd(id, taken, out sync), Is.True);
            }

            return sync;
        }

        private static int IndexOf(byte[] bytes, byte[] pattern)
        {
            for (var i = 0; i + pattern.Length <= bytes.Length; i++)
            {
                var found = true;
                for (var j = 0; j < pattern.Length && found; j++)
                {
                    found = bytes[i + j] == pattern[j];
                }

                if (found)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int Bytes(SnapshotMessage message)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            return writer.AsSegment().Count;
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
