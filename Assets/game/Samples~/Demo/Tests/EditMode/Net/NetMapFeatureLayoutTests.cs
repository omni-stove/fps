using System;
using System.Linq;
using Shooter.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Net.Tests
{
    public sealed class NetMapFeatureLayoutTests
    {
        private const string Source = "Test.features.json";

        private static NetMapFeatureLayout Sample => new NetMapFeatureLayout(
            new[] { new NetMapLadderTower("Tower", new Vector3(-5f, -0.9f, 4f), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.North, 0.8f) },
            new[] { new NetMapWaterPool("Pond", new Vector3(6f, -0.9f, -2f), new Vector3(8f, 3f, 6f), 2.5f) },
            new[] { new NetMapVehicleSpot("Car", NetMapFeatureBuilder.CarKind, new Vector3(1f, -0.9f, -10f), 90f) });

        [Test]
        public void TheWrittenLayoutReadsBackTheSame()
        {
            var read = NetMapFeatureLayout.Parse(Sample.ToJson(), Source);

            Assert.That(read.Count, Is.EqualTo(3));
            var tower = read.LadderTowers.Single();
            Assert.That(tower.Name, Is.EqualTo("Tower"));
            Assert.That(tower.BaseCenter, Is.EqualTo(new Vector3(-5f, -0.9f, 4f)));
            Assert.That(tower.Size, Is.EqualTo(new Vector3(4f, 3f, 4f)));
            Assert.That(tower.Side, Is.EqualTo(NetMapFeatureLayout.North));
            Assert.That(tower.SideDirection, Is.EqualTo(Vector3.forward));
            Assert.That(tower.LadderWidth, Is.EqualTo(0.8f));

            var pool = read.WaterPools.Single();
            Assert.That(pool.Name, Is.EqualTo("Pond"));
            Assert.That(pool.GroundCenter, Is.EqualTo(new Vector3(6f, -0.9f, -2f)));
            Assert.That(pool.Size, Is.EqualTo(new Vector3(8f, 3f, 6f)));
            Assert.That(pool.SurfaceHeight, Is.EqualTo(2.5f));

            var vehicle = read.Vehicles.Single();
            Assert.That(vehicle.Name, Is.EqualTo("Car"));
            Assert.That(vehicle.Kind, Is.EqualTo(NetMapFeatureBuilder.CarKind));
            Assert.That(vehicle.Position, Is.EqualTo(new Vector3(1f, -0.9f, -10f)));
            Assert.That(vehicle.YawDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void TheJsonUsesTheDocumentedItemNames()
        {
            var json = Sample.ToJson();

            Assert.That(json, Does.Contain("\"ladderTowers\"").And.Contain("\"waterPools\"").And.Contain("\"vehicles\""));
            Assert.That(json, Does.Contain("\"baseCenter\"").And.Contain("\"side\": \"north\"").And.Contain("\"ladderWidth\""));
            Assert.That(json, Does.Contain("\"groundCenter\"").And.Contain("\"surfaceHeight\""));
            Assert.That(json, Does.Contain("\"kind\": \"car\"").And.Contain("\"yawDegrees\""));
        }

        [Test]
        public void MissingListsAreEmpty()
        {
            var read = NetMapFeatureLayout.Parse("{}", Source);

            Assert.That(read.LadderTowers, Is.Empty);
            Assert.That(read.WaterPools, Is.Empty);
            Assert.That(read.Vehicles, Is.Empty);
            Assert.That(read.Count, Is.Zero);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        public void TextThatIsNotAnObjectIsRefusedWithTheSource(string text)
        {
            var e = Assert.Throws<InvalidOperationException>(() => NetMapFeatureLayout.Parse(text, Source));

            Assert.That(e.Message, Does.StartWith(Source));
        }

        [Test]
        public void EveryInvalidEntryIsReportedTogether()
        {
            const string text = "{"
                + "\"ladderTowers\": ["
                + "{ \"name\": \"\", \"size\": { \"x\": 4, \"y\": 3, \"z\": 4 }, \"side\": \"up\", \"ladderWidth\": 0.8 },"
                + "{ \"name\": \"Narrow\", \"size\": { \"x\": 4, \"y\": 3, \"z\": 0.5 }, \"side\": \"east\", \"ladderWidth\": 0.8 }"
                + "],"
                + "\"waterPools\": [ { \"name\": \"Shallow\", \"size\": { \"x\": 4, \"y\": 1, \"z\": 4 }, \"surfaceHeight\": 2 } ],"
                + "\"vehicles\": [ { \"name\": \"Nothing\", \"kind\": \"\" } ]"
                + "}";

            var e = Assert.Throws<InvalidOperationException>(() => NetMapFeatureLayout.Parse(text, Source));

            Assert.That(e.Message, Does.Contain("ladderTowers[0] has no name"));
            Assert.That(e.Message, Does.Contain("ladderTowers[0] has the side 'up'; use one of east, west, north, south"));
            Assert.That(e.Message, Does.Contain("ladderTowers[1] has a ladder (0.8 m) wider than the east face of the tower"));
            Assert.That(e.Message, Does.Contain("waterPools[0] has a surfaceHeight (2 m) above the top of its box (1 m)"));
            Assert.That(e.Message, Does.Contain("vehicles[0] has no kind"));
        }

        [Test]
        public void ATowerOrPoolWithoutASizeIsRefused()
        {
            const string text = "{ \"ladderTowers\": [ { \"name\": \"Flat\", \"side\": \"west\", \"ladderWidth\": 0.8 } ],"
                + " \"waterPools\": [ { \"name\": \"Dry\" } ] }";

            var e = Assert.Throws<InvalidOperationException>(() => NetMapFeatureLayout.Parse(text, Source));

            Assert.That(e.Message, Does.Contain("ladderTowers[0] has a size"));
            Assert.That(e.Message, Does.Contain("waterPools[0] has a size"));
            Assert.That(e.Message, Does.Contain("waterPools[0] has a surfaceHeight 0 that is not positive"));
        }

        [TestCase(NetMapFeatureLayout.East, 1f, 0f, 0f)]
        [TestCase(NetMapFeatureLayout.West, -1f, 0f, 0f)]
        [TestCase(NetMapFeatureLayout.North, 0f, 0f, 1f)]
        [TestCase(NetMapFeatureLayout.South, 0f, 0f, -1f)]
        public void SidesPointAlongTheWorldAxes(string side, float x, float y, float z)
        {
            Assert.That(NetMapFeatureLayout.TryGetSideDirection(side, out var direction), Is.True);
            Assert.That(direction, Is.EqualTo(new Vector3(x, y, z)));
        }

        [TestCase(null)]
        [TestCase("East")]
        [TestCase("up")]
        public void OtherSidesAreNotKnown(string side)
        {
            Assert.That(NetMapFeatureLayout.TryGetSideDirection(side, out _), Is.False);
        }

        [Test]
        public void TheFaceWidthIsAcrossTheSide()
        {
            var size = new Vector3(6f, 3f, 2f);

            Assert.That(NetMapFeatureLayout.FaceWidth(size, NetMapFeatureLayout.East), Is.EqualTo(2f));
            Assert.That(NetMapFeatureLayout.FaceWidth(size, NetMapFeatureLayout.West), Is.EqualTo(2f));
            Assert.That(NetMapFeatureLayout.FaceWidth(size, NetMapFeatureLayout.North), Is.EqualTo(6f));
            Assert.That(NetMapFeatureLayout.FaceWidth(size, NetMapFeatureLayout.South), Is.EqualTo(6f));
        }

        [Test]
        public void TheLayoutKeepsItsOwnCopyOfTheLists()
        {
            var vehicles = new[] { new NetMapVehicleSpot("Car", NetMapFeatureBuilder.CarKind, Vector3.zero, 0f) };
            var layout = new NetMapFeatureLayout(Array.Empty<NetMapLadderTower>(), Array.Empty<NetMapWaterPool>(), vehicles);

            vehicles[0] = new NetMapVehicleSpot("Other", NetMapFeatureBuilder.ParkedKind, Vector3.one, 0f);

            Assert.That(layout.Vehicles.Single().Name, Is.EqualTo("Car"));
            Assert.Throws<ArgumentNullException>(() => new NetMapFeatureLayout(null, Array.Empty<NetMapWaterPool>(), vehicles));
        }
    }
}
