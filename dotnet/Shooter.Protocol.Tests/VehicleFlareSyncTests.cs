using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class VehicleFlareSyncTests
    {
        [Test]
        public void TheSnapshotCarriesTheFlaresInTheAir()
        {
            var sent = new SnapshotMessage { Tick = 10 };
            ref var vehicle = ref sent.Vehicles.Add();
            vehicle.Rotation = Quaternion.Identity;
            vehicle.Health = 500;
            vehicle.Flares = VehicleFlareSync.From(Flares((3, new Vector3(1f, 20f, 2f)), (4, new Vector3(-1f, 19f, 2.5f))));

            var got = Send(sent, null);

            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.All));
            var flares = got.Vehicles[0].Flares;
            Assert.That(flares.Count, Is.EqualTo(2));
            Assert.That(flares.IdAt(0), Is.EqualTo(3));
            Assert.That(flares.PositionAt(0), Is.EqualTo(new Vector3(1f, 20f, 2f)));
            Assert.That(flares.TryLocate(4, out var second), Is.True);
            Assert.That(second, Is.EqualTo(new Vector3(-1f, 19f, 2.5f)));
            Assert.That(flares.TryLocate(5, out _), Is.False, "a flare that was not sent is not found");
        }

        [Test]
        public void AVehicleWithoutFlaresSendsNoFlaresEvenWithoutABaseline()
        {
            var sent = new SnapshotMessage { Tick = 10 };
            ref var vehicle = ref sent.Vehicles.Add();
            vehicle.Rotation = Quaternion.Identity;
            vehicle.Health = 500;
            var withFlare = new SnapshotMessage { Tick = 10 };
            ref var flaring = ref withFlare.Vehicles.Add();
            flaring.Rotation = Quaternion.Identity;
            flaring.Health = 500;
            flaring.Flares = VehicleFlareSync.From(Flares((0, Vector3.One)));

            var plain = Bytes(sent);
            var got = Send(sent, null);

            Assert.That(Bytes(withFlare) - plain, Is.EqualTo(1 + 4 + 12), "a count, the id and the position");
            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.All), "the missing flares are filled as none");
            Assert.That(got.Vehicles[0].Flares.Count, Is.Zero);
        }

        [Test]
        public void AFallingFlareSendsOnlyTheFlares()
        {
            var baseline = new SnapshotMessage { Tick = 10 };
            ref var before = ref baseline.Vehicles.Add();
            before.Rotation = Quaternion.Identity;
            before.Health = 500;
            before.Flares = VehicleFlareSync.From(Flares((1, new Vector3(0f, 30f, 0f))));
            var current = new SnapshotMessage { Tick = 11 };
            ref var after = ref current.Vehicles.Add();
            after.Rotation = Quaternion.Identity;
            after.Health = 500;
            after.Flares = VehicleFlareSync.From(Flares((1, new Vector3(0f, 29.5f, 0f))));
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.Flares));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Vehicles[0].Flares.PositionAt(0), Is.EqualTo(new Vector3(0f, 29.5f, 0f)));
            Assert.That(got.Vehicles[0].Health, Is.EqualTo(500));
        }

        [Test]
        public void BurntOutFlaresAreSentAsAnEmptyList()
        {
            var baseline = new SnapshotMessage { Tick = 10 };
            ref var before = ref baseline.Vehicles.Add();
            before.Rotation = Quaternion.Identity;
            before.Flares = VehicleFlareSync.From(Flares((1, Vector3.UnitY)));
            var current = new SnapshotMessage { Tick = 11 };
            ref var after = ref current.Vehicles.Add();
            after.Rotation = Quaternion.Identity;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.Flares));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Vehicles[0].Flares.Count, Is.Zero);
        }

        [Test]
        public void UnchangedFlaresAreTakenFromTheBaseline()
        {
            var baseline = new SnapshotMessage { Tick = 10 };
            ref var before = ref baseline.Vehicles.Add();
            before.Rotation = Quaternion.Identity;
            before.Flares = VehicleFlareSync.From(Flares((2, new Vector3(5f, 6f, 7f))));
            var current = new SnapshotMessage { Tick = 11 };
            current.Vehicles.Add() = baseline.Vehicles[0];
            current.Vehicles[0].Position = Vector3.UnitZ;
            var received = Send(baseline, null);

            var got = Send(current, baseline);

            Assert.That(got.Vehicles[0].Present, Is.EqualTo(VehicleFields.Transform));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.Vehicles[0].Flares.TryLocate(2, out var point), Is.True);
            Assert.That(point, Is.EqualTo(new Vector3(5f, 6f, 7f)));
        }

        [Test]
        public void MoreFlaresThanFitKeepTheNewest()
        {
            var flares = new List<CountermeasureFlare>();
            for (var i = 0; i < VehicleFlareSync.MaxFlares + 4; i++)
            {
                flares.Add(new CountermeasureFlare(i, new Vector3(i, 0f, 0f), Vector3.Zero, 0f));
            }

            var sync = VehicleFlareSync.From(flares);

            Assert.That(sync.Count, Is.EqualTo(VehicleFlareSync.MaxFlares));
            Assert.That(sync.IdAt(0), Is.EqualTo(4), "the four oldest burn out first, so they are left out");
            Assert.That(sync.IdAt(VehicleFlareSync.MaxFlares - 1), Is.EqualTo(VehicleFlareSync.MaxFlares + 3));
        }

        [Test]
        public void NoFlaresIsTheSharedEmptyList()
        {
            Assert.That(VehicleFlareSync.From(null), Is.SameAs(VehicleFlareSync.None));
            Assert.That(VehicleFlareSync.From(Array.Empty<CountermeasureFlare>()), Is.SameAs(VehicleFlareSync.None));
            Assert.That(VehicleFlareSync.None.Count, Is.Zero);
            Assert.That(default(VehicleSnapshot).Flares, Is.SameAs(VehicleFlareSync.None));
        }

        [Test]
        public void ListsWithTheSameFlaresInTheSamePlacesAreTheSame()
        {
            var first = VehicleFlareSync.From(Flares((1, Vector3.One), (2, Vector3.UnitX)));

            Assert.That(first.SameAs(VehicleFlareSync.From(Flares((1, Vector3.One), (2, Vector3.UnitX)))), Is.True);
            Assert.That(first.SameAs(VehicleFlareSync.From(Flares((1, Vector3.One), (2, Vector3.UnitY)))), Is.False);
            Assert.That(first.SameAs(VehicleFlareSync.From(Flares((1, Vector3.One), (3, Vector3.UnitX)))), Is.False);
            Assert.That(first.SameAs(VehicleFlareSync.From(Flares((1, Vector3.One)))), Is.False);
            Assert.That(first.SameAs(null), Is.False);
        }

        [Test]
        public void AFlareThatCannotBeSentIsRefusedWhenTheListIsMade()
        {
            Assert.That(() => VehicleFlareSync.From(Flares((-1, Vector3.Zero))), Throws.ArgumentException);
            Assert.That(() => VehicleFlareSync.From(Flares((0, new Vector3(float.NaN, 0f, 0f)))), Throws.ArgumentException);
            Assert.That(() => VehicleFlareSync.None.IdAt(0), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AFlareListedTwiceIsRefused()
        {
            // 書き手は同じ Id を 2 度並べられないので、正しく書いた後で 2 つ目の Id を 1 つ目と同じ値に書き換える
            const int first = 0x0A0B0C0D;
            const int second = 0x01020304;
            var sent = new SnapshotMessage { Tick = 1 };
            ref var vehicle = ref sent.Vehicles.Add();
            vehicle.Rotation = Quaternion.Identity;
            vehicle.Flares = VehicleFlareSync.From(Flares((first, Vector3.One), (second, Vector3.UnitX)));
            var writer = new MessageWriter();
            writer.WriteMessage(sent);
            var segment = writer.AsSegment();
            var bytes = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, bytes, 0, segment.Count);
            var at = IndexOf(bytes, Int32Bytes(second));
            Assert.That(at, Is.GreaterThan(0));
            Int32Bytes(first).CopyTo(bytes, at);

            Assert.That(
                () => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)),
                Throws.InstanceOf<MessageReadException>().With.Message.Contains("listed twice"));
        }

        // バイト順を決め打ちせず、書き手が書く並びで探す
        private static byte[] Int32Bytes(int value)
        {
            var writer = new MessageWriter();
            writer.WriteInt32(value);
            var segment = writer.AsSegment();
            var bytes = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, bytes, 0, segment.Count);
            return bytes;
        }

        private static List<CountermeasureFlare> Flares(params (int Id, Vector3 Position)[] flares)
        {
            var list = new List<CountermeasureFlare>();
            foreach (var (id, position) in flares)
            {
                list.Add(new CountermeasureFlare(id, position, Vector3.Zero, 0f));
            }

            return list;
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
