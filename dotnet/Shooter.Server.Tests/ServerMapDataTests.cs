using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerMapDataTests
    {
        private TempDirectory _first;
        private TempDirectory _second;

        [SetUp]
        public void SetUp()
        {
            _first = new TempDirectory();
            _second = new TempDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            _first.Dispose();
            _second.Dispose();
        }

        [Test]
        public void LoadsTheCollisionAndNavMeshAsTheCoreInterfaces()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Directory, Is.EqualTo(Path.GetFullPath(directory)));
            Assert.That(data.ColliderCount, Is.EqualTo(1));
            Assert.That(data.World.Raycast(new Vector3(0f, 5f, 0f), -Vector3.UnitY, 10f, ~0, out var hit), Is.True);
            Assert.That(hit.Point.Y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(data.Navigation.SamplePosition(new Vector3(0.2f, 0.5f, 0.2f), 1f, ~0, out _), Is.True);
            Assert.That(data.LogLine, Does.StartWith("[Server] map NetArena01 colliders=1 navTriangles=1 navLinks=0 spawnPoints=none controlPoints=none structures=none ladders=0 vehicles=0 water=0 ziplines=0 from="));
            Assert.That(data.Ziplines.Ziplines, Is.Empty, "a map exported before ziplines has none");
            Assert.That(data.Water.SurfaceAt(new Vector3(0f, 1f, 0f)), Is.Null, "a map exported before water has none");
        }

        [Test]
        public void LoadsTheWaterBesideTheCollision()
        {
            TestMaps.Write(_second.Path, "NetArena01", water: TestMaps.OnePool());

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Water.Water, Has.Count.EqualTo(1));
            Assert.That(data.Water.SurfaceAt(new Vector3(0f, 1f, 0f)), Is.EqualTo(3.5f));
            Assert.That(data.LogLine, Does.Contain(" water=1 "));
        }

        [Test]
        public void BrokenWaterIsAnError()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            File.WriteAllText(
                Path.Combine(directory, "NetArena01" + NetMapWater.Extension),
                "{ \"water\": [ { \"min\": [0, 0, 0], \"max\": [1, 1, 1], \"surfaceY\": 2 } ] }");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NetArena01.water.json"));
            Assert.That(error, Does.Contain("water[0].surfaceY"));
        }

        [Test]
        public void LoadsThePickupsBesideTheCollisionAndHasNoneWithoutTheFile()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var bare, out var error), Is.True, error);
            Assert.That(bare.Pickups.Pickups, Is.Empty, "a map exported before pickups has none");
            Assert.That(bare.Pickups.AmmoCrates, Is.Empty);

            var pickups = new NetMapPickups(
                new[] { new NetMapPickup("medkit", PickupSpec.Health(25), new Vector3(1f, 0.5f, 2f), 1f, 20f, true) },
                new[] { new NetMapAmmoCrate("crate", new Vector3(-3f, 0.5f, 0f), 1.6f, 1f) });
            File.WriteAllText(Path.Combine(directory, "NetArena01" + NetMapPickups.Extension), NetMapPickups.Write(pickups));

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var data, out error), Is.True, error);
            Assert.That(data.Pickups.Pickups, Has.Count.EqualTo(1));
            Assert.That(data.Pickups.Pickups[0].Spec.Kind, Is.EqualTo(PickupKind.Health));
            Assert.That(data.Pickups.AmmoCrates[0].Id, Is.EqualTo("crate"));
        }

        [Test]
        public void BrokenPickupsAreAnError()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            File.WriteAllText(
                Path.Combine(directory, "NetArena01" + NetMapPickups.Extension),
                "{ \"pickups\": [ { \"id\": \"a\", \"kind\": \"Health\", \"amount\": 0, \"position\": [0, 0, 0], \"radius\": 1, \"respawnSeconds\": 0 } ] }");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NetArena01.pickups.json"));
            Assert.That(error, Does.Contain("pickups[0].amount"));
        }

        [Test]
        public void LoadsTheLaddersBesideTheCollision()
        {
            TestMaps.Write(_second.Path, "NetArena01", ladders: TestMaps.OneLadder());

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Ladders.Ladders, Has.Count.EqualTo(1));
            Assert.That(data.Ladders.Ladders[0].Span.Bottom, Is.EqualTo(new Vector3(0f, 0.5f, 3f)));
            Assert.That(data.LogLine, Does.Contain(" ladders=1 "));
        }

        [Test]
        public void BrokenLaddersAreAnError()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            File.WriteAllText(Path.Combine(directory, "NetArena01" + NetMapLadders.Extension), "{ \"ladders\": [ { \"bottom\": [0, 0, 0] } ] }");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NetArena01.ladders.json"));
        }

        [Test]
        public void LoadsTheZiplinesBesideTheCollision()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            File.WriteAllText(
                Path.Combine(directory, "NetArena01" + NetMapZiplines.Extension),
                "{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 2, 20], \"maxSpeed\": 10 } ] }");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Ziplines.Ziplines, Has.Count.EqualTo(1));
            Assert.That(data.Ziplines.Ziplines[0].End, Is.EqualTo(new Vector3(0f, 2f, 20f)));
            Assert.That(data.Ziplines.Ziplines[0].Spec.MaxSpeed, Is.EqualTo(10f));
            Assert.That(data.Ziplines.Ziplines[0].Spec.MinSpeed, Is.EqualTo(ZiplineSpec.DefaultMinSpeed), "a left-out speed reads as the default");
            Assert.That(data.LogLine, Does.Contain(" ziplines=1 "));
        }

        [Test]
        public void BrokenZiplinesAreAnError()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            File.WriteAllText(
                Path.Combine(directory, "NetArena01" + NetMapZiplines.Extension),
                "{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 6, 1] } ] }");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NetArena01.ziplines.json"));
            Assert.That(error, Does.Contain("ziplines[0].end"));
        }

        [Test]
        public void LoadsTheVehiclesBesideTheCollision()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            File.WriteAllText(
                Path.Combine(directory, "NetArena01" + NetMapVehicles.Extension),
                "{ \"vehicles\": [ { \"id\": \"cart\", \"position\": [1, 0.5, 2], \"seats\": [ { \"role\": \"Driver\", \"sit\": [0, 1, 0] } ] } ] }");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Vehicles.Vehicles, Has.Count.EqualTo(1));
            Assert.That(data.Vehicles.Vehicles[0].Position, Is.EqualTo(new Vector3(1f, 0.5f, 2f)));
            Assert.That(data.LogLine, Does.Contain(" vehicles=1 "));
        }

        [Test]
        public void BrokenVehiclesAreAnError()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            File.WriteAllText(Path.Combine(directory, "NetArena01" + NetMapVehicles.Extension), "{ \"vehicles\": [ { \"id\": \"cart\" } ] }");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NetArena01.vehicles.json"));
        }

        [Test]
        public void TheFirstRootWithTheMapWins()
        {
            TestMaps.Write(_first.Path, "NetTest");
            TestMaps.Write(_second.Path, "NetTest");

            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out var data, out _), Is.True);
            Assert.That(data.Directory, Does.StartWith(Path.GetFullPath(_first.Path)));
        }

        [Test]
        public void MissingCollisionNamesEveryPlaceLooked()
        {
            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out var data, out var error), Is.False);

            Assert.That(data, Is.Null);
            Assert.That(error, Does.Contain("NetTest.fpsmap"));
            Assert.That(error, Does.Contain(Path.GetFullPath(_first.Path)));
            Assert.That(error, Does.Contain(Path.GetFullPath(_second.Path)));
            Assert.That(error, Does.Contain("make export-server-data"));
        }

        [Test]
        public void MissingNavMeshIsAnError()
        {
            TestMaps.Write(_first.Path, "NetTest", navigation: false);

            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NetTest.fpsnav"));
        }

        [Test]
        public void BrokenFileIsAnError()
        {
            var directory = TestMaps.Write(_first.Path, "NetTest");
            File.WriteAllBytes(Path.Combine(directory, "NetTest.fpsmap"), new byte[] { 1, 2, 3 });

            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("cannot read "));
            Assert.That(error, Does.Contain("NetTest.fpsmap"));
        }

        [Test]
        public void NameThatIsNotAMapIsRejectedBeforeTouchingFiles()
        {
            Assert.That(ServerMapData.TryLoad("../NetTest", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("not a scene name"));
        }

        [Test]
        public void ThePoolLoadsEveryCandidateAfterTheCurrentMapOnce()
        {
            TestMaps.Write(_second.Path, "NetArena01");
            TestMaps.Write(_second.Path, "NetArena02");
            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var current, out var error), Is.True, error);

            Assert.That(ServerMapData.TryLoadPool(current, Ids("NetArena02", "NetArena01"), Roots(), out var maps, out error), Is.True, error);

            Assert.That(maps, Has.Count.EqualTo(2));
            Assert.That(maps[0], Is.SameAs(current));
            Assert.That(maps[1].Name, Is.EqualTo("NetArena02"));
            Assert.That(ServerMapData.PoolLogLine(maps), Is.EqualTo("[Server] map pool loaded 2 maps: NetArena01, NetArena02"));
        }

        [Test]
        public void AnEmptyPoolKeepsOnlyTheCurrentMap()
        {
            TestMaps.Write(_second.Path, "NetArena01");
            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var current, out var error), Is.True, error);

            Assert.That(ServerMapData.TryLoadPool(current, Ids(), Roots(), out var maps, out error), Is.True, error);

            Assert.That(maps, Is.EqualTo(new[] { current }));
        }

        [Test]
        public void ACandidateThatCannotBeReadFailsThePool()
        {
            TestMaps.Write(_second.Path, "NetArena01");
            TestMaps.Write(_second.Path, "NetArena03", navigation: false);
            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var current, out var error), Is.True, error);

            Assert.That(ServerMapData.TryLoadPool(current, Ids("NetArena02"), Roots(), out var maps, out error), Is.False);
            Assert.That(maps, Is.Null);
            Assert.That(error, Does.StartWith("map pool: map NetArena02 has no collision: NetArena02.fpsmap"));

            Assert.That(ServerMapData.TryLoadPool(current, Ids("NetArena03"), Roots(), out _, out error), Is.False);
            Assert.That(error, Does.StartWith("map pool: map NetArena03 has no navmesh"));
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Ids(params string[] ids) => ids;

        private string[] Roots()
        {
            return new[] { _first.Path, _second.Path };
        }
    }
}
