using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMapPointsTests
    {
        private static NetMapPoints WithLootAndZone()
        {
            return new NetMapPoints(
                new List<SpawnPoint> { new SpawnPoint(new Vector3(1f, 0.5f, -2.25f)) },
                new List<ControlPointArea>(),
                lootContainers: new List<NetMapLootContainer>
                {
                    new NetMapLootContainer("crate-north", "common", new Vector3(4f, 0f, 12.5f), 1.6f),
                    new NetMapLootContainer("crate-south", string.Empty, new Vector3(-4f, 0f, -12.5f), 2f),
                },
                extractionPoints: new List<NetMapExtractionPoint>
                {
                    new NetMapExtractionPoint("exit-east", new Vector3(30f, 0f, 0f), 3f),
                    new NetMapExtractionPoint("exit-west", new Vector3(-30f, 0.25f, 0f), 4.5f),
                },
                zone: new NetMapZoneCircle(new Vector3(0f, 1f, 2f), 60f),
                dropHeight: 80f);
        }

        [Test]
        public void LootContainersExtractionPointsTheZoneAndTheDropHeightReadBack()
        {
            var text = NetMapPoints.Write(WithLootAndZone());
            var read = NetMapPoints.Parse(text, "NetBr01.points.json");

            Assert.That(read.LootContainers, Has.Count.EqualTo(2));
            Assert.That(read.LootContainers[0].Id, Is.EqualTo("crate-north"));
            Assert.That(read.LootContainers[0].TableId, Is.EqualTo("common"));
            Assert.That(read.LootContainers[0].Position, Is.EqualTo(new Vector3(4f, 0f, 12.5f)));
            Assert.That(read.LootContainers[0].Reach, Is.EqualTo(1.6f));
            Assert.That(read.LootContainers[1].Id, Is.EqualTo("crate-south"));
            Assert.That(read.LootContainers[1].TableId, Is.Empty, "a box without a table opens empty");
            Assert.That(read.LootContainers[1].Reach, Is.EqualTo(2f));
            Assert.That(read.ExtractionPoints, Has.Count.EqualTo(2));
            Assert.That(read.ExtractionPoints[0].Id, Is.EqualTo("exit-east"));
            Assert.That(read.ExtractionPoints[0].Center, Is.EqualTo(new Vector3(30f, 0f, 0f)));
            Assert.That(read.ExtractionPoints[0].Radius, Is.EqualTo(3f));
            Assert.That(read.ExtractionPoints[1].Center, Is.EqualTo(new Vector3(-30f, 0.25f, 0f)));
            Assert.That(read.ExtractionPoints[1].Radius, Is.EqualTo(4.5f));
            Assert.That(read.Zone.Center, Is.EqualTo(new Vector3(0f, 1f, 2f)));
            Assert.That(read.Zone.Radius, Is.EqualTo(60f));
            Assert.That(read.Zone.HorizontalCenter, Is.EqualTo(new Vector2(0f, 2f)));
            Assert.That(read.DropHeight, Is.EqualTo(80f));
            Assert.That(NetMapPoints.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void TheNewItemsAreWrittenAfterTheOldOnes()
        {
            var text = NetMapPoints.Write(WithLootAndZone());

            Assert.That(text, Does.Contain(
                "  \"lootContainers\": [\n"
                + "    {\n"
                + "      \"id\": \"crate-north\",\n"
                + "      \"table\": \"common\",\n"
                + "      \"position\": [4, 0, 12.5],\n"
                + "      \"reach\": 1.6\n"
                + "    },\n"));
            Assert.That(text, Does.Contain(
                "  \"extractionPoints\": [\n"
                + "    {\n"
                + "      \"id\": \"exit-east\",\n"
                + "      \"center\": [30, 0, 0],\n"
                + "      \"radius\": 3\n"
                + "    },\n"));
            Assert.That(text, Does.Contain(
                "  \"zone\": {\n"
                + "    \"center\": [0, 1, 2],\n"
                + "    \"radius\": 60\n"
                + "  },\n"
                + "  \"dropHeight\": 80\n"));
            Assert.That(text.IndexOf("\"controlPoints\"", StringComparison.Ordinal),
                Is.LessThan(text.IndexOf("\"lootContainers\"", StringComparison.Ordinal)));
        }

        [Test]
        public void AMapWithoutTheNewItemsHasThemEmptyAndWritesAsBefore()
        {
            const string text = "{\"spawnPoints\": [{\"position\": [0, 1, 2]}], \"controlPoints\": []}";

            var read = NetMapPoints.Parse(text, "NetTest.points.json");
            var written = NetMapPoints.Write(read);

            Assert.That(read.LootContainers, Is.Empty);
            Assert.That(read.ExtractionPoints, Is.Empty);
            Assert.That(read.Zone, Is.Null);
            Assert.That(read.DropHeight, Is.Null);
            Assert.That(written, Does.Not.Contain(NetMapPoints.LootContainersItem));
            Assert.That(written, Does.Not.Contain(NetMapPoints.ExtractionPointsItem));
            Assert.That(written, Does.Not.Contain("\"" + NetMapPoints.ZoneItem + "\""));
            Assert.That(written, Does.Not.Contain(NetMapPoints.DropHeightItem));
        }

        [Test]
        public void ADropHeightOfZeroIsKept()
        {
            var points = new NetMapPoints(new List<SpawnPoint>(), new List<ControlPointArea>(), dropHeight: 0f);

            var read = NetMapPoints.Parse(NetMapPoints.Write(points), "flat.points.json");

            Assert.That(read.DropHeight, Is.EqualTo(0f));
        }

        // 今のマップ (NetArena01・NetTest) は箱もエリアも置いていないので、読み直しても中身が変わらない
        [TestCase("NetArena01")]
        [TestCase("NetTest")]
        public void TheCommittedMapsHaveNoneOfTheNewItemsAndWriteTheSame(string map)
        {
            var path = Path.Combine(NetMatchFileTests.ServerDataRoot(), "maps", map + NetMapPoints.Extension);
            var text = File.ReadAllText(path);

            var read = NetMapPoints.Parse(text, path);

            Assert.That(read.LootContainers, Is.Empty);
            Assert.That(read.ExtractionPoints, Is.Empty);
            Assert.That(read.Zone, Is.Null);
            Assert.That(read.DropHeight, Is.Null);
            Assert.That(NetMapPoints.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void TwoBoxesOrExitsWithTheSameIdAreRefused()
        {
            var box = new NetMapLootContainer("crate", "common", Vector3.Zero, 1f);
            var exit = new NetMapExtractionPoint("exit", Vector3.Zero, 1f);

            Assert.That(
                Assert.Throws<ArgumentException>(() => new NetMapPoints(
                    new List<SpawnPoint>(), new List<ControlPointArea>(), lootContainers: new[] { box, box })).ParamName,
                Is.EqualTo("lootContainers"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => new NetMapPoints(
                    new List<SpawnPoint>(), new List<ControlPointArea>(), extractionPoints: new[] { exit, exit })).ParamName,
                Is.EqualTo("extractionPoints"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapPoints(
                    new List<SpawnPoint>(), new List<ControlPointArea>(), dropHeight: -1f)).ParamName,
                Is.EqualTo("dropHeight"));
        }

        [Test]
        public void TheNewPointsNeedAnIdAndAPositiveFiniteSize()
        {
            Assert.That(Assert.Throws<ArgumentException>(() => new NetMapLootContainer(" ", "t", Vector3.Zero, 1f)).ParamName, Is.EqualTo("id"));
            Assert.That(Assert.Throws<ArgumentNullException>(() => new NetMapLootContainer("c", null, Vector3.Zero, 1f)).ParamName, Is.EqualTo("tableId"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapLootContainer("c", "t", new Vector3(float.NaN, 0f, 0f), 1f)).ParamName,
                Is.EqualTo("position"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapLootContainer("c", "t", Vector3.Zero, 0f)).ParamName, Is.EqualTo("reach"));
            Assert.That(Assert.Throws<ArgumentException>(() => new NetMapExtractionPoint(string.Empty, Vector3.Zero, 1f)).ParamName, Is.EqualTo("id"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapExtractionPoint("e", Vector3.Zero, float.PositiveInfinity)).ParamName,
                Is.EqualTo("radius"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapZoneCircle(Vector3.Zero, -2f)).ParamName, Is.EqualTo("radius"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new NetMapZoneCircle(new Vector3(0f, float.NegativeInfinity, 0f), 2f)).ParamName,
                Is.EqualTo("center"));
        }

        [Test]
        public void ReachAndExitsAreMeasuredOnTheGround()
        {
            var box = new NetMapLootContainer("c", "t", new Vector3(0f, 0f, 0f), 2f);
            var exit = new NetMapExtractionPoint("e", new Vector3(10f, 0f, 0f), 3f);

            Assert.That(box.IsInReach(new Vector3(1.2f, 5f, 1.6f)), Is.True, "2 m away on the ground, 5 m above");
            Assert.That(box.IsInReach(new Vector3(2.1f, 0f, 0f)), Is.False);
            Assert.That(exit.Contains(new Vector3(13f, -4f, 0f)), Is.True);
            Assert.That(exit.Contains(new Vector3(10f, 0f, 3.1f)), Is.False);
        }

        [TestCase("{\"lootContainers\": [{\"table\": \"t\", \"position\": [0, 0, 0], \"reach\": 1}]}", "lootContainers[0].id")]
        [TestCase("{\"lootContainers\": [{\"id\": \"\", \"table\": \"t\", \"position\": [0, 0, 0], \"reach\": 1}]}", "lootContainers[0].id")]
        [TestCase("{\"lootContainers\": [{\"id\": 3, \"table\": \"t\", \"position\": [0, 0, 0], \"reach\": 1}]}", "lootContainers[0].id")]
        [TestCase("{\"lootContainers\": [{\"id\": \"a\", \"table\": \"t\", \"position\": [0, 0, 0], \"reach\": 1}, "
            + "{\"id\": \"a\", \"table\": \"t\", \"position\": [1, 0, 0], \"reach\": 1}]}", "lootContainers[1].id")]
        [TestCase("{\"lootContainers\": [{\"id\": \"a\", \"position\": [0, 0, 0], \"reach\": 1}]}", "lootContainers[0].table")]
        [TestCase("{\"lootContainers\": [{\"id\": \"a\", \"table\": \"t\", \"position\": [0, 0], \"reach\": 1}]}", "lootContainers[0].position")]
        [TestCase("{\"lootContainers\": [{\"id\": \"a\", \"table\": \"t\", \"position\": [0, 0, 0]}]}", "lootContainers[0].reach")]
        [TestCase("{\"lootContainers\": [{\"id\": \"a\", \"table\": \"t\", \"position\": [0, 0, 0], \"reach\": 0}]}", "lootContainers[0].reach")]
        [TestCase("{\"lootContainers\": [{\"id\": \"a\", \"table\": \"t\", \"position\": [0, 0, 0], \"reach\": 1, \"name\": \"Crate\"}]}",
            "lootContainers[0].name")]
        [TestCase("{\"extractionPoints\": [{\"center\": [0, 0, 0], \"radius\": 3}]}", "extractionPoints[0].id")]
        [TestCase("{\"extractionPoints\": [{\"id\": \"e\", \"center\": [0, 0, 0], \"radius\": 3}, {\"id\": \"e\", \"center\": [9, 0, 0], \"radius\": 3}]}",
            "extractionPoints[1].id")]
        [TestCase("{\"extractionPoints\": [{\"id\": \"e\", \"radius\": 3}]}", "extractionPoints[0].center")]
        [TestCase("{\"extractionPoints\": [{\"id\": \"e\", \"center\": [0, 0, 0], \"radius\": -3}]}", "extractionPoints[0].radius")]
        [TestCase("{\"zone\": {\"radius\": 60}}", "zone.center")]
        [TestCase("{\"zone\": {\"center\": [0, 0, 0]}}", "zone.radius")]
        [TestCase("{\"zone\": {\"center\": [0, 0, 0], \"radius\": 0}}", "zone.radius")]
        [TestCase("{\"zone\": {\"center\": [0, 0, 0], \"radius\": 60, \"phases\": []}}", "zone.phases")]
        [TestCase("{\"dropHeight\": -1}", "dropHeight")]
        [TestCase("{\"dropHeight\": \"high\"}", "dropHeight")]
        public void BadNewItemsNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapPoints.Parse(text, "bad.points.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }
    }
}
